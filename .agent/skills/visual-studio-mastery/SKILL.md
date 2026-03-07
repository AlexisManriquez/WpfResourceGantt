---
name: visual-studio-mastery
description: Expert knowledge of Visual Studio Solution management, NuGet strategies, MSBuild, and debugging tools. Use this when adding projects, managing dependencies, or fixing build errors.
---

# Visual Studio & Solution Mastery

## 1. Solution Structure Strategy
We follow the **Modern .NET Layout** (separation of source and tests).

```text
MySolution.sln
├── src/
│   ├── MyApp.Core/       (Class Library - Standard 2.1)
│   ├── MyApp.Wpf/        (WPF App - .NET 6/7/8)
│   └── MyApp.Infra/      (Class Library - .NET 6/7/8)
└── tests/
    ├── MyApp.Core.Tests/ (xUnit)
    └── MyApp.Wpf.Tests/  (xUnit)
🔧 PowerShell Commands

Create Solution: dotnet new sln -n MySolution

Add Project: dotnet sln add src/MyApp.Core/MyApp.Core.csproj

Reference Project: dotnet add src/MyApp.Wpf/MyApp.Wpf.csproj reference src/MyApp.Core/MyApp.Core.csproj

2. NuGet Package Management

Do NOT use packages.config (Legacy). We use PackageReference in the .csproj file.

Management Rules

Central Versioning: If multiple projects use the same package (e.g., Newtonsoft.Json), ensure versions match exactly to avoid "DLL Hell".

Version Awareness: Do not hallucinate versions. Use the latest stable unless specified.

🔧 Installation Commands

Use the CLI for stability over the internal VS console.

# Install a package (e.g., MVVM Toolkit)
dotnet add package CommunityToolkit.Mvvm

# Restore all packages (Fix missing references)
dotnet restore
3. The Modern .csproj (.NET 6+)

We use the SDK-Style project format. It is concise and XML-based.

Essential Properties

Ensure these properties exist in the <PropertyGroup>:

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
Editing Strategy

Do not add files manually (<Compile Include...). The SDK includes all .cs files by default.

Only edit .csproj to add dependencies, resources, or build configurations.

4. Debugging & Output Hygiene
WPF Binding Failures

By default, binding errors fail silently. To catch them, the agent must check the Output Window text.

Config: In App.xaml.cs (Debug only), set the data binding trace level.

System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
#endif
The "Clean" Protocol

If Visual Studio behaves erratically (IntelliSense errors, weird build failures), perform a Deep Clean.

🔧 PowerShell Deep Clean Script:

Get-ChildItem -Include bin,obj -Recurse | Remove-Item -Recurse -Force
dotnet restore
dotnet build
5. .gitignore for .NET

Never commit build artifacts. Ensure .gitignore includes:

[Bb]in/
[Oo]bj/
.vs/
*.user
*.suo
