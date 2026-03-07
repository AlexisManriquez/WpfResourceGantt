using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    public partial class GateProgressView : UserControl
    {
        public GateProgressView()
        {
            InitializeComponent();

            // -------------------------------------------------------
            // BUG FIX (Issues 2 & 3 – hover-selects / stuck selection):
            //
            // The per-row PreviewMouseLeftButtonUp Interaction.Trigger only fires
            // when the mouse button goes up while the cursor is over THAT specific
            // border.  When the user click-drags across several rows and releases
            // over a *different* row, the originating row's trigger never fires and
            // _isDraggingSelection stays true forever.  From that point on, every
            // MouseEnter fires HoverSelectionCommand and silently selects rows.
            //
            // Subscribing at the UserControl level guarantees the flag is cleared
            // no matter where inside the view the button is released.
            // -------------------------------------------------------
            this.PreviewMouseLeftButtonUp += OnViewMouseLeftButtonUp;
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GateProgressViewModel oldVm)
            {
                UnsubscribeRecursively(oldVm.RootRows);
            }
            if (e.NewValue is GateProgressViewModel newVm)
            {
                SubscribeRecursively(newVm.RootRows);
            }
        }

        private void SubscribeRecursively(System.Collections.ObjectModel.ObservableCollection<GateRowViewModel> collection)
        {
            if (collection == null) return;
            collection.CollectionChanged += OnCollectionChanged;
            foreach (var item in collection)
            {
                SubscribeRecursively(item.Children);
            }
        }

        private void UnsubscribeRecursively(System.Collections.ObjectModel.ObservableCollection<GateRowViewModel> collection)
        {
            if (collection == null) return;
            collection.CollectionChanged -= OnCollectionChanged;
            foreach (var item in collection)
            {
                UnsubscribeRecursively(item.Children);
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (GateRowViewModel newItem in e.NewItems)
                {
                    // Recursively listen to children of the new row
                    SubscribeRecursively(newItem.Children);

                    // If the VM marked it as selected (which our AddBlock/AddItem logic does), focus it
                    if (newItem.IsSelected)
                    {
                        FocusRow(newItem);
                    }
                }
            }
        }

        private async void FocusRow(GateRowViewModel row)
        {
            // Give WPF a moment to generate the visual container (DataTemplate expansion)
            await Task.Delay(100);

            var container = FindContainerForRow(this, row);
            if (container != null)
            {
                var textBox = FindVisualChild<TextBox>(container);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
        }

        private DependencyObject FindContainerForRow(DependencyObject parent, GateRowViewModel row)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.DataContext == row)
                {
                    return fe;
                }
                var result = FindContainerForRow(child, row);
                if (result != null) return result;
            }
            return null;
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Ensures _isDraggingSelection is always reset when the left mouse button
        /// is released anywhere inside this view.
        /// </summary>
        private void OnViewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GateProgressViewModel vm)
                vm.CancelDragSelection();
        }

        // -----------------------------------------------------------------------
        // BUG FIX (Issue 1 – grip drag moves only one row):
        //
        // PreviewMouseLeftButtonDown is a TUNNELING event that travels root → leaf.
        // The row Border is an ANCESTOR of the grip Border, so the Border's
        // Interaction.Trigger fires StartDragSelectionCommand (→ ClearAllSelection)
        // BEFORE Grip_PreviewMouseLeftButtonDown even runs.  By the time we get
        // here, the multi-selection is already gone.
        //
        // The fix uses a paired-event trick:
        //   • The XAML row trigger is changed from PreviewMouseLeftButtonDown
        //     to MouseLeftButtonDown (bubbling).
        //   • Here we set e.Handled = true on the TUNNELING phase.
        //   • WPF propagates the Handled flag to the paired bubbling event, so
        //     when MouseLeftButtonDown bubbles up to the row Border the
        //     Interaction.Trigger sees Handled = true and skips it.
        //   → Multi-selection survives into the grip drag. ✅
        // -----------------------------------------------------------------------
        private void Grip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is GateRowViewModel item)
            {
                if (DataContext is GateProgressViewModel vm)
                {
                    if (!item.IsSelected)
                    {
                        // Grip clicked on an unselected row – clear others, select only this one.
                        var selectedItems = vm.GetSelectedRows();
                        foreach (var s in selectedItems) s.IsSelected = false;
                        item.IsSelected = true;
                    }

                    // Stop any in-progress paint-selection so HoverSelectionCommand
                    // does not accidentally modify the selection during the drag.
                    vm.CancelDragSelection();
                }

                // Mark handled on the tunneling event so the paired bubbling
                // MouseLeftButtonDown is also raised as Handled, preventing the
                // row Border's StartDragSelectionCommand from clearing our selection.
                e.Handled = true;
            }
        }

        private void Grip_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element)
            {
                if (DataContext is GateProgressViewModel vm)
                {
                    // Snapshot selection first, then stop paint-selection mode.
                    var selectedItems = vm.GetSelectedRows();
                    vm.CancelDragSelection();

                    var clickedItem = element.Tag as GateRowViewModel;

                    // Safety: if the grip's own item is somehow not in the selection yet, make it the sole item.
                    if (clickedItem != null && !selectedItems.Contains(clickedItem))
                    {
                        foreach (var s in selectedItems) s.IsSelected = false;
                        clickedItem.IsSelected = true;
                        selectedItems = new System.Collections.Generic.List<GateRowViewModel> { clickedItem };
                    }

                    if (selectedItems.Any())
                    {
                        // DoDragDrop blocks synchronously until the drop or escape.
                        // The WPF drag framework captures and swallows the mouse-up event,
                        // so the UserControl-level OnViewMouseLeftButtonUp handler won't
                        // see it.  Call CancelDragSelection explicitly as a safety net.
                        DragDrop.DoDragDrop(element, selectedItems, DragDropEffects.Move);
                        vm.CancelDragSelection();
                    }
                }
            }
        }

        private void Row_DragOver(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.DataContext is GateRowViewModel targetVm)
            {
                var sources = e.Data.GetData(typeof(System.Collections.Generic.List<GateRowViewModel>))
                    as System.Collections.Generic.List<GateRowViewModel>;

                if (sources != null && sources.All(s => s.Parent == targetVm.Parent) && !sources.Contains(targetVm))
                {
                    e.Effects = DragDropEffects.Move;
                    var pos = e.GetPosition(border);

                    var top = FindVisualChildByName<Border>(border, "TopLine");
                    var bot = FindVisualChildByName<Border>(border, "BottomLine");

                    if (top != null && bot != null)
                    {
                        if (pos.Y < border.ActualHeight / 2)
                        {
                            top.Visibility = Visibility.Visible;
                            bot.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            top.Visibility = Visibility.Collapsed;
                            bot.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    Row_DragLeave(sender, e);
                }
            }
            e.Handled = true;
        }

        private void Row_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                var top = FindVisualChildByName<Border>(border, "TopLine");
                var bot = FindVisualChildByName<Border>(border, "BottomLine");
                if (top != null) top.Visibility = Visibility.Collapsed;
                if (bot != null) bot.Visibility = Visibility.Collapsed;
            }
        }

        private void Row_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.DataContext is GateRowViewModel targetVm)
            {
                Row_DragLeave(sender, e);
                var sources = e.Data.GetData(typeof(System.Collections.Generic.List<GateRowViewModel>))
                    as System.Collections.Generic.List<GateRowViewModel>;

                if (sources != null && sources.All(s => s.Parent == targetVm.Parent) && !sources.Contains(targetVm))
                {
                    var pos = e.GetPosition(border);
                    bool after = pos.Y >= border.ActualHeight / 2;

                    if (DataContext is GateProgressViewModel vm)
                    {
                        vm.MoveRowCommand.Execute(new GateProgressViewModel.ReorderParams
                        {
                            Sources = sources,
                            Target = targetVm,
                            After = after
                        });
                    }
                }
            }
        }

        private T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;
                var result = FindVisualChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    // Force the binding to update immediately
                    var be = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                    be?.UpdateSource();

                    // Move focus away to "confirm" the edit
                    Keyboard.ClearFocus();
                    this.Focus();

                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Tab)
            {
                var element = sender as UIElement;
                if (element != null)
                {
                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
        }
    }
}
