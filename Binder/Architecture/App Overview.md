---
tags: [architecture]
purpose: AI-optimized overview of the app's entry point, tech stack, and core patterns.
---

# App Overview

## Tech Stack
| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 8, WPF |
| **Pattern** | MVVM (manual `INotifyPropertyChanged` via `ViewModelBase`) |
| **ORM** | Entity Framework Core (SQL Server Express via `AppDbContext`) |
| **Charts** | LiveCharts (WPF) |
| **DI** | Manual constructor injection (no container) |

## Entry Point Flow
```
App.xaml → StartupWindow.xaml → MainWindow.xaml → MainViewModel.cs
```
1. `App.xaml` loads global `ResourceDictionary` themes
2. `StartupWindow` handles login/user selection (see [[Authentication & Deployment]])
   - **Dev mode**: Combobox user selection
   - **Production mode**: Auto-login via Windows identity
3. `MainWindow` hosts a `ContentControl` bound to `MainViewModel.CurrentViewModel`
4. `MainViewModel.InitializeAsync()` loads all data via [[DataService]] and sets the initial view

## Deployment Toggle
**File**: `ProjectManagement/AppSettings.cs`

A single compile-time constant controls login behavior:
- `UseWindowsAuthentication = false` → Dev mode (combobox)
- `UseWindowsAuthentication = true` → Production mode (Windows auto-login)

See [[Authentication & Deployment]] for full details.

## Core Architectural Rules
1. **No logic in code-behind** — all UI logic lives in ViewModels
2. **View switching** is controlled by `MainViewModel.CurrentView` string property, bound via `DataTemplate` in `ProjectManagementControl.xaml`
3. **Data persistence** uses an in-memory model (`_projectData`) synced to SQL via EF Core's `TrackGraph` on save — see [[DataService]]
4. **Debounced saves** — `SaveDataAsync()` uses a 300ms `CancellationTokenSource` debounce to coalesce rapid edits
5. **Role-based access control** — Systems are containers; PM visibility is project-level; see [[User & Role]]
6. **Administrator role** — hidden superuser with full access, invisible in all UI lists

## Key Source Files
| File | Role |
|------|------|
| `App.xaml` / `App.xaml.cs` | Theme loading, startup, deployment mode routing |
| `MainWindow.xaml` | Shell with `ContentControl` + modal overlay |
| `StartupWindow.xaml` | Login/user selection (dual-mode) |
| `AppSettings.cs` | Deployment toggle (`UseWindowsAuthentication`) |
| [[MainViewModel]] (`MainViewModel.cs`) | Central orchestrator |
| [[DataService]] (`DataService.cs`) | All data I/O |
| `ProjectManagementControl.xaml` | Feature view host + contextual toolbar |
| `appsettings.json` | DB connection string config |

## Related Pages
- [[Data Hierarchy]] — the recursive WBS model
- [[Navigation & Ribbons]] — how views are switched
- [[MainViewModel]] — detailed orchestration reference
- [[Authentication & Deployment]] — login flow and Windows auto-login
- [[User & Role]] — role-based access control
