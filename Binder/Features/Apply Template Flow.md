---
tags: [feature]
purpose: AI reference for the template application workflow.
---

# Apply Template Flow

**Files**: `ProjectManagement/Features/ApplyTemplate/ApplyTemplateDialogView.xaml`, `ApplyTemplateDialogViewModel.cs`

## Full Flow
1. User clicks 📋 on a Sub-Project row in [[System Management]]
2. `ApplyTemplateCommand` fires `SystemManagementViewModel.HandleApplyTemplate`
3. `TemplateService.GetAllTemplatesAsync()` loads available templates
4. VM creates `ApplyTemplateDialogViewModel`
5. VM calls `MainViewModel.ShowModalCustomDialog()`
6. User selects a template + chooses **Overwrite** vs **Append**
7. On confirm: `TemplateService.ApplyTemplateAsync()` runs dual-path conversion:
   - **Path A** (Gates with Tasks): Creates summary gate + child leaf tasks with predecessors
   - **Path B** (Gates without Tasks): Creates flat leaf gate with ProgressBlocks
8. ID generation: `ParentID|Sequence` for gates, `GateID|Sequence_random` for tasks
9. Predecessor mapping: relative template indices (`"0"`, `"1.0, 1.1"`) → actual generated IDs
10. WBS generation: `DataService.RegenerateWbsValues()` ensures NOT NULL constraints
11. `DataService.SaveDataAsync()` persists to SQL
12. [[ScheduleCalculationService]] runs on next load → drives dates via forward/backward pass
13. VM row refreshes its `Children` collection from DB

## Overwrite vs Append
| Mode | Behavior |
|------|----------|
| **Overwrite** | Clears all existing Level 3+ children before adding template |
| **Append** | Adds template gates alongside existing children |

## Result: What Gets Created

For the Default Template:
```
📁 Sub-Project (Level 2)
├── 📁 Design (Leaf, 30d) — with 6 ProgressBlock checklists
└── 📁 Integration (Summary) — dates rolled up from children
    ├── Software Development  (Leaf, 45d, Pred: Design)
    ├── Hardware Delivery      (Receipt, 60d, 0 work)
    ├── UUT Delivery           (Receipt, 30d, 0 work)
    └── Station Integration   (Leaf, 20d, Pred: SW Dev + HW + UUT)
```

## Related Pages
- [[System Management]] — trigger point
- [[TemplateService]] — conversion engine (dual-path logic)
- [[Templates]] — blueprint models (includes `TemplateTask`)
- [[ScheduleCalculationService]] — drives dates after apply
- [[DataService]] — persistence
- [[MainViewModel]] — dialog system host
