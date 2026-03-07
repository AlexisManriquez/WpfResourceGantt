# WpfResourceGantt - App Breakdown

> See also: [[00 - Home]] for the full Map of Content.

## 1. High-Level Overview
**WpfResourceGantt** is a Desktop Project Management application built with **.NET 8 (WPF)** using the **MVVM (Model-View-ViewModel)** pattern. It utilizes **Entity Framework Core** for data persistence (targeting SQL Server Express).

The application manages complex Work Breakdown Structures (WBS), tracks resource assignments, calculates Earned Value Management (EVM) metrics, and now supports **Standardized Project Templates** to rapidly deploy complex gate/checklist structures.

---

## 2. Core Architecture
- **Entry Point**: `App.xaml` -> `MainWindow.xaml` -> `MainViewModel.cs` — See [[App Overview]]
- **Data Layer**: 
    - `DataService.cs`: Central repository for Live Project Data (WorkItems, Users, Systems). See [[DataService]].
    - `TemplateService.cs`: Manages "Blueprint" data (Templates) and handles the logic of converting Templates into Live WorkItems. See [[TemplateService]].
- **Navigation**: Controlled by the **Tactical Ribbon**, switching `CurrentViewModel`. See [[Navigation & Ribbons]].
- **Dialog System**: A generic Modal Overlay system in `MainWindow.xaml` managed by `MainViewModel.CurrentDialogViewModel`. See [[MainViewModel]].

---

## 3. Data Hierarchy & Structure — See [[Data Hierarchy]]
The data model uses a deeply nested, recursive strategy to support complex project trees.

### A. The WBS Hierarchy (System to Task)
The application supports an infinite nesting of work items, typically following this "Standard" hierarchy:

1.  **System** (`SystemItem`): The root container (e.g., "Weapon System X").
    *   *Properties*: Strictly a structural/categorical container. Contains identity data only (`Id`, `WbsValue`, `Name`, `Status`, `ProjectManagerId`). Execution metrics (Dates, Work, EVM) execute exclusively from Level 1 Projects downwards.
2.  **Project** (`WorkBreakdownItem`): Level 1 child of a System.
3.  **Sub-Project** (`WorkBreakdownItem`): Level 2 child.
4.  **Gate** (`WorkBreakdownItem`): Level 3 child (Milestones or Phase Gates).
5.  **Task / Sub-Task** (`WorkBreakdownItem`): Level 4+ children.
    *   **Recursive**: A Task can have its own children (Sub-Tasks), creating a deep tree.
    *   **Leaf Node**: The lowest level `WorkBreakdownItem` that has *no children* is considered a "Leaf".

### B. Execution Detail Hierarchy (Inside a Leaf Task)
When a user opens a Leaf Task, they find a granular checklist structure used for reporting progress:

1.  **Task (Leaf WorkItem)**: The container.
2.  **Progress Block** (`ProgressBlock`): A logical grouping of checklist items (e.g., "Design Phase", "Code Review").
3.  **Progress Item / Sub-Task** (`ProgressItem`): The individual checkbox item (e.g., "Create Interface", "Write Unit Tests").
    *   *Action*: Users mark these as `IsCompleted`.
    *   *Impact*: Completing these items automatically calculates the `% Progress` of the parent Task.

### C. The Template Hierarchy (Blueprints) — See [[Templates]]
Stored separately from live data to allow reuse without pollution.
1.  **ProjectTemplate**: The root definition (e.g., "Standard DoD Lifecycle").
2.  **TemplateGate**: Mirrors Level 3 Gates.
3.  **TemplateProgressBlock**: Mirrors Progress Blocks.
4.  **TemplateProgressItem**: Mirrors Checklist Items.

### Entity Relationships
*   **SystemItem** (1) --> (Many) **WorkBreakdownItem** (Children)
*   **WorkBreakdownItem** (1) --> (Many) **WorkBreakdownItem** (Children - *Recursive*)
*   **WorkBreakdownItem** (1) --> (Many) **ProgressBlock**
*   **ProgressBlock** (1) --> (Many) **ProgressItem**

---

## 4. User Interface: The Dual-Ribbon System — See [[Navigation & Ribbons]]
 The application uses a two-tier command structure to separate high-level navigation from view-specific actions.

### A. Primary Navigation Ribbon (`TacticalRibbonView.xaml`)
Located at the very top, this ribbon is dedicated to switching the global application state.
*   **Primary Navigation Group**: DASHBOARD, PROJECTS, RESOURCE GANTT, ANALYTICS, EVM.
*   **Administration Group**: USERS, SYSTEMS.
*   **Features**: Supports ribbon collapsing (toggled via chevron) to maximize workspace.

### B. Contextual Action Toolbar (`ProjectManagementControl.xaml`)
Located immediately below the Navigation Ribbon, this toolbar updates dynamically based on the `CurrentView`.

*   **SYSTEMS Context**:
    *   **Editing Group**: Add System, Add Item, Apply Template, Gate Progress, Delete Item.
*   **PROJECTS Context**:
    *   **Display Group**: Expand All, Collapse All, Zoom controls.
*   **USERS Context**:
    *   **Users Group**: Add User, Edit User, Delete User.
*   **GLOBAL Context (DATA TOOLS)**:
    *   **Data Group**: MPP Import/Export, Import Hours, Export Excel.
    *   **Sync**: Global Refresh button.

---

## 5. Views & ViewModels (ProjectManagement/Features)

| Feature | ViewModel | Description |
| :--- | :--- | :--- |
| **Projects (Gantt)** | `GanttViewModel.cs` | **Core Editing View**. Shows the WBS hierarchy (System -> -> Task). Triggers the **Gate Progress View** via the launch icon on Sub-Projects. |
| **System Management** | `SystemManagementViewModel.cs` | **Top-Level CRUD**. Manages the root `SystemItem` entries (e.g. creating new Weapon Systems). |
| **Template Dialog** | `ApplyTemplateDialogViewModel.cs` | `ApplyTemplateDialogView.xaml` | Popup for selecting a template and choosing "Overwrite" vs "Append". |
| **Gate Progress View** | `GateProgressViewModel.cs` | **Detailed Dashboard**. Displays a specific Sub-Project's Gates (Level 3) and their underlying Test/Progress Blocks. Uses a recursive lookup to find all leaf tasks under a gate. |
| **Resource Gantt** | `ResourceGanttViewModel.cs` | Renders tasks grouped by User. Used to see who is overloaded. |

| **Developer Portal** | `DeveloperPortalViewModel.cs` | **Role-Specific**. A simplified dashboard for Developers to see only their assigned Tasks and Quick Tasks. |
| **Analytics** | `AnalyticsViewModel.cs` | Visualizes Project Health using `LiveCharts`. |
| **Assign Developer** | `AssignDeveloperViewModel.cs` | **Dialog View**. Allows a Manager to assign a Developer to a specific Task/Leaf item. |
| **Dialogs** | `ExportSystemDialogViewModel.cs`, `ImportBlocksViewModel.cs` | **Utility Module**. Contains shared dialogs for system export and block imports. |

---

## 6. Key Dependencies & Code Logic — See [[Data Lifecycle]]

### A. View Dependency & Orchestration Maps
The application uses a centralized orchestration pattern in `MainViewModel.cs`. Each view follows a predictable "Trigger -> Logic -> Orchestration -> Presentation" flow.

#### 1. Projects (Gantt) View
The core WBS editing interface where managers build the project tree.
*   **Trigger**: `TacticalRibbonView.xaml` -> `PROJECTS` Button.
*   **Logic**: `MainViewModel.ShowProjectManagementCommand`.
*   **Orchestration**: `MainViewModel.ShowProjectGanttView()`
    *   Sets `CurrentView = "Projects"`.
    *   Initializes/Refreshes `GanttViewModel.cs`.
*   **Presentation**: `GanttView.xaml` (hosted via `DataTemplate` in `ProjectManagementControl.xaml`).
*   **Key Files**: `GanttViewModel.cs` (logic), `GanttView.xaml.cs` (drag-drop/scroll sync).

#### 2. Dashboard Overview
Visual health cards and drill-down navigation for all active systems.
*   **Trigger**: `TacticalRibbonView.xaml` -> `DASHBOARD` Button.
*   **Logic**: `MainViewModel.ShowDashboardCommand`.
*   **Orchestration**: `MainViewModel.ShowDashboardView()`
    *   Sets `CurrentView = "Dashboard"`.
    *   Initializes `DashboardViewModel.cs`.
*   **Presentation**: `DashboardView.xaml`.
*   **Key Files**: `DashboardViewModel.cs` (graphing logic), `DashboardView.xaml.cs` (card layout resizing).

#### 3. Resource Gantt (Capacity Planning)
A person-centric view used to identify resource overloads and availability.
*   **Trigger**: `TacticalRibbonView.xaml` -> `RESOURCE GANTT` Button.
*   **Logic**: `MainViewModel.ShowGanttCommand`.
*   **Orchestration**: Sets `CurrentView = "Resource Gantt"`.
    *   Note: Uses a persistent `GanttContext` (ResourceGanttViewModel) on `MainViewModel` to preserve filters.
*   **Presentation**: `ResourceGanttView.xaml` (Visible when `IsGanttVisible` is true in `MainWindow.xaml`).
*   **Key Files**: `ResourceGanttViewModel.cs` (filtering/timeline generation).


#### 5. Gate Progress View (Drill-Down)
Detailed status of a Sub-Project's gates and test blocks.
*   **Trigger**: Launch icon button on Level 2 (Sub-Project) rows in `GanttView.xaml`.
*   **Logic**: `GanttViewModel.OpenGateProgressCommand`.
*   **Orchestration**: `MainViewModel.GoToGateProgress(WorkItem)`.
*   **Presentation**: `GateProgressView.xaml`.
*   **Key Files**: `GateProgressViewModel.cs` (Recursive data loading from WBS tree).

#### 6. Developer Portal (Role-Specific)
A simplified, task-focused view for end-users assigned to work.
*   **Trigger**: Automatic upon login if `User.Role == Developer`.
*   **Logic**: `MainViewModel.InitializeAsync` check.
*   **Orchestration**: Sets `IsDeveloperPortalVisible = true` and `CurrentView = "DeveloperPortal"`.
*   **Presentation**: `DeveloperPortalView.xaml`.
*   **Key Files**: `DeveloperPortalViewModel.cs` (Filters data to show only assigned tasks).

#### 7. Analytics Dashboard
Live charts showing project burn-down and resource utilization.
*   **Trigger**: `TacticalRibbonView.xaml` -> `ANALYTICS` Button.
*   **Logic**: `MainViewModel.ShowAnalyticsCommand`.
*   **Orchestration**: Sets `CurrentView = "Analytics"`.
*   **Presentation**: `AnalyticsView.xaml`.
*   **Key Files**: `AnalyticsViewModel.cs` (Uses `LiveCharts` for rendering).

#### 8. System Management
CRUD operations for the hierarchical system structure.
*   **Trigger**: `TacticalRibbonView.xaml` -> `SYSTEMS` Button.
*   **Logic**: `MainViewModel.ShowSystemsCommand`.
*   **Orchestration**: `MainViewModel.ShowSystemManagementView()`
    *   Sets `CurrentView = "Systems"`.
    *   Initializes `SystemManagementViewModel.cs`.
*   **Presentation**: `SystemManagementView.xaml`.

#### 9. User Management
Administration of users, roles, and section assignments.
*   **Trigger**: `TacticalRibbonView.xaml` -> `USERS` Button.
*   **Logic**: `MainViewModel.ShowUsersCommand`.
*   **Orchestration**: `MainViewModel.ShowUserManagementView()`
    *   Sets `CurrentView = "Users"`.
    *   Initializes `UserManagementViewModel.cs`.
*   **Presentation**: `UserManagementView.xaml`.

#### 10. EVM (Earned Value Management)
Financial performance metrics (BCWS, BCWP, ACWP, CPI, SPI).
*   **Trigger**: `TacticalRibbonView.xaml` -> `EVM` Button.
*   **Logic**: `MainViewModel.ShowEVMCommand`.
*   **Orchestration**: `MainViewModel.ShowEVMView()`
    *   Sets `CurrentView = "EVM"`.
    *   Initializes `EVMViewModel.cs`.
*   **Presentation**: `EVMView.xaml`.

#### 11. Quick Tasks
A flat list of tasks for rapid entry and status tracking.
*   **Trigger**: `TacticalRibbonView.xaml` -> `QUICK TASKS` Button.
*   **Logic**: `MainViewModel.ShowQuickTasksCommand`.
*   **Orchestration**: `MainViewModel.ShowQuickTasksView()`
    *   Sets `CurrentView = "QuickTasks"`.
    *   Initializes `QuickTasksViewModel.cs`.
*   **Presentation**: `QuickTasksView.xaml`.

#### 12. Apply Template Flow
*   **Trigger**: User clicks `📋` on a Sub-Project row (`SystemHierarchyItemViewModel`).
*   **Command**: `ApplyTemplateCommand` fires `SystemManagementViewModel.HandleApplyTemplate`.
*   **Logic**:
    1.  `SystemManagementViewModel` calls `TemplateService.GetAllTemplatesAsync()`.
    2.  VM creates `ApplyTemplateDialogViewModel`.
    3.  VM calls `MainViewModel.ShowModalCustomDialog()`.
*   **UI**: `MainWindow` renders the Overlay and the Dialog View.
*   **Execution**:
    1.  User selects Template + Overwrite/Append -> Clicks Confirm.
    2.  `TemplateService.ApplyTemplateAsync` is called.
    3.  **ID Generation**: Service generates IDs using `ParentID|Sequence` format.
    4.  **WBS Generation**: Service calls `DataService.RegenerateWbsValues` to ensure `NOT NULL` DB constraints are met.
    5.  `DataService.SaveDataAsync` persists to SQL.
*   **Refresh**: The specific VM row clears its `Children` collection and re-maps the new data from the DB.

### B. Critical Infrastructure Files
*   **`ProjectManagement/DataService.cs`**:
    *   **Heavily Used By**: ALL ViewModels.
    *   **Key Logic**: `SanitizeChildrenIds` (ensures unique IDs for the recursive WBS), `SaveDataAsync` (calculates progress based on completed `ProgressItems`).
*   **`ProjectManagement/Models/WorkBreakdownItem.cs`**:
    *   Contains the recursive `Children` list.
    *   Contains `RecalculateRollup()`: Logic that bubbles up dates, costs, and progress from Sub-Tasks -> Tasks -> Gates -> Systems.
*   **`ProjectManagement/MainViewModel.cs`**:
    *   Orchestrates the View Switching (bound to Ribbon commands).
    *   Holds `CurrentUser` state (controlling "Developer Mode" vs "Manager Mode").
*   **`ProjectManagement/Services/TemplateService.cs`**:
    *   **Responsibility**: Bridging the gap between `ProjectTemplate` entities and `WorkBreakdownItem` entities.
    *   **Key Method**: `ApplyTemplateAsync` (Maps Gates -> Blocks -> Items and handles ID generation).
* **`ProjectManagement/Services/MppExportService.cs`**:
	* **Responsibility**: Translates the app's WBS into a native Microsoft Project file.
	* **EVM Logic**: Uses "Physical % Complete" to decouple performance progress (Checklists) from labor burn-rate (Hours).
	* **Precision**: Forces MS Project to use a 5-day, 8-hour Government calendar to ensure Schedule Variance (SV) alignment.
	* **Source of Truth**: The app uses a **Work-Weighted Rollup** for progress, which is more accurate than MS Project's default duration-based rollup. 
*   **`ProjectManagement/Models/Templates/`** :
    *   Contains the 4 schema definitions (`ProjectTemplate.cs`, `TemplateGate.cs`, etc.).

### C. Feature Deep Dive: Project Creation

#### 1. Project Creation (Folder: `ProjectManagement/Features/ProjectCreation`)
This module handles the complex logic of creating and editing hierarchical Work Breakdown Structures (WBS) before they are saved to the database.
*   **Purpose**: Provides a recursive, temporary working state for building trees (System -> Project -> Sub-Project -> Task) without committing to the DB immediately.
*   **Key Files**:
    *   **`CreateWorkItemViewModel.cs`**: The powerhouse of this module. It is a **recursive ViewModel** that wraps a `WorkBreakdownItem`. It allows users to build infinite levels of children (Tasks within Tasks) in memory. It handles:
        *   Recursive status cascading (e.g., setting a System to "Active" updates all children).
        *   Name parsing (splitting "SYS-001 Project Alpha" into Number: "001" and Name: "Project Alpha").
        *   Dynamic UI logic (hiding "Number" fields for lower-level tasks).
    *   **`CreateProjectViewModel.cs`**: A specialized wrapper often used for the top-level "Add Project" flow.
    *   **`CreateSubProjectViewModel.cs`** & **`CreateTaskViewModel.cs`**: Helper classes for specific levels, though `CreateWorkItemViewModel` is the primary recursive driver.



---

## 7. Common Modification Scenarios — See [[Common Modifications]]
*   **Adding a new Ribbon Button**:
    1.  Add command to `MainViewModel.cs`.
    2.  Add Button to `TacticalRibbonView.xaml`.
*   **Changing Progress Calculation**:
    1.  Modify `DeveloperPortalViewModel.ToggleBlockItem` (for quick updates).
    2.  Modify `DataService.SaveDataAsync` (deep recalculation).
    3.  Modify `WorkItem.RecalculateRollup` (for bubbling up the WBS).
