using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Features.SystemManagement
{
    public static class TextBoxExtensions
    {
        // ==========================================
        // CLEAR FOCUS ON CLICK (Steals focus from TextBoxes)
        // ==========================================
        public static readonly DependencyProperty ClearFocusOnClickProperty =
            DependencyProperty.RegisterAttached(
                "ClearFocusOnClick",
                typeof(bool),
                typeof(TextBoxExtensions),
                new PropertyMetadata(false, OnClearFocusOnClickChanged));

        public static void SetClearFocusOnClick(DependencyObject obj, bool value)
            => obj.SetValue(ClearFocusOnClickProperty, value);

        public static bool GetClearFocusOnClick(DependencyObject obj)
            => (bool)obj.GetValue(ClearFocusOnClickProperty);

        private static void OnClearFocusOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                    fe.PreviewMouseDown += Fe_ClearFocus_PreviewMouseDown;
                else
                    fe.PreviewMouseDown -= Fe_ClearFocus_PreviewMouseDown;
            }
        }

        private static void Fe_ClearFocus_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                var src = e.OriginalSource as DependencyObject;
                bool isFocusableControl = false;

                // Climb the visual tree to see if they clicked a native focusable control
                while (src != null && src != fe)
                {
                    if (src is TextBox || src is ComboBox || src is Button || src is DatePicker || src is ToggleButton)
                    {
                        isFocusableControl = true;
                        break;
                    }

                    DependencyObject parent = null;
                    if (src is Visual || src is System.Windows.Media.Media3D.Visual3D)
                        parent = VisualTreeHelper.GetParent(src);

                    if (parent == null && src is FrameworkElement frameworkElement)
                        parent = frameworkElement.Parent;

                    src = parent;
                }

                // If they clicked empty background space, force the Grid to steal focus!
                if (!isFocusableControl)
                {
                    fe.Focus();
                }
            }
        }

        // ==========================================
        // PROPAGATE ENTER AS TAB (For generic routing)
        // ==========================================
        public static readonly DependencyProperty PropagateEnterAsTabProperty =
            DependencyProperty.RegisterAttached(
                "PropagateEnterAsTab",
                typeof(bool),
                typeof(TextBoxExtensions),
                new PropertyMetadata(false, OnPropagateEnterAsTabChanged));

        public static void SetPropagateEnterAsTab(DependencyObject obj, bool value)
            => obj.SetValue(PropagateEnterAsTabProperty, value);

        public static bool GetPropagateEnterAsTab(DependencyObject obj)
            => (bool)obj.GetValue(PropagateEnterAsTabProperty);

        private static void OnPropagateEnterAsTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb)
            {
                if ((bool)e.NewValue) tb.KeyDown += OnKeyDown;
                else tb.KeyDown -= OnKeyDown;
            }
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is FrameworkElement fe)
            {
                fe.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                fe.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        // ==========================================
        // COMMIT ON LOST FOCUS (Safe for DatePicker & TextBox)
        // ==========================================
        public static readonly DependencyProperty CommitOnLostFocusProperty =
            DependencyProperty.RegisterAttached(
                "CommitOnLostFocus",
                typeof(ICommand),
                typeof(TextBoxExtensions),
                new PropertyMetadata(null, OnCommitOnLostFocusChanged));

        public static void SetCommitOnLostFocus(DependencyObject obj, ICommand value)
            => obj.SetValue(CommitOnLostFocusProperty, value);

        public static ICommand GetCommitOnLostFocus(DependencyObject obj)
            => (ICommand)obj.GetValue(CommitOnLostFocusProperty);

        private static void OnCommitOnLostFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DatePicker dp)
            {
                dp.LostKeyboardFocus -= Dp_LostKeyboardFocus;
                if (e.NewValue is ICommand) dp.LostKeyboardFocus += Dp_LostKeyboardFocus;
            }
            else if (d is TextBox tb)
            {
                tb.LostKeyboardFocus -= Tb_LostKeyboardFocus;
                if (e.NewValue is ICommand) tb.LostKeyboardFocus += Tb_LostKeyboardFocus;
            }
        }

        private static void Tb_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                var command = GetCommitOnLostFocus(tb);
                if (command != null)
                {
                    // Allow jumping between the Number/Name boxes without closing edit mode!
                    if (e.NewFocus is TextBox nextTb &&
                       (nextTb.Tag?.ToString() == "Name" || nextTb.Tag?.ToString() == "No"))
                    {
                        return;
                    }

                    tb.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (command.CanExecute(null)) command.Execute(null);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private static void Dp_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                if (dp.IsDropDownOpen) return;
                var command = GetCommitOnLostFocus(dp);
                if (command != null)
                {
                    dp.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!dp.IsDropDownOpen && !dp.IsKeyboardFocusWithin && command.CanExecute(null))
                            command.Execute(null);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        // ==========================================
        // COMMIT ON ENTER (Safe for DatePicker & TextBox)
        // ==========================================
        public static readonly DependencyProperty CommitOnEnterProperty =
            DependencyProperty.RegisterAttached(
                "CommitOnEnter",
                typeof(ICommand),
                typeof(TextBoxExtensions),
                new PropertyMetadata(null, OnCommitOnEnterChanged));

        public static void SetCommitOnEnter(DependencyObject obj, ICommand value)
            => obj.SetValue(CommitOnEnterProperty, value);

        public static ICommand GetCommitOnEnter(DependencyObject obj)
            => (ICommand)obj.GetValue(CommitOnEnterProperty);

        private static void OnCommitOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DatePicker dp)
            {
                dp.PreviewKeyDown -= Dp_PreviewKeyDown;
                if (e.NewValue is ICommand) dp.PreviewKeyDown += Dp_PreviewKeyDown;
            }
            else if (d is TextBox tb)
            {
                tb.PreviewKeyDown -= Tb_PreviewKeyDown;
                if (e.NewValue is ICommand) tb.PreviewKeyDown += Tb_PreviewKeyDown;
            }
        }

        private static void Tb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                var command = GetCommitOnEnter(tb);

                if (command != null)
                {
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    tb.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (command.CanExecute(null)) command.Execute(null);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                e.Handled = true;
            }
        }

        private static void Dp_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is DatePicker dp)
            {
                var textBox = dp.Template.FindName("PART_TextBox", dp) as DatePickerTextBox;
                if (textBox != null && textBox.IsKeyboardFocusWithin)
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text)) dp.SelectedDate = null;
                    else if (DateTime.TryParse(textBox.Text, out DateTime parsedDate)) dp.SelectedDate = parsedDate;

                    dp.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();

                    var command = GetCommitOnEnter(dp);
                    if (command != null)
                    {
                        dp.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (command.CanExecute(null)) command.Execute(null);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    e.Handled = true;
                }
            }
        }
    }
}
