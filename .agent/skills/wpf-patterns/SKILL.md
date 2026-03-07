---
name: wpf-patterns
description: Technical implementation details for WPF, XAML, Data Binding, and the CommunityToolkit.Mvvm library. Use when generating ViewModels, XAML layouts, or ValueConverters.
---

# WPF & MVVM Patterns (Modern Edition)

## 1. MVVM Implementation (Source Generators)
We use **CommunityToolkit.Mvvm** to reduce boilerplate. Do not write manual `INotifyPropertyChanged` code unless strictly necessary.

### ❌ The Old Way (Verbose)
```csharp
private string _firstName;
public string FirstName
{
    get => _firstName;
    set
    {
        if (_firstName != value)
        {
            _firstName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FullName)); // Error prone
        }
    }
}
✅ The Modern Way (Source Generators)

using CommunityToolkit.Mvvm.ComponentModel;

public partial class UserViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string _firstName;

    public string FullName => $"{FirstName} ...";
}
2. Command Pattern (Async Support)

Avoid Click event handlers in code-behind. Use [RelayCommand] to bind buttons to methods.

❌ The Old Way (ICommand)

public ICommand SaveCommand { get; }
public MyViewModel() {
    SaveCommand = new RelayCommand(Save, CanSave);
}
✅ The Modern Way (RelayCommand)

using CommunityToolkit.Mvvm.Input;

[RelayCommand(CanExecute = nameof(CanSave))]
private async Task SaveAsync(CancellationToken token)
{
    // Awaitable logic here
    await _service.SaveAsync(token);
}

private bool CanSave() => !string.IsNullOrEmpty(FirstName);

In XAML:


<!-- Note: The generator strips 'Async' from the command name -->
<Button Command="{Binding SaveCommand}" Content="Save" />
3. Data Binding Discipline
Binding Modes

OneWay: Default for TextBlock/Label (Read-only source).

TwoWay: Required for TextBox/CheckBox (Input fields).

UpdateSourceTrigger: Use PropertyChanged for real-time validation.


<TextBox Text="{Binding UserName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
Debugging Bindings

If a binding fails silently, check the Output Window in Visual Studio.
To debug in XAML:


<!-- Adds a trace level to specific binding -->
<TextBlock Text="{Binding BrokenPath, diag:PresentationTraceSources.TraceLevel=High}"
           xmlns:diag="clr-namespace:System.Diagnostics;assembly=WindowsBase" />
4. Value Converters

Use converters to transform data for UI display without cluttering the ViewModel.

Pattern:

Create folder Converters/.

Implement IValueConverter.

Use [ValueConversion(typeof(bool), typeof(Visibility))] attribute.

Example (BoolToVisibility):

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }
    // ... Implement ConvertBack
}
5. XAML Layout Strategy
Grid vs StackPanel

Grid: Use for high-level structure (Header, Content, Footer). It is the most performant layout root.

StackPanel: Use for simple linear lists of small items.

DockPanel: Use for window chrome (Menu bars, Status bars).

Performance Rule

Virtualization is mandatory for lists with >100 items.


<ListBox ItemsSource="{Binding LargeCollection}">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
6. Resource Dictionaries (Theming)

Never clutter App.xaml with hundreds of styles. Structure Resources/ logically:

Resources/Colors.xaml (Brushes)

Resources/Styles.xaml (Control Styles)

Resources/Templates.xaml (DataTemplates)

Merge them in App.xaml:


<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Colors.xaml" />
            <ResourceDictionary Source="Resources/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
