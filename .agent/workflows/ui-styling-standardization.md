---
description: Implementing or updating the visual theme of the application.
---

# Workflow: UI Styling & Theme Standardization

> **Goal:** To establish or update global visual styles, ensure theme consistency, and eliminate hardcoded UI values in favor of centralized resources.
> **Orchestration Type:** Resource Management (Led by `wpf-developer`)

---

## 🎨 Styling Architecture
WPF styles must follow a "Atomic to Composite" hierarchy:
1.  **Atomic (Brushes.xaml):** Raw colors and brushes.
2.  **Typography (Fonts.xaml):** Font sizes, weights, and families.
3.  **Basic Controls (CoreStyles.xaml):** Styles for Buttons, TextBoxes, etc.
4.  **Layouts (SharedLayouts.xaml):** Margins, Paddings, and Grid definitions.

---

## 🛠️ Step-by-Step Execution

### Phase 1: Resource Dictionary Audit (`wpf-developer`)
**Goal:** Locate where styles live and identify hardcoded "leaks."
1.  **Find Resources:**
    *   *PowerShell:* `Get-ChildItem -Path . -Recurse -Filter "Resources" -Directory`
2.  **Scan for Hardcoded Hex:** Find UI elements using inline colors instead of resources.
    *   *PowerShell:* `Select-String -Path "src\**\*.xaml" -Pattern 'Background="#', 'Foreground="#' | Select-String -NotMatch '"{StaticResource'`
3.  **Check App.xaml:** Verify the `MergedDictionaries` order. Base colors must be merged *before* control styles.

### Phase 2: Atomic Definition (`wpf-developer`)
**Goal:** Define the palette.
1.  **Define Brushes:** Create or edit `Resources\Brushes.xaml`.
    *   *Snippet:* `<SolidColorBrush x:Key="PrimaryActionBrush" Color="#007ACC" />`
2.  **Define Typography:** Create or edit `Resources\Typography.xaml`.
    *   *Snippet:* `<FontSize x:Key="HeaderFontSize">24</FontSize>`

### Phase 3: Control Template Implementation (`wpf-developer`)
**Goal:** Apply the look and feel to native controls.
1.  **Targeted Styles:** Create styles in `Resources\CoreStyles.xaml` with `TargetType`.
2.  **Avoid Keys for Defaults:** If a style should apply to *every* button, omit the `x:Key`.
    *   *Rule:* Use `BasedOn="{StaticResource {x:Type Button}}"` to inherit from system defaults.
3.  **Interaction States:** Ensure `VisualStateManager` or `Triggers` handle `IsMouseOver`, `IsPressed`, and `IsEnabled`.

### Phase 4: DataTemplate Mapping (`wpf-developer` + `dotnet-architect`)
**Goal:** Map ViewModels to Views globally so the UI "just works" when navigating.
1.  **Update Templates.xaml:** Define `DataTemplate` for every ViewModel.
    ```xml
    <DataTemplate DataType="{x:Type vm:CustomerViewModel}">
        <v:CustomerView />
    </DataTemplate>
    ```

### Phase 5: Verification & Accessibility (`desktop-tester`)
**Goal:** Ensure the UI is performant and readable.
1.  **Resource Lookup Test:** Build the solution to ensure no `StaticResource` keys are missing.
    *   *PowerShell:* `dotnet build`
2.  **Theme Swap Test:** If the app supports Dark Mode, verify that all brushes use `DynamicResource` instead of `StaticResource`.
3.  **Check Contrast:** Instruct user: *"Please check the contrast ratio of the PrimaryActionBrush against the background in F5 mode."*

---

## 🛑 Standardization Rules

| Rule | Description | Rationale |
|------|-------------|-----------|
| **No Inline Brushes** | `Background="#FFF"` is forbidden. | Breaks theming support. |
| **No Magic Numbers** | `Margin="12,0,5,2"` is forbidden. | Use a `Thickness` resource. |
| **Grid over Canvas** | Use `Grid` for responsive layouts. | Canvas doesn't scale with window resizing. |
| **Dynamic for Themes** | Use `DynamicResource` for anything that changes at runtime. | `StaticResource` only evaluates once at startup. |

---

## 🔧 PowerShell Styling Audit Tools

### Find all XAML files without Shared Resources
```powershell
# Identify Views that might be missing the global ResourceDictionary merge
Get-ChildItem -Recurse -Filter "*.xaml" | ForEach-Object {
    $content = Get-Content $_.FullName
    if ($content -notmatch "ResourceDictionary.MergedDictionaries") {
        Write-Host "Local Resource Warning: $($_.Name)" -ForegroundColor Yellow
    }
}
Search for specific Style Key usage

# Verify if a style (e.g., 'PrimaryButtonStyle') is actually being used
Select-String -Path "src\**\*.xaml" -Pattern "Style=\"{StaticResource PrimaryButtonStyle}\""
🆘 Troubleshooting Styling

"Resource Not Found" at Runtime: This usually means the ResourceDictionary is not merged in App.xaml or the x:Key has a typo.

Style not applying to inherited controls: Ensure TargetType matches exactly. If you style Control, it won't automatically style Button unless you use BasedOn.

UI is blurry: Ensure UseLayoutRounding="True" is set on the root Window to snap elements to physical pixels.