using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace WpfResourceGantt.ProjectManagement.Features.SystemManagement
{
    public class InvokeDelegateCommandAction : TriggerAction<DependencyObject>
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(InvokeDelegateCommandAction));

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public Key? Key { get; set; }

        protected override void Invoke(object parameter)
        {
            // If a Key is specified, only proceed if it matches the event
            if (Key.HasValue)
            {
                if (!(parameter is KeyEventArgs e && e.Key == Key.Value))
                    return;

                e.Handled = true;
            }

            // FORCE BINDING UPDATES
            if (AssociatedObject is DatePicker dp)
            {
                // If user typed text, parse it. If they used the calendar, 
                // this ensures the UI value is pushed to the ViewModel immediately.
                if (!string.IsNullOrEmpty(dp.Text) && DateTime.TryParse(dp.Text, out DateTime parsed))
                {
                    dp.SelectedDate = parsed;
                }
                var binding = dp.GetBindingExpression(DatePicker.SelectedDateProperty);
                binding?.UpdateSource();
            }
            else if (AssociatedObject is TextBox tb)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }

            // Execute the command only AFTER the properties are updated
            if (Command?.CanExecute(null) == true)
            {
                Command.Execute(null);
            }
        }
    }
}
