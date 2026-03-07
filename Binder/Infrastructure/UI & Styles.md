---
tags: [infrastructure, ui]
purpose: AI reference for the application's visual theme and style system.
---

# UI & Styles

## Style Files
**Directory**: `ProjectManagement/UI/Styles/`

| File | Purpose |
|------|---------|
| `CoreStyles.xaml` | Global `ResourceDictionary` — colors, brushes, button styles, text styles |
| `Icons.xaml` | SVG path data for all application icons |

These are merged into `App.xaml` as application-wide resources.

## Key Views
**Directory**: `ProjectManagement/UI/Views/`

| File | Purpose |
|------|---------|
| `TacticalRibbonView.xaml` | Primary navigation ribbon (see [[Navigation & Ribbons]]) |

## Theme Architecture
1. Colors, brushes, and fonts defined in `CoreStyles.xaml`
2. `App.xaml` merges both `CoreStyles.xaml` and `Icons.xaml`
3. All views reference styles via `StaticResource` or `DynamicResource`
4. Icon paths are referenced as `{StaticResource IconName}` in button content

## Selectors
**Directory**: `ProjectManagement/Selectors/`

Contains custom `DataTemplateSelector` classes for dynamic template resolution.

## Adorners
**Directory**: `ProjectManagement/Adorners/`

| File | Purpose |
|------|---------|
| `DragAdorners.cs` | Visual feedback during drag-and-drop operations in [[Gantt View]] |

## Related Pages
- [[Navigation & Ribbons]] — ribbon view details
- [[Converters]] — visual transformation logic
- [[Common Modifications]] — how to add new styles
