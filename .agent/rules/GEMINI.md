---
trigger: always_on
---

# GEMINI.md - Maestro Configuration (WPF .NET Edition)

> **Version 1.0** - Desktop Development Orchestrator
> This file defines how the AI behaves in this C# WPF workspace.

---

## 🛑 CRITICAL: AGENT & SKILL PROTOCOL (START HERE)

> **MANDATORY:** You MUST read the appropriate agent file and its skills BEFORE performing any implementation. This is the highest priority rule.

### 1. Modular Skill Loading Protocol

Agent activated → Check frontmatter "skills:" field
│
└── For EACH skill:
├── Read SKILL.md (INDEX only)
├── Find relevant sections from content map
└── Read ONLY those section files

- **Rule Priority:** P0 (GEMINI.md) > P1 (Agent .md) > P2 (SKILL.md). All rules are binding.

### 2. Enforcement Protocol
1. **When agent is activated:**
   - ✅ READ all rules inside the agent file.
   - ✅ CHECK frontmatter `skills:` list.
   - ✅ LOAD each skill's `SKILL.md`.
   - ✅ APPLY all rules from agent AND skills.

---

## 📥 REQUEST CLASSIFIER (STEP 2)

**Before ANY action, classify the request:**

| Request Type | Trigger Keywords | Active Tiers | Result |
|--------------|------------------|--------------|--------|
| **UI/XAML** | "design", "view", "style", "control", "animation", "grid" | TIER 0 + wpf-developer | XAML Implementation |
| **LOGIC/DATA**| "service", "database", "model", "logic", "async", "api" | TIER 0 + dotnet-architect | C# Logic / Architecture |
| **TESTING** | "test", "unit", "mock", "verify", "xunit" | TIER 0 + desktop-tester | Unit/Integration Tests |
| **SURVEY** | "analyze", "list files", "explain solution" | TIER 0 + Explorer | Session Intel |

---

## TIER 0: UNIVERSAL RULES (Always Active)

### 🧩 Solution Awareness
**Before modifying ANY file:**
1. **Check Solution:** Read `.sln` and `.csproj` files to understand project structure and target framework (.NET 6/7/8).
2. **Namespace Consistency:** Ensure `namespace` declarations match the folder structure (e.g., `MyApp.Views.Controls`).
3. **NuGet Awareness:** Do not hallucinate packages. Check `packages.config` or `<PackageReference>` in the csproj file.

### 🧹 Clean Code (.NET Edition)
**ALL code MUST follow `@[skills/csharp-best-practices]` rules.**

- **Async/Await:** ALWAYS use `async Task` (never `async void` unless it is a top-level Event Handler).
- **Properties:** Use Auto-properties `{ get; set; }` unless backing fields are strictly required for logic.
- **Naming:** PascalCase for Methods/Classes/Properties, camelCase for private fields (`_privateField`).
- **Dependencies:** Never instantiate heavy services with `new`. Use Constructor Injection.

---

## TIER 1: CODE RULES (When Writing Code)

### 📱 Project Type Routing

| Project Type | Primary Agent | Skills |
|--------------|---------------|--------|
| **WPF UI / XAML** | `wpf-developer` | wpf-patterns, desktop-design |
| **Core Logic / BLL** | `dotnet-architect` | csharp-best-practices, dependency-injection |
| **Testing** | `desktop-tester` | testing-patterns, xunit-patterns |

> 🔴 **Logic in Code-Behind = WRONG.** UI Logic belongs in ViewModels (MVVM). `MainWindow.xaml.cs` should be nearly empty.

### 🛑 Socratic Gate (Architecture Check)

**MANDATORY: Every complex request must pass through the Socratic Gate.**

**For WPF Tasks, you MUST clarify:**
1. **MVVM Strategy:** "Are we using CommunityToolkit.Mvvm, Prism, or raw INotifyPropertyChanged?"
2. **Data Source:** "Is the data coming from a local DB (SQLite), JSON file, or REST API?"
3. **Threading:** "Will this operation block the UI thread? Do we need `Task.Run`?"

**Protocol:**
1. **Never Assume:** If the user says "Add a grid", ask if they mean a Layout Grid or a DataGrid.
2. **Wait:** Do NOT write XAML or C# until architecture questions are answered.

### 🏁 Final Checklist Protocol

**Trigger:** When the user says "final checks", "build solution", or "verify".

| Task Stage | Command | Purpose |
|------------|---------|---------|
| **Build** | `dotnet build` | Verify compilation |
| **Test** | `dotnet test` | Run unit tests |
| **Format** | `dotnet format` | Apply style rules |

**Completion Rule:** A task is NOT finished until the solution compiles without errors.

---

## TIER 2: DESIGN RULES (Reference)

> **Design rules are in the specialist agents, NOT here.**

| Task | Read |
|------|------|
| UI Layouts / Styles | `~/.agent/wpf-developer.md` |
| Data Models / Architecture | `~/.agent/dotnet-architect.md` |

**These agents contain:**
- XAML Style Guides (ResourceDictionaries)
- ControlTemplate rules
- Database Schema policies (Entity Framework)

---

## 📁 QUICK REFERENCE

### Available Master Agents

| Agent | Domain & Focus |
|-------|----------------|
| `wpf-developer` | XAML Specialist (Views, Styles, DataTemplates, MVVM Binding) |
| `dotnet-architect` | C# Logic (Services, Models, Entity Framework, DI) |
| `desktop-tester` | QA (xUnit Tests, Moq, Integration Testing) |
| `dotnet-debugger` | Expert in troubleshooting .NET build errors, runtime crashes, and XAML binding failures. Use when the solution won't compile, the app crashes on startup, or behavior is unexpected. |

### Key Skills

| Skill | Purpose |
|-------|---------|
| `wpf-patterns` | Binding, Commands, Converters, Behaviors |
| `csharp-best-practices` | LINQ, Async, Nullable types, Records |
| `clean-architecture` | Dependency Injection, Repository Pattern |
| `visual-studio-mastery` | Solution management, NuGet, Debugging |

## 🛠️ CUSTOM AI TOOLS (MANDATORY USAGE)

To preserve tokens and ensure accuracy, **DO NOT use raw PowerShell commands** (`Get-ChildItem`, `Select-String`, `cat`, `grep`, or `dotnet build`) for searching, reading, or building. 

### 🚀 Research & Navigation Protocol
Follow this hierarchy to minimize token cost and avoid command failures:
1. **Outline First:** For any file > 300 lines (especially `.cs` and `.xaml`), use `view_file_outline` to find method boundaries before reading.
2. **Targeted Search:** Use `python .agent/tools/smart_search.py "Query"` to find definitions across the workspace.
3. **Chunked Reading:** Use `python .agent/tools/read_chunk.py` with specific `--start` and `--end` lines obtained from the outline.
4. **Shell Ban:** Direct shell commands for file inspection (`cat`, `type`, `grep`, `dir /s`) are strictly prohibited. If a Python tool fails, investigate the tool logic or ask the USER.

You MUST use the provided Python scripts located in `.agent/tools/`.

| Action Required | Mandatory Command |
|-----------------|-------------------|
| **Build & Check Errors** | `python .agent/tools/build_analyzer.py` |
| **Search Codebase** | `python .agent/tools/smart_search.py "MyClassName"` |
| **Read Large File** | `python .agent/tools/read_chunk.py src/MyView.xaml --start 50 --end 100` |
| **Validate XAML** | `python .agent/tools/xaml_validator.py "Views/MyView.xaml"` |
| **Verify Edit Match** | `python .agent/tools/verify_edit.py src/MyFile.cs --start 10 --end 20` |
| **Verify Sync** | `python .agent/tools/doc_assistant.py` |

*Note: The `read_chunk.py` script prepends line numbers. Use these line numbers when writing edit/replace logic.*