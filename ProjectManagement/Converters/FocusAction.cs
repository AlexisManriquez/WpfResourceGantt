using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfResourceGantt.ProjectManagement.Converters
{
    /// <summary>
    /// A generic behavior to set focus to the attached element when triggered.
    /// Useful for auto-focusing TextBoxes when they become visible.
    /// </summary>
    public class FocusAction : TriggerAction<UIElement>
    {
        protected override void Invoke(object parameter)
        {
            // Dispatch the focus call to ensure the UI element is fully rendered and visible
            AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                AssociatedObject.Focus();

                // If it's a TextBox, select all text for easy replacement
                if (AssociatedObject is TextBox tb)
                {
                    tb.SelectAll();
                }
            }));
        }
    }
}
