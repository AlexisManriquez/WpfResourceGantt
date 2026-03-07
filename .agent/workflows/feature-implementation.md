---
description: The standard flow for adding new functionality.
---

# Workflow: Vertical Slice Feature Implementation

> **Goal:** To implement a new feature (e.g., a screen, a data-entry flow, or an API integration) across all architecture layers using MVVM.
> **Orchestration Type:** Sequential Multi-Agent

---

## 🏗️ Workflow Diagram
```mermaid
graph TD
    A[Socratic Gate: Requirements Check] --> B[Phase 1: Domain Modeling]
    B --> C[Phase 2: Service & Logic]
    C --> D[Phase 3: ViewModel & State]
    D --> E[Phase 4: XAML UI]
    E --> F[Phase 5: Verification & Tests]
    F --> G{Success?}
    G -- No --> H[dotnet-debugger Agent]
    H --> C
    G -- Yes --> I[Final Format & Build]
🛠️ Step-by-Step Execution
Phase 1: Domain & Contracts (dotnet-architect)

Goal: Define the data structure and the rules of engagement.

Define Entity: Create the POCO class in src\MyApp.Core\Entities.

Define Interface: Create the IService or IRepository interface in src\MyApp.Core\Interfaces.

Verify Core: Run PowerShell to ensure no prohibited references (like WPF) were added to Core.

Command: Select-String -Path "src\MyApp.Core\*.csproj" -Pattern "PresentationFramework"

Phase 2: Implementation & DI (dotnet-architect)

Goal: Build the engine and wire it into the solution.

Service Logic: Implement the interface in src\MyApp.Application\Services.

Persistence: If using EF Core, update the DbContext and add a migration via PowerShell.

Command: dotnet ef migrations add Add[FeatureName] --project src\MyApp.Infrastructure

Register DI: Open App.xaml.cs (or your DI module) and register the new service.

Snippet: services.AddTransient<IFeatureService, FeatureService>();

Phase 3: ViewModel Implementation (wpf-developer)

Goal: Create the UI logic and state container.

Create ViewModel: Create [Feature]ViewModel.cs in src\MyApp.Wpf\ViewModels.

Apply Toolkit: Use [ObservableProperty] and [RelayCommand] from CommunityToolkit.Mvvm.

Inject Service: Request the IService via the constructor (Constructor Injection).

Async Commands: Ensure all data-loading commands are async Task.

Phase 4: XAML Implementation (wpf-developer)

Goal: Build the visual representation.

Create View: Create [Feature]View.xaml (UserControl) in src\MyApp.Wpf\Views.

DataBinding: Bind controls to ViewModel properties using {Binding ...}.

No Code-Behind: Ensure .xaml.cs only contains InitializeComponent().

Resource Check: Ensure all colors and font sizes use StaticResource from the global dictionaries.

Phase 5: Verification (desktop-tester)

Goal: Ensure logic is correct and the build is stable.

Unit Tests: Create a test class in tests\MyApp.UnitTests.

Mocking: Use Moq to isolate the ViewModel from the real database/API.

Execute Tests: Run PowerShell command: dotnet test.

XAML Validation: Run dotnet build to catch XAML naming errors or missing resources.

🛑 Critical Checkpoints
Checkpoint	Validation Command (PowerShell)	Expectation
Solution Health	dotnet build	No errors.
Logic Integrity	dotnet test	All tests pass.
Clean Architecture	Get-ChildItem -Recurse -Filter "*.csproj" | Select-String "ProjectReference"	Presentation depends on Application, not Infrastructure.
Binding Hygiene	(Manual Check)	No System.Windows.Data Error in Output window.
🆘 Troubleshooting Feature Implementation

Error: "Cannot resolve Service X": Check App.xaml.cs. You likely forgot to register the Service or the ViewModel in the IServiceCollection.

UI not updating: Ensure the ViewModel inherits from ObservableObject and you are using [ObservableProperty] (which requires the class to be partial).

XAML Intellisense broken: Run Get-ChildItem -Include bin,obj -Recurse | Remove-Item -Recurse -Force then rebuild.