---
tags: [index, moc]
purpose: Central hub — Map of Content for the entire WpfResourceGantt project.
---

# 🏠 WpfResourceGantt — Map of Content

> **WpfResourceGantt** is a .NET 8 WPF desktop application for DoD-style project management with WBS trees, Earned Value Management, resource allocation, and template-driven workflows.

---

## Architecture
- [[App Overview]] — Entry point, tech stack, MVVM pattern, core services
- [[Data Hierarchy]] — SystemItem → WorkBreakdownItem → ProgressBlock → ProgressItem
- [[Navigation & Ribbons]] — Dual-ribbon UI, view switching, contextual toolbar
- [[Common Modifications]] — Quick-reference recipes for common changes
- [[EVM Calculation Rules]] — BAC, BCWS, BCWP, SPI, CPI formulas and their implementation
- [[Data Lifecycle]] — How data flows from creation → save → rollup → display

## Models
- [[ProjectData & SystemItem]] — Root JSON structure, SystemItem (Level 0)
- [[WorkBreakdownItem]] — Recursive WBS node, EVM fields, rollup logic
- [[ProgressBlock & Items]] — Checklists that drive physical % complete
- [[Templates]] — ProjectTemplate, TemplateGate, TemplateProgressBlock, TemplateProgressItem
- [[User & Role]] — Users, Roles, Section assignments
- [[Resource & Gantt Models]] — ResourcePerson, ResourceTask, ResourceGanttModels

## Services
- [[DataService]] — Central repository: load, save (debounced graph-tracked), import, clone
- [[TemplateService]] — Blueprint-to-live-data conversion engine (dual-path: logic-driven + flat)
- [[ScheduleCalculationService]] — CPM schedule engine: forward pass, backward pass, float, critical path
- [[MppImportExport]] — MS Project XML import and native .mpp export
- [[CsvImportService]] — CSV hours import pipeline

## Features
- [[Gantt View]] — Core WBS editing interface with hierarchical tree
- [[Dashboard]] — High-level health cards and KPI overview
- [[System Management]] — CRUD for Systems (Level 0 containers)
- [[User Management]] — Administration of users, roles, sections
- [[EVM]] — S-Curve charts, KPI gauges, WBS drill-down
- [[Analytics]] — Resource utilization and workload analysis
- [[Resource Gantt]] — Person-centric timeline (capacity planning)
- [[Developer Portal]] — Role-filtered task dashboard
- [[Gate Progress]] — Sub-Project drill-down showing gates and test blocks
- [[Quick Tasks]] — Flat list of ad-hoc administrative tasks
- [[Project Creation]] — Recursive in-memory WBS builder (pre-commit)
- [[Apply Template Flow]] — Template selection → overwrite/append → save

## Infrastructure
- [[MainViewModel]] — Central orchestrator: navigation, dialog system, global state
- [[Converters]] — All WPF value converters (hierarchy, health, visibility)
- [[UI & Styles]] — CoreStyles.xaml, Icons.xaml, TacticalRibbonView.xaml
