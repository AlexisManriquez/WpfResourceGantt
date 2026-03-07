---
name: clean-architecture
description: Guidelines for structuring .NET solutions using Clean Architecture (Onion Architecture). Enforces strict layer separation (Core, Application, Infrastructure, Presentation) and Dependency Injection.
---

# Clean Architecture for .NET

## 1. The Dependency Rule
Dependencies must point **inward** towards high-level policies.

1.  **Core (Domain)** - *Center*: Interfaces, Entities. (No dependencies).
2.  **Application** - *Middle*: Logic, Service Implementations. (Depends on Core).
3.  **Infrastructure** - *Outer*: Database, File System, API Clients. (Depends on Core/Application).
4.  **Presentation (WPF)** - *Outer*: Views, ViewModels. (Depends on Application/Core).

> 🛑 **CRITICAL:** `Core` must NEVER reference `EntityFramework`, `WPF`, or `Newtonsoft`. It is pure C#.

---

## 2. Solution Structure Template

When creating a new solution or refactoring, strictly follow this folder structure:

```text
MySolution.sln
│
├── src/
│   ├── MyApp.Core/                (Class Library - .NET Standard 2.1)
│   │   ├── Entities/              (POCOs)
│   │   ├── Interfaces/            (IService, IRepository)
│   │   └── Enums/
│   │
│   ├── MyApp.Application/         (Class Library - .NET 8)
│   │   ├── Services/              (Logic Implementation)
│   │   ├── DTOs/                  (Data Transfer Objects)
│   │   └── Validators/            (FluentValidation)
│   │
│   ├── MyApp.Infrastructure/      (Class Library - .NET 8)
│   │   ├── Data/                  (DbContext, Migrations)
│   │   ├── Repositories/          (Db Access)
│   │   └── External/              (HttpClients)
│   │
│   └── MyApp.Wpf/                 (WPF App - .NET 8 - Windows)
│       ├── Views/
│       ├── ViewModels/
│       └── App.xaml.cs            (Composition Root)
3. Layer Responsibilities
🟢 Core (Domain)

Contains: Enterprise business rules, Entities, Repository Interfaces.

Forbidden: Reference to Database drivers, UI libraries, or HTTP clients.

Why: Keeps business logic testable and independent of UI/DB.

🟡 Application

Contains: Orchestration logic. It implements the "Use Cases" of the app.

Role: It takes data from Infrastructure, applies logic, and prepares it for Presentation.

🔴 Infrastructure

Contains: Concrete implementations of interfaces defined in Core.

Role: "The Adapter". It talks to the SQL Server, the File System, or the Web API.

Example: CustomerRepository : ICustomerRepository.

🔵 Presentation (WPF)

Contains: XAML, ViewModels, Converters.

Role: Display data and capture commands.

DI Entry Point: App.xaml.cs is the "Composition Root" where all services are wired up.

4. Implementation Steps (Feature Workflow)

When adding a feature (e.g., "Add Customer"):

Define Interface (Core): Create ICustomerService.cs and Customer.cs.

Implement Logic (Application): Create CustomerService.cs.

Implement Data (Infrastructure): Create CustomerRepository.cs (EF Core logic).

Register DI (WPF): Add to App.xaml.cs: services.AddTransient<ICustomerService, CustomerService>();

Build UI (WPF): Create CustomerViewModel and CustomerView.

5. 🔧 PowerShell Validation

Use these commands to verify architectural integrity.

Verify Core has no bad dependencies

# Should return nothing. If it returns text, Core is polluted.
Select-String -Path "src\MyApp.Core\*.csproj" -Pattern "EntityFramework", "System.Windows"
Create Layer Structure (Scaffolding)

# Create the standard folder structure
$name = "MyApp"
New-Item -ItemType Directory -Force -Path "src/$name.Core"
New-Item -ItemType Directory -Force -Path "src/$name.Application"
New-Item -ItemType Directory -Force -Path "src/$name.Infrastructure"
New-Item -ItemType Directory -Force -Path "src/$name.Wpf"
Check Circular Dependencies

If build fails with "Circular Dependency", run:

# List project references to find loops
Get-ChildItem -Recurse -Filter "*.csproj" | Select-String "ProjectReference"