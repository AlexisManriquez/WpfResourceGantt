---
description: Handling build errors or runtime crashes (The F5 protocol).
---

# Workflow: Systematic .NET & WPF Debugging

> **Goal:** To resolve build failures, runtime crashes, or unexpected UI behavior by following an evidence-based diagnostic path.
> **Orchestration Type:** Diagnostic & Remedial (Led by `dotnet-debugger`)

---

## 🔍 Investigation Flow

```mermaid
graph TD
    A[Trigger: Build Error or Crash] --> B[Phase 1: Evidence Gathering]
    B --> C{Error Type?}
    C -- Compile Time --> D[Phase 2: Build & Dependency Audit]
    C -- Runtime / F5 --> E[Phase 3: Stack Trace & XAML Analysis]
    C -- Silent / UI --> F[Phase 4: DataContext & Binding Audit]
    D --> G[Implementation of Fix]
    E --> G
    F --> G
    G --> H[Phase 5: Verification]
🛠️ Step-by-Step Execution
Phase 1: Evidence Gathering (dotnet-debugger)

Goal: Stop guessing and look at the actual error text.

Extract Logs: Run a detailed build to capture specific MSBuild error codes.

PowerShell: dotnet build -v minimal /fl /flp:logfile=build.log;errorsonly

Ask User for F5 Data: If the crash is at runtime, explicitly ask:

"Please run in Debug mode (F5). Provide the Exception Type, the Message, and the first 10 lines of the Stack Trace."

Check InnerException: If the error is TargetInvocationException or XamlParseException, instruct the user to expand the "Inner Exception" in Visual Studio.

Phase 2: Build & Dependency Audit (dotnet-debugger + dotnet-architect)

Goal: Resolve project reference and NuGet issues.

The "Clean" Protocol: Eliminate stale artifacts.

PowerShell: Get-ChildItem -Include bin,obj -Recurse | Remove-Item -Recurse -Force

Restore Check: Ensure packages are actually there.

PowerShell: dotnet restore

Version Parity: Check if one project is .NET 6 and another is .NET 8.

PowerShell: Select-String -Path "*.csproj" -Pattern "TargetFramework"

Phase 3: Runtime & Crash Analysis (dotnet-debugger + Layer Owner)

Goal: Fix logic or threading errors.

Analyze NullReferenceException: Check if a service was not registered in App.xaml.cs.

Analyze InvalidOperationException: Check for cross-thread UI updates.

Fix: Wrap in Application.Current.Dispatcher.Invoke(() => { ... });.

Analyze XamlParseException: Look for missing StaticResource keys or typos in DataTemplate names.

Phase 4: Silent Failure / Binding Audit (wpf-developer)

Goal: Fix issues where the app runs but data doesn't appear.

Output Window Audit: Instruct the user to search the Visual Studio Output Window for "BindingExpression path error".

Trace Logging: Suggest adding a trace listener to the XAML binding.

Snippet: diag:PresentationTraceSources.TraceLevel=High (requires diag namespace).

DataContext Verification: Verify the View actually has the ViewModel assigned.

Phase 5: Verification (desktop-tester)

Goal: Ensure the fix doesn't break other parts of the system.

Regression Test: Run dotnet test.

Smoke Test: Build the app one last time.

PowerShell: dotnet build

🛑 Common "Red Flag" Patterns
Symptom	Probable Cause	Fix
App crashes on .Add() to list	Multi-threading	Use BindingOperations.EnableCollectionSynchronization or Dispatcher.
"Cannot find Resource X"	Merged Dictionaries	Check App.xaml to ensure the correct ResourceDictionary is merged.
ViewModel Property changes, UI doesn't	INotifyPropertyChanged	Ensure property is [ObservableProperty] and class is partial.
Build fails with "Metadata not found"	Project reference loop	Check project references for circular dependencies.
🔧 PowerShell Debugging Toolbox
Check for Service Registration

# Search App.xaml.cs to see if the crashed service is registered in DI
Select-String -Path "**/App.xaml.cs" -Pattern "AddTransient", "AddSingleton"
Search for specific Exception string in source

# Useful if you have a custom exception you are hunting for
Get-ChildItem -Recurse -Filter "*.cs" | Select-String -Pattern "throw new [Name]Exception"
Clean and Build Pipeline

Write-Host "Starting Deep Clean..." -ForegroundColor Cyan
Get-ChildItem -Include bin,obj -Recurse | Remove-Item -Recurse -Force
dotnet restore
dotnet build
if ($?) { Write-Host "Build Fixed!" -ForegroundColor Green }
else { Write-Host "Build still failing." -ForegroundColor Red }
