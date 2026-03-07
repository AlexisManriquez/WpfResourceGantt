namespace WpfResourceGantt.ProjectManagement.UI
{
    using System.Windows;

    /// <summary>
    /// Attached properties for the Tactical Ribbon button system.
    /// Allows buttons to specify an alternate icon for the active/selected state.
    /// </summary>
    public static class RibbonProperties
    {
        public static readonly DependencyProperty ActiveIconProperty =
            DependencyProperty.RegisterAttached(
                "ActiveIcon",
                typeof(object),
                typeof(RibbonProperties),
                new FrameworkPropertyMetadata(null));

        public static void SetActiveIcon(DependencyObject element, object value)
            => element.SetValue(ActiveIconProperty, value);

        public static object GetActiveIcon(DependencyObject element)
            => element.GetValue(ActiveIconProperty);
    }
}
