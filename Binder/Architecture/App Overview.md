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
2. `StartupWindow` handles login/user selection
3. `MainWindow` hosts a `ContentControl` bound to `MainViewModel.CurrentViewModel`
4. `MainViewModel.InitializeAsync()` loads all data via [[DataService]] and sets the initial view

## Core Architectural Rules
1. **No logic in code-behind** — all UI logic lives in ViewModels
2. **View switching** is controlled by `MainViewModel.CurrentView` string property, bound via `DataTemplate` in `ProjectManagementControl.xaml`
3. **Data persistence** uses an in-memory model (`_projectData`) synced to SQL via EF Core's `TrackGraph` on save — see [[DataService]]
4. **Debounced saves** — `SaveDataAsync()` uses a 300ms `CancellationTokenSource` debounce to coalesce rapid edits

## Key Source Files
| File | Role |
|------|------|
| `App.xaml` / `App.xaml.cs` | Theme loading, startup |
| `MainWindow.xaml` | Shell with `ContentControl` + modal overlay |
| `StartupWindow.xaml` | Login/user selection |
| [[MainViewModel]] (`MainViewModel.cs`) | Central orchestrator |
| [[DataService]] (`DataService.cs`) | All data I/O |
| `ProjectManagementControl.xaml` | Feature view host + contextual toolbar |
| `appsettings.json` | DB connection string config |

## Related Pages
- [[Data Hierarchy]] — the recursive WBS model
- [[Navigation & Ribbons]] — how views are switched
- [[MainViewModel]] — detailed orchestration reference
