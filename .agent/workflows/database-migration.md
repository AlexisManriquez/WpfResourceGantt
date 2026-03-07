---
description: Adding or modifying data entities and updating the database.
---

# Workflow: EF Core Database Migration

> **Goal:** To modify existing data entities or add new ones and safely propagate those changes to the database schema.
> **Orchestration Type:** Data Schema Update (Led by `dotnet-architect`)

---

## 🗄️ Migration Architecture
- **Entities:** Located in `src\MyApp.Core\Entities`.
- **DbContext:** Located in `src\MyApp.Infrastructure\Data`.
- **Migration History:** Stored in `src\MyApp.Infrastructure\Migrations`.
- **Startup Project:** The WPF app (`src\MyApp.Wpf`) provides the configuration (Connection String).

---

## 🛠️ Step-by-Step Execution

### Phase 1: Entity & Context Update (`dotnet-architect`)
**Goal:** Define the desired state of the data.
1.  **Modify Entities:** Add properties or new classes to the `Core` project.
2.  **Update DbContext:** Add `DbSet<T>` properties or update `OnModelCreating` (Fluent API) in the `Infrastructure` project.
3.  **Nullable Check:** Ensure properties match the database's `NULL` / `NOT NULL` requirements.

### Phase 2: Environment Validation (`visual-studio-mastery`)
**Goal:** Ensure the system is ready to generate code.
1.  **Check EF Tool:** Verify the `dotnet-ef` global tool is installed.
    *   *PowerShell:* `dotnet ef --version` (If missing: `dotnet tool install --global dotnet-ef`)
2.  **Verify Design Package:** Ensure `Microsoft.EntityFrameworkCore.Design` is installed in the WPF project.
    *   *PowerShell:* `Select-String -Path "src\**\*.csproj" -Pattern "EntityFrameworkCore.Design"`

### Phase 3: Migration Generation (`dotnet-architect`)
**Goal:** Create the C# code that represents the schema delta.
1.  **Generate Migration:**
    *   *PowerShell:* 
      ```powershell
      dotnet ef migrations add [MigrationName] `
             --project src\MyApp.Infrastructure `
             --startup-project src\MyApp.Wpf `
             --output-dir Data\Migrations
      ```
2.  **Review Code:** Check the generated `Up()` and `Down()` methods for potential data-loss warnings.

### Phase 4: Database Update (`dotnet-architect` + `dotnet-debugger`)
**Goal:** Apply the changes to the physical database.
1.  **Apply Migration:**
    *   *PowerShell:*
      ```powershell
      dotnet ef database update `
             --project src\MyApp.Infrastructure `
             --startup-project src\MyApp.Wpf
      ```
2.  **Scripting (Alternative):** If working on a production-bound change, generate the SQL instead:
    *   *PowerShell:* `dotnet ef migrations script --project src\MyApp.Infrastructure`

### Phase 5: Repository & Test Sync (`desktop-tester`)
**Goal:** Ensure the rest of the application can use the new schema.
1.  **Update Repositories:** Update `Infrastructure\Repositories` to handle new fields.
2.  **Verify Data Flow:** Run `dotnet test` to ensure existing logic still works with the new schema.
3.  **Seed Data:** If new tables were added, update the `DbInitializer` or seed logic.

---

## 🛑 Migration Safety Rules

| Rule | Description | Rationale |
|------|-------------|-----------|
| **Inward Entities** | Entities must stay in the `Core` project. | Prevents Infrastructure leaks into Logic. |
| **No Manual SQL** | Never modify the DB schema via SSMS/SQLite Studio. | The Migrations folder must be the source of truth. |
| **Snapshots** | Always commit the `ModelSnapshot.cs` file. | EF Core uses this to calculate the next delta. |
| **Rollback Plan** | Test `Down()` logic if the migration is complex. | Ensures you can revert if the deployment fails. |

---

## 🔧 PowerShell Data Toolbox

### List all Migrations
```powershell
# See the history of schema changes
dotnet ef migrations list --project src\MyApp.Infrastructure --startup-project src\MyApp.Wpf
Remove the last Migration (Unapplied only)

# Use this if you made a typo in the migration you just created
dotnet ef migrations remove --project src\MyApp.Infrastructure --startup-project src\MyApp.Wpf
Drop and Recreate (Dev Only)

# Nuclear option for local development when schema is heavily corrupted
dotnet ef database drop --force --project src\MyApp.Infrastructure --startup-project src\MyApp.Wpf
dotnet ef database update --project src\MyApp.Infrastructure --startup-project src\MyApp.Wpf
🆘 Troubleshooting Migrations

Error: "More than one DbContext was found": Explicitly add --context AppDbContext to the PowerShell commands.

Error: "Startup project doesn't reference Microsoft.EntityFrameworkCore.Design": Add the NuGet package to the WPF project, not just the Infrastructure project.

Data Truncation Error: If shrinking a column size, EF will warn you. You must handle existing data via a MigrationBuilder.Sql() call in the Up() method before the column is altered.
