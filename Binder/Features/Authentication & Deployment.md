---
tags: [feature, architecture]
purpose: AI reference for the deployment toggle, Windows auto-login system, and authentication flow.
---

# Authentication & Deployment

**Files**: `ProjectManagement/AppSettings.cs`, `ProjectManagement/ViewModels/StartupViewModel.cs`, `StartupWindow.xaml`, `StartupWindow.xaml.cs`

## Deployment Toggle

**File**: `ProjectManagement/AppSettings.cs`

A compile-time constant controls the login behavior:

```csharp
public static class AppSettings
{
    // TRUE  → Production: Auto-login via Windows identity (no combobox)
    // FALSE → Development: Show user selection combobox
    public const bool UseWindowsAuthentication = false;
}
```

This is the **only toggle** that needs to change between dev and production builds.

## Login Modes

### Dev Mode (`UseWindowsAuthentication = false`)
1. `StartupWindow` shows user selection combobox
2. Users displayed: all except `Administrator` role
3. User picks profile → clicks "ACCESS DASHBOARD"
4. `OnLoginSuccess` fires → passes `User` to `App.xaml.cs`

### Production Mode (`UseWindowsAuthentication = true`)
1. `StartupWindow` hides combobox, shows auto-login status panel
2. `StartupViewModel.AttemptAutoLogin()` is triggered automatically
3. Windows identity is read via `WindowsIdentity.GetCurrent().Name`
4. Name is normalized and fuzzy-matched against database (see below)
5. If matched → auto-login after 800ms greeting
6. If not matched → shows message: "Please ask your Section Chief or Flight Chief to add you as a user."

## Name Matching Algorithm

The system handles multiple name formats from Windows/Active Directory:

| Windows Identity | Database Name | Match? |
|---|---|---|
| `AFNET\john.doe` | `John Doe` | ✅ |
| `AFNET\doe.john` | `John Doe` | ✅ |
| `john.doe` | `Doe, John` | ✅ |

**Steps:**
1. **Strip domain prefix**: `AFNET\john.doe` → `john.doe`
2. **Replace dots/underscores**: `john.doe` → `john doe`
3. **Normalize**: remove commas, collapse spaces, lowercase
4. **Match**: try direct match, then reversed-order match

Methods: `GetWindowsDisplayName()`, `FindMatchingUser()`, `NormalizeName()`, `ReverseNameParts()`

## StartupWindow UI Structure

```
┌──────────────────────────────────────────────┐
│  LEFT: Branding    │  RIGHT: Login Form       │
│  - Logo            │                          │
│  - "AM PROJECT"    │  "Welcome Back"          │
│  - "SUITE"         │                          │
│  - Version         │  [Manual Mode]:          │
│                    │    ComboBox + Button      │
│                    │                          │
│                    │  [Auto Mode]:            │
│                    │    Status text            │
│                    │                          │
│                    │  Loading indicator        │
└──────────────────────────────────────────────┘
```

Visibility is controlled by `IsManualLoginMode` / `IsAutoLoginMode` bound to `BoolToVis` converter.

## Entry Point Flow (App.xaml.cs)

```
App.OnStartup()
  ├── Set culture (MM/dd/yyyy)
  ├── Seed templates
  ├── Show StartupWindow as dialog
  │     ├── [Dev]  → User selects from combobox
  │     └── [Prod] → Auto-login via Windows identity
  ├── If login success:
  │     ├── Create MainWindow(loggedInUser)
  │     └── Show MainWindow
  └── If login failed/cancelled:
        └── Application.Shutdown()
```

## Related Pages
- [[User & Role]] — role enum, access control, Administrator role
- [[User Management]] — CRUD UI (hides Administrators)
- [[App Overview]] — tech stack and entry point
- [[DataService]] — `LoadDataAsync()` called by `StartupViewModel`
