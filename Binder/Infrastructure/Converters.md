---
tags: [infrastructure]
purpose: AI reference for all WPF value converters.
---

# Converters

**Directory**: `ProjectManagement/Converters/` (28 files)

All WPF value converters used for data binding transformations.

## Hierarchy & Layout
| Converter | File | Purpose |
|-----------|------|---------|
| `HierarchyConverters` | `HierarchyConverters.cs` | Indentation logic for TreeGridViews |
| `IndentationConverter` | `IndentationConverter.cs` | Tree depth → left margin |
| `LevelToIndentConverter` | `LevelToIndentConverter.cs` | Level property → visual indent |
| `LeftMarginConverter` | `LeftMarginConverter.cs` | Margin calculation |
| `BoolToColumnConverter` | `BoolToColumnConverter.cs` | Grid column toggling |
| `BoolToColumnSpanConverter` | `BoolToColumnSpanConverter.cs` | Grid column span |
| `DoubleToGridLengthConverter` | `DoubleToGridLengthConverter.cs` | Double → GridLength |
| `AutoFitMarginConverter` | `AutoFitMarginConverter.cs` | Dynamic sizing |
| `AutoFitWidthConverter` | `AutoFitWidthConverter.cs` | Dynamic sizing |
| `CanvasLeftConverter` | `CanvasLeftConverter.cs` | Gantt bar positioning |

## Status & Health
| Converter | File | Purpose |
|-----------|------|---------|
| `HealthToBrushConverter` | `HealthToBrushConverter.cs` | SPI/CPI → Red/Yellow/Green |
| `StatusToOpacityConverter` | `StatusToOpacityConverter.cs` | Completed items dimming |
| `TaskStatusToBooleanConverter` | `TaskStatusToBooleanConverter.cs` | Status enum → bool |
| `WorkItemStateConverter` | `WorkItemStateConverter.cs` | WorkItem state display |
| `WorkItemStatusConverter` | `WorkItemStatusConverter.cs` | Status enum → display string |

## Visibility & Boolean
| Converter | File | Purpose |
|-----------|------|---------|
| `BooleanToVisibilityConverter` | `BooleanToVisibilityConverter.cs` | `bool` → `Visibility` |
| `EnumToBooleanConverter` | `EnumToBooleanConverter.cs` | Enum value → bool |
| `StringEqualityToVisibilityConverter` | `StringEqualityToVisibilityConverter.cs` | String match → Visibility (used for contextual toolbar) |

## Value Formatting
| Converter | File | Purpose |
|-----------|------|---------|
| `ValueToKiloCurrencyConverter` | `ValueToKiloCurrencyConverter.cs` | Number → "$12.5K" format |
| `EvmMetricMultiConverter` | `EvmMetricMultiConverter.cs` | Formats metrics based on Dollars/Hours mode toggle |
| `NegativeValueConverter` | `NegativeValueConverter.cs` | Negation |
| `StringSplitConverter` | `StringSplitConverter.cs` | String splitting |

## Infrastructure
| Converter | File | Purpose |
|-----------|------|---------|
| `BindingProxy` | `BindingProxy.cs` | Fix for DataGrid binding context loss |

## Root-Level Converters
| File | Purpose |
|------|---------|
| `GroupSummaryConverter.cs` | Data grouping in DataGrids |
| `GroupSummaryMultiConverter.cs` | Multi-value grouping |
| `JsonConverter.cs` | Custom JSON serialization |
| `StringToIsNotEmptyConverter.cs` | String → bool (non-empty check) |

## Related Pages
- [[UI & Styles]] — where converters are used in XAML
- [[Gantt View]] — primary consumer of hierarchy converters
- [[Navigation & Ribbons]] — uses StringEqualityToVisibilityConverter
