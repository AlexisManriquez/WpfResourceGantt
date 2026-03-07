# Project Structure & API Reference

> See also: [[00 - Home]] for the full Map of Content.

This document provides a comprehensive reference of the codebase structure, detailing folders, files, classes, and their key members.

---

## 📂 Root Directory
**Location**: `d:\Project Management Application\Merged\ResourceAllocation\WpfResourceGantt\`

### Key Files
*   **`App.xaml` / `App.xaml.cs`**: Entry point. Sets up the application theme and startup logic. See [[App Overview]].
*   **`MainWindow.xaml` / `MainWindow.xaml.cs`**: The main shell window. Contains the definitions for the `ContentControl` that swaps views.
*   **`StartupWindow.xaml` / `StartupWindow.xaml.cs`**: The initial splash/login screen.
*   **`PdfReportService.cs`**: Service for generating PDF reports. See [[DataService]] for other services.
*   **`GroupSummaryConverter.cs`**: Root-level converter for grouping logic in data grids.
*   **`GroupSummaryMultiConverter.cs`**: Handling multiple bound values for group headers.
*   **`JsonConverter.cs`**: Custom JSON serialization logic.
*   **`RelayCommand.cs`**: (Root version) Basic ICommand implementation.
*   **`WpfResourceGantt.csproj`**: Project definition file.
    *   **COM Stabilization**: Uses direct `<COMReference>` for `Microsoft.Office.Interop.Excel` with `EmbedInteropTypes="True"` to prevent assembly resolution errors during reconstruction.
*   **`appsettings.json`**: Application configuration.

---

## 📂 ProjectManagement (Core Namespace)
**Location**: `ProjectManagement\`

### 📄 Core Classes
*   **`MainViewModel.cs`**: (**Core**) The central orchestrator. Manages `CurrentView`, navigation, and global state. See [[MainViewModel]].
*   **`WorkItem.cs`**: (**Core**) The primary ViewModel wrapper for `WorkBreakdownItem`. Adds `INotifyPropertyChanged` to the data model.
*   **`DataService.cs`**: (**Core**) Handles all I/O operations (saving/loading `ProjectDataFromMpp.json`). See [[DataService]].
*   **`CsvImportService.cs`**: Handles importing hours from CSV/Excel and **Project Reconstruction** (WBS hierarchy generation from external data).
*   **`ProjectManagementModule.cs`**: Dependency injection or module initialization logic.
*   **`RelayCommand.cs`**: (ProjectManagement version) `ICommand` implementation used by ViewModels.
*   **`StringToIsNotEmptyConverter.cs`**: Helper converter in root PM folder.
*   **`ProjectManagementControl.xaml` / `ProjectManagementControl.xaml.cs`**: (**Updated**) The main host for all feature views. Now contains the **Contextual Action Toolbar** which filters buttons based on `IsSystemsVisible`, `IsProjectsVisible`, etc.

### 📂 Models
**Location**: `ProjectManagement\Models`

#### Core Entities
*   **`ProjectData.cs`**: Root JSON object (`ProjectData`) and `SystemItem` (a strict structural container for Level 1 Projects, without execution metrics). See [[ProjectData & SystemItem]] and [[Data Hierarchy]].
*   **`User.cs`**: User entity (`Id`, `Name`, `Role`, `Section`).
*   **`Role.cs`**: Enum definition for user roles (Developer, ProjectManager, etc.).
*   **`WorkItemStatus.cs`**: Enum definition for task statuses (Active, Complete, etc.).
*   **`ResourceAssignment.cs`**: Mapping between `WorkItem` and `User`.

#### Gantt & Visualization Models
*   **`GanttColumn.cs`**: definition for dynamic columns in the Gantt chart.
*   **`ResourceGanttModels.cs`**: Classes specific to the Resource Gantt view (`ResourcePerson`, `ResourceTask`).
*   **`ProgressBlock.cs`**: Represents a checklist block within a task. See [[ProgressBlock & Items]].
*   **`ProgressHistoryItem.cs`**: Represents a snapshot of progress over time (used for EVM graphs).
*   **`AdminTask.cs`**: Represents a quick task/to-do item (used in Developer Portal).

#### 📁 Templates (Model Templates)
**Location**: `ProjectManagement\Models\Templates`
*   **`ProjectTemplate.cs`**: Structure for a full project template. See [[Templates]].
*   **`TemplateGate.cs`**: Structure for a standard Gate (e.g., "Design", "Integration"). Contains both `Tasks` and `Blocks` collections. See [[Templates]].
*   **`TemplateTask.cs`**: (**New**) Schedule-driving leaf task within a gate. Defines `DurationDays`, `WorkHours`, `ItemType` (Leaf/Receipt), and relative `Predecessors`. Gates with Tasks become Summary nodes on apply.
*   **`TemplateProgressBlock.cs`**: Structure for a standard checklist block (e.g., "Design Review").
*   **`TemplateProgressItem.cs`**: Structure for a single checklist item.

### 📂 Infrastructure

#### `ProjectManagement\Converters` — See [[Converters]]
*   **`HierarchyConverters.cs`**: Indentation logic for TreeGridViews.
*   **`HealthToBrushConverter.cs`**: Maps SPI/CPI to Red/Yellow/Green.
*   **`StatusToOpacityConverter.cs`**: Visual cues for completed items.
*   **`BooleanToVisibilityConverter.cs`**, `EnumToBooleanConverter.cs`: Standard UI helpers.
*   **`AutoFitMarginConverter.cs`, `AutoFitWidthConverter.cs`**: Dynamic sizing logic.
*   **`BindingProxy.cs`**: Fix for binding context issues in DataGrids.
*   **`BoolToColumnConverter.cs`, `BoolToColumnSpanConverter.cs`**: Grid layout logic.
*   **`CanvasLeftConverter.cs`**: Positioning logic for Gantt bars.
*   **`DoubleToGridLengthConverter.cs`**, `IndentationConverter.cs`, `LeftMarginConverter.cs`.
*   **`LevelToIndentConverter.cs`**: Visual hierarchy indentation.
*   **`StringEqualityToVisibilityConverter.cs`**: Contextual UI logic for Ribbon tab/group visibility.
*   **`NegativeValueConverter.cs`, `StringSplitConverter.cs`**.
*   **`TaskStatusToBooleanConverter.cs`, `ValueToKiloCurrencyConverter.cs`**.
*   **`WorkItemStateConverter.cs`, `WorkItemStatusConverter.cs`**.

#### `ProjectManagement\Services`
*   **`TemplateService.cs`**: Manages applying WBS templates to new projects. Supports dual-path conversion (logic-driven tasks + flat checklists). See [[TemplateService]].
*   **`ScheduleCalculationService.cs`**: (**New**) CPM schedule engine — forward pass, backward pass, total float, and critical path identification per GAO-16-89G. See [[ScheduleCalculationService]].
*   **`PredecessorParser.cs`**: (**New**) Parses predecessor strings (e.g., `"1.1.1.1, 1.1.1.2FS+5d"`) into `DependencyInfo` objects with type and lag. Used by both the schedule engine and dependency arrow rendering.
*   **`MppImportService.cs`**: Logic for parsing MS Project XML exports. See [[MppImportExport]].
*   **`MppExportService.cs`**: Handles exporting Systems to MS Project (.mpp).
	*  **DoD Alignment**: Configures `PhysicalPercentComplete` and disables automatic cost calculations to ensure SV/CV values match the app's internal EVM engine.
	*  **Resource Management**: Maps assignments and handles `FixedCost` injection for unassigned tasks.

#### `ProjectManagement\Adorners`
*   **`DragAdorners.cs`**: Visual support for drag-and-drop operations.

#### `ProjectManagement\UI`
*   **`Styles\CoreStyles.xaml`**: Global resource dictionary (Colors, Buttons, Text). See [[UI & Styles]].
*   **`Styles\Icons.xaml`**: SVG path data for application icons.
*   **`Views\TacticalRibbonView.xaml`**: (**Updated**) Primary navigation shell. Handles high-level view switching logic and ribbon expansion states. See [[Navigation & Ribbons]].
---

## 📂 ProjectManagement/Features (Modules)

### 1. 📁 Gantt (The Main Project View) — See [[Gantt View]]
**Files**: `GanttViewModel.cs`, `GanttView.xaml`, `GanttView.xaml.cs`
*   **`GanttViewModel`**: Manages the hierarchical Gantt chart.
    *   `WorkItems`: Flattened list with indentation.
    *   `FilterRecursive()`: Logic for filtering tree structures.
    *   **Sandbox Mode**: Supports an injected `simulatedData` collection to allow disconnected "What-If" visualization.

*   **`AnimatedGanttBar.xaml` / `.cs`**: Custom UserControl for rendering Gantt bars. Handles task bars (blue/red), summary bars (health-colored with EVM labels), and baseline ghost bars.
*   **`DependencyArrowCanvas.cs`**: (**New**) Custom Canvas overlay that draws elbow-style FS dependency arrows between predecessor and successor bars. Uses `StreamGeometry` via `OnRender` for performance. Arrows are slate gray (normal) or red (critical path). Refreshes on scroll, expand/collapse, and resize with 50ms debounce.

### 2. 📁 Dashboard
**Files**: `DashboardViewModel.cs`, `DashboardView.xaml`
*   **`DashboardViewModel`**: High-level overview.
    *   `TotalProjects`, `RedProjectsCount`: Aggregated KPIs.


### 4. 📁 DeveloperPortal
**Files**: `DeveloperPortalViewModel.cs`, `DeveloperPortalView.xaml`
*   **`DeveloperPortalViewModel`**: Personal dashboard.
    *   `AssignmentGroups`: Tasks grouped by status/priority.
    *   `QuickTasks`: List of `AdminTask`.

### 5. 📁 Analytics
**Files**: `AnalyticsViewModel.cs`, `AnalyticsView.xaml`
*   **`AnalyticsViewModel`**: Resource analysis.
    *   `Resources`: List of users and utilization.
    *   `UnassignedTasks`: "Bench" report.

### 6. 📁 ResourceGantt (Capacity Planning)
**Files**: `ResourceGanttViewModel.cs`, `ResourceGanttView.xaml`
*   **`ResourceGanttViewModel`**: Timeline view of User availability.
    *   `SectionStatistics`: Availability counts per section.

### 📁 EVM (Earned Value Management) — See [[EVM]] and [[EVM Calculation Rules]]
**Files**: `EVMViewModel.cs`, `EVMView.xaml`
*   **`EVMViewModel`**: S-Curve charts and financial metrics.
    *   `ChartSeries`: Data points for LiveCharts S-Curve.

### 8. 📁 SystemManagement (Admin) — See [[System Management]]
**Files**: `SystemManagementViewModel.cs`, `SystemManagementView.xaml`
*   **`SystemManagementViewModel`**: CRUD for Systems.
    *   `HierarchicalSystems`: Tree of Systems/Projects.
    *   `HierarchyItemViewModel` (if present): Wrapper for system tree items.
		*   **SelectedVM**: Tracks the active row for Ribbon-based actions.
    *   **Ribbon Commands**: AddChild, ApplyTemplate, EditDetails, and DeleteCommand are exposed for the Edit Tab.
### 9. 📁 UserManagement (Admin)
**Files**: `UserManagementViewModel.cs`, `UserManagementView.xaml`, `CreateUserDialog.xaml`, `CreateUserDialogViewModel.cs`.
*   **`UserManagementViewModel`**: CRUD for Users.

### 10. 📁 ProjectCreation
**Files**: `CreateProjectViewModel.cs`, `CreateSubProjectViewModel.cs`, `CreateWorkItemViewModel.cs`
*   **`CreateWorkItemViewModel`**: Generic form for adding nodes.

### 11. 📁 QuickTasks
**Files**: `QuickTasksViewModel.cs`, `QuickTasksView.xaml`
*   **`QuickTasksViewModel`**: Standalone view for ad-hoc tasks.

### 12. 📁 ApplyTemplate
**Files**: `ApplyTemplateDialogView.xaml`, `ApplyTemplateDialogViewModel.cs`
*   **`ApplyTemplateDialogViewModel`**: Logic for selecting and applying templates.

### 13. 📁 AssignDeveloper
**Files**: `AssignDeveloperDialog.xaml`, `AssignDeveloperViewModel.cs`
*   **`AssignDeveloperViewModel`**: Modal logic for assigning users to tasks.

### 14. 📁 Simulation (Temporal Sandbox) — See [[Temporal Sandbox]]
**Files**: `SimulationViewModel.cs`, `SimulationView.xaml`, `InteractiveManipulatorGraph.xaml`, `SimulationDataPoint.cs`, `CloneHelper.cs`
*   **`SimulationViewModel`**: Manages the sandbox life-cycle, including project cloning and time-travel recalculations.
*   **`InteractiveManipulatorGraph`**: High-performance custom control for visual timeline manipulation of progress and hours.
*   **`CloneHelper`**: Specialized utility for deep-cloning complex task hierarchies while maintaining parent-child links.

### 15. 📁 Dialogs
**Location**: `ProjectManagement\Features\Dialogs`
*   Contains shared dialogs for system export, block imports, and project reconstruction.
*   **Files**: `ExportSystemDialogView.xaml`, `ExportSystemDialogViewModel.cs`, `ImportTestBlocksDialog.xaml`, `ImportTestBlocksDialogViewModel.cs`, `ReconstructProjectDialogView.xaml`, `ReconstructProjectDialogViewModel.cs`.
