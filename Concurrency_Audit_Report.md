# Concurrency and Saving Logic Audit Report

## 1. Executive Summary
An audit of the recently implemented concurrency and saving logic (Dirty Flags, Removal of Auto-Closing Saves, and Optimistic Concurrency Control via `RowVersion`) reveals that while the **Lost Update (Silent Overwrite)** problem has been successfully mitigated, a critical **False Conflict (Global Blocking)** issue has been introduced. 

In its current state, the application is highly restrictive in a multi-user environment. Flight Chiefs, Section Chiefs, and PMs working simultaneously—even on completely separate systems or projects—will constantly trigger concurrency lockout errors, forcing them to restart their applications.

---

## 2. Static Logic Analysis

### **A. Optimistic Concurrency Control (`RowVersion`)**
**Status:** Implemented Correctly (At the Schema Level)
**Analysis:** You successfully added `RowVersion` byte arrays to your Entity Framework models (`User`, `WorkItem`, `ProgressBlock`, etc.) and correctly mapped them in the `AppDbContext` using `.IsRowVersion()`. You also correctly catch the `DbUpdateConcurrencyException` in `DataService.cs`. This mathematically guarantees no user will ever silently overwrite another user's newer data.

### **B. Dirty Flag Implementation (`_hasUnsavedChanges`)**
**Status:** Implemented Correctly (With Caveats)
**Analysis:** You successfully added the `_hasUnsavedChanges` flag. It is set to `true` at the start of `SaveDataAsync` and set to `false` upon a successful database commit. The `EnsureSavedAsync` method properly respects this flag when the window closes. 

### **C. The Critical Flaw: Global State Overwrite**
**Status:** CRITICAL FAILURE
**Analysis:** Inside `ExecuteDbSaveAsync` (around line 536 in `DataService.cs`), the logic iterates through **every single item** across **every single system graph** (`_projectData.Systems`) and blindly sets its State in the ChangeTracker to `EntityState.Modified`:

```csharp
node.Entry.State = existsInDb ? EntityState.Modified : EntityState.Added;
```

Because of Optimistic Concurrency, Entity Framework translates `EntityState.Modified` into a literal SQL command that attempts to update the database row utilizing the `RowVersion` currently held in memory. Because you are setting *everything* to modified, Entity Framework attempts to validate the `RowVersion` of *every record in the Database* against the user's localized snapshot.

---

## 3. Dynamic User Scenarios

To illustrate why the global state overwrite is fatal to User Experience, let's examine common collaborative scenarios, specifically within the Gate Progress View.

### **Scenario 1: Two Users Editing Different Projects**
* **Context:** Flight Chief A is in the Gate Progress view checking off Progress Items for "Project Alpha". Technical Specialist B is in the Gate Progress view adjusting dates for "Project Beta".
* **Execution:** Flight Chief A clicks a checkbox. `SaveDataAsync` executes and safely persists the changes. The database increments the `RowVersion` for Project Alpha.
* **Result:** When Technical Specialist B attempts to save their dates for Project Beta, `ExecuteDbSaveAsync` forces Entity Framework to update *both* Project Beta (which B changed) *and* Project Alpha (which B did not change, but still holds the stale `RowVersion` in memory). 
* **Outcome:** Technical Specialist B is hit with a `DbUpdateConcurrencyException`. Their valid changes to Project Beta are rejected entirely, and they are forced to restart the app, simply because somebody else updated a completely unrelated project.

### **Scenario 2: Two Users Editing the Same Project**
* **Context:** Section Chief A and PM B are simultaneously reviewing the Gate Progress View for "Project Delta". 
* **Execution:** 
    1. Section Chief A marks "Design Review" as complete and saves.
    2. PM B marks "Budget Approval" as complete and saves.
* **Result:** PM B's save will fail with a concurrency conflict. 
* **Outcome:** This is technically the correct behavior of Optimistic Concurrency (protecting the row), but because PM B has to restart the entire application to retrieve Section Chief A's `RowVersion`, collaborative editing on the same Gate is functionally impossible without external coordination (e.g., calling each other on the phone to say "I'm saving now").

---

## 4. Recommendations for Remediation

To elevate this application from a "Lockout" state to a seamless multi-user experience, the tracking strategy must be modernized.

### **Phase 1: Adopt Delta-Saving (Targeted Modification)**
You must stop setting the entire graph to `EntityState.Modified`. Entity Framework Core has deep, built-in change tracking. 

1. **Stop Detaching/Reattaching:** If possible, keep your entities attached to an active `AppDbContext` during the user's session, or...
2. **Explicit Property Modification:** If you must use disconnected graphs, only mark the *exact object* the user touched as `EntityState.Modified`. For example, in the `GateProgressViewModel`, when a user checks a box, pass *only that specific `ProgressItem`* and its parent `WorkBreakdownItem` to the `DataService` to be marked as modified, leaving the rest of the systems untouched.

### **Phase 2: Granular Concurrency (Field-Level vs. Row-Level)**
Currently, if User A updates a Task's *Name* and User B updates the same Task's *End Date*, they will conflict because `RowVersion` locks the entire row.
* **Fix:** If you rely on EF Core's built-in change tracker (instead of manually forcing `EntityState.Modified`), EF Core will only generate `UPDATE` SQL statements for the columns that actually changed. This highly reduces row-version collisions.

### **Phase 3: Silent Merge / Reloading**
When a concurrency exception `DbUpdateConcurrencyException` occurs, you currently show a MessageBox and force the user to restart.
* **Fix:** You can catch the exception, pull the database's new values (`ex.Entries.Single().GetDatabaseValues()`), silently merge the user's local edits on top of the fresh database values, and re-attempt the save. This creates a "Google Docs" style experience where users rarely notice conflicts occurring in the background.
