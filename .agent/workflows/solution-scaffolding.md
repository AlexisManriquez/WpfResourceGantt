---
description: Setting up a brand new Clean Architecture solution from scratch.
---

# Workflow: Clean Architecture Solution Scaffolding

> **Goal:** To create a brand new, enterprise-grade .NET Solution with a WPF Presentation layer, structured according to Clean Architecture principles.
> **Orchestration Type:** Multi-Agent Automation

---

## 🏗️ Architecture Blueprint
- **Solution Name:** `[SolutionName]`
- **Layers:** 
  - `.Core` (Domain Entities & Interfaces)
  - `.Application` (Logic & Services)
  - `.Infrastructure` (Database & External APIs)
  - `.Wpf` (MVVM UI)
  - `.UnitTests` (xUnit)

---

## 🛠️ Step-by-Step Execution

### Phase 1: Environment Setup (`dotnet-architect`)
**Goal:** Initialize the solution and the root directory.
1. **Initialize Solution:**
   ```powershell
   dotnet new sln -n [SolutionName]

Create Directory Tree:

New-Item -ItemType Directory -Path "src", "tests"
Phase 2: Project Creation (dotnet-architect)

Goal: Generate the SDK-style projects.

Create Core (Standard 2.1 or .NET 8):

dotnet new classlib -n [SolutionName].Core -o src/[SolutionName].Core

Create Application:

dotnet new classlib -n [SolutionName].Application -o src/[SolutionName].Application

Create Infrastructure:

dotnet new classlib -n [SolutionName].Infrastructure -o src/[SolutionName].Infrastructure

Create WPF App:

dotnet new wpf -n [SolutionName].Wpf -o src/[SolutionName].Wpf

Create Unit Tests:

dotnet new xunit -n [SolutionName].UnitTests -o tests/[SolutionName].UnitTests
Phase 3: Dependency Mapping (dotnet-architect)

Goal: Enforce the "Dependencies point inward" rule.

Wire References:

# Application depends on Core
dotnet add src/[SolutionName].Application reference src/[SolutionName].Core

# Infrastructure depends on Application and Core
dotnet add src/[SolutionName].Infrastructure reference src/[SolutionName].Application

# WPF depends on Application and Core
dotnet add src/[SolutionName].Wpf reference src/[SolutionName].Application

# Tests depend on everything
dotnet add tests/[SolutionName].UnitTests reference src/[SolutionName].Application

Add to Solution:

Get-ChildItem -Recurse -Filter "*.csproj" | ForEach-Object { dotnet sln add $_.FullName }
Phase 4: Core NuGet Setup (visual-studio-mastery)

Goal: Install essential toolkits.

MVVM Toolkit (For WPF):

dotnet add src/[SolutionName].Wpf package CommunityToolkit.Mvvm

DI & Hosting (For App Entry):

dotnet add src/[SolutionName].Wpf package Microsoft.Extensions.Hosting

Mocking (For Tests):

dotnet add tests/[SolutionName].UnitTests package Moq
Phase 5: Boilerplate Implementation (wpf-developer)

Goal: Set up the initial MVVM folders and the DI container.

Folder Scaffolding (WPF): Create Views, ViewModels, Models, and Resources folders.

Generic Host Setup: Modify App.xaml.cs to implement IHost for Dependency Injection.

Initial View: Create a simple MainView.xaml and MainViewModel.cs to verify binding works.

🛑 Verification Checkpoints
Check	PowerShell Command	Success Criteria
Compilation	dotnet build	Build Succeeded.
Project Refs	Select-String -Path "src\**\*.csproj" -Pattern "ProjectReference"	No illegal directions (e.g., Core depends on Wpf).
Namespace Hygiene	Get-ChildItem -Recurse -Filter "*.cs" | Select-String "namespace"	Namespaces match folder structure.
Git Setup	Test-Path ".gitignore"	.gitignore exists and excludes bin/obj.
📝 Required Project Configuration (.csproj)

Ensure the WPF project has these flags enabled:

<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <UseWPF>true</UseWPF>
</PropertyGroup>
🆘 Troubleshooting Scaffolding

"Target Framework Mismatch": Ensure Class Libraries are .NET 8.0 if the WPF app is .NET 8.0.

"WPF types not found": Check that the Class Library project file doesn't accidentally have <UseWPF>true</UseWPF> unless it's a UI library.

"Missing gitignore": Run dotnet new gitignore at the solution root.