using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfResourceGantt.ProjectManagement.Adorners;
using System.Windows.Threading;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    using System.Windows.Controls.Primitives;
    using WpfResourceGantt.ProjectManagement;

    /// <summary>
    /// Interaction logic for GanttView.xaml
    /// </summary>
    public partial class GanttView : UserControl
    {
        private Point _startPoint;
        private DragAdorner _dragAdorner;
        private AdornerLayer _adornerLayer;
        private WorkItem _draggedItem;
        private TreeViewItem _lastDropTarget;
        private string _lastDropPosition;
        private bool _isInitialFitPerformed = false;
        private DispatcherTimer _arrowRefreshTimer;
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register("HorizontalOffset", typeof(double), typeof(GanttView), new PropertyMetadata(0.0));

        public double HorizontalOffset
        {
            get { return (double)GetValue(HorizontalOffsetProperty); }
            set
            {
                SetValue(
HorizontalOffsetProperty, value);
            }
        }
        public GanttView()
        {
            InitializeComponent();
            this.Loaded += GanttView_Loaded;
            this.Unloaded += GanttView_Unloaded;

            // Debounce timer: coalesces rapid layout updates into a single arrow redraw
            _arrowRefreshTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _arrowRefreshTimer.Tick += (s, e) =>
            {
                _arrowRefreshTimer.Stop();
                DependencyArrows?.Refresh();
            };
        }
        private void GanttView_Loaded(object sender, RoutedEventArgs e)
        {
            if (GanttTree != null)
            {
                GanttTree.SizeChanged -= GanttTree_SizeChanged;
                GanttTree.SizeChanged += GanttTree_SizeChanged;

                // Refresh arrows on any layout change (expand/collapse)
                GanttTree.LayoutUpdated -= GanttTree_LayoutUpdated;
                GanttTree.LayoutUpdated += GanttTree_LayoutUpdated;

                // FIX: Force a layout pass and then perform the initial fit.
                // We use a lower priority (Background) to ensure the TreeView has 
                // computed its ActualWidth before we try to fit to it.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isInitialFitPerformed)
                    {
                        FitGanttToScreen();
                        _isInitialFitPerformed = true;
                    }
                    RefreshDependencyArrows();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }


        private void GanttView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (GanttTree != null)
            {
                GanttTree.SizeChanged -= GanttTree_SizeChanged;
                GanttTree.LayoutUpdated -= GanttTree_LayoutUpdated;
            }
        }

        private void GanttTree_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only auto-fit if the window size actually changed (User resized the window)
            // and NOT during the initial load (which is handled by the Loaded event).
            if (e.WidthChanged && _isInitialFitPerformed && e.PreviousSize.Width > 0)
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 50)
                {
                    FitGanttToScreen();
                }
            }
        }

        private void GanttTree_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // We sync the header and the frozen columns based on the horizontal offset
            // of the ScrollViewer internal to the TreeView.

            // 1. Always update the HorizontalOffset for the Frozen Columns (Task Name, SV, CV)
            // This handles the TranslateTransform logic
            this.HorizontalOffset = e.HorizontalOffset;

            // 2. Force the Timeline Header ScrollViewer to the same position
            if (TimelineHeaderScrollViewer != null)
            {
                // This keeps the dates (Years/Months) in sync with the bars
                TimelineHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            }

            // 3. Refresh dependency arrows (handles vertical scroll too)
            RefreshDependencyArrows();
        }

        private void GanttTree_LayoutUpdated(object sender, EventArgs e)
        {
            // Debounce: restart the timer on each layout update
            // This prevents hundreds of redraws per second during expand/collapse animations
            _arrowRefreshTimer.Stop();
            _arrowRefreshTimer.Start();
        }

        private void RefreshDependencyArrows()
        {
            DependencyArrows?.Refresh();
        }

        private void FitGanttToScreen()
        {
            if (DataContext is GanttViewModel vm && GanttTree != null)
            {
                // Force the UI to update its layout so ActualWidth is accurate
                GanttTree.UpdateLayout();

                double currentWidth = GanttTree.ActualWidth;

                // Ensure we have a valid width before fitting
                if (currentWidth > 500)
                {
                    vm.PerformFit(currentWidth);
                }
            }
        }
        // --- DRAG START ---
        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (IsClickOnToggle(e.OriginalSource as DependencyObject, sender as DependencyObject))
            {
                _draggedItem = null;
                return;
            }

            // 1. Handle Double Click Navigation
            // We check ClickCount == 2 inside the Down event to reliably catch double clicks
            // before the TreeViewItem swallows them.
            if (e.ClickCount == 2)
            {
                /*
                if ((sender as FrameworkElement)?.DataContext is WorkItem clickedWorkItem && clickedWorkItem.IsLeaf)
                {
                    var ganttViewModel = DataContext as GanttViewModel;
                    var mainViewModel = ganttViewModel?.MainViewModel;
                    if (mainViewModel != null)
                    {
                        mainViewModel.NavigateToTaskDetailsView(clickedWorkItem, mainViewModel.CurrentUser);
                        
                        // Mark as handled to prevent the TreeViewItem from processing this as an expand/collapse toggle
                        e.Handled = true; 
                        return;
                    }
                }
                */
            }

            // 2. Initialize Drag (only if not handled above)
            _startPoint = e.GetPosition(null);
            _draggedItem = (sender as FrameworkElement)?.DataContext as WorkItem;
        }

        private bool IsClickOnToggle(DependencyObject clickedElement, DependencyObject treeViewItem)
        {
            // Walk up the visual tree from the clicked element until we hit the TreeViewItem or null
            while (clickedElement != null && clickedElement != treeViewItem)
            {
                if (clickedElement is System.Windows.Controls.Primitives.ToggleButton)
                {
                    return true; // We clicked a toggle button!
                }
                clickedElement = VisualTreeHelper.GetParent(clickedElement);
            }
            return false;
        }

        private void TreeViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Initialize the drag operation
                    _adornerLayer = AdornerLayer.GetAdornerLayer(this);

                    // Create the adorner
                    _dragAdorner = new DragAdorner(this, _draggedItem);
                    _adornerLayer.Add(_dragAdorner);

                    // Perform the drag-and-drop
                    DragDrop.DoDragDrop(sender as DependencyObject, _draggedItem, DragDropEffects.Move);

                    // Cleanup after drag ends
                    // FIX: Check if _dragAdorner is not null before removing it
                    if (_adornerLayer != null && _dragAdorner != null)
                    {
                        _adornerLayer.Remove(_dragAdorner);
                        _dragAdorner = null;
                    }
                    _draggedItem = null;
                }
            }
        }

        // --- DRAG OVER (VISUAL FEEDBACK) ---
        private TreeViewItem GetNearestContainer(UIElement element)
        {
            // Walk up the visual tree to find the nearest TreeViewItem.
            var container = element as TreeViewItem;
            while (container == null && element != null)
            {
                element = VisualTreeHelper.GetParent(element) as UIElement;
                container = element as TreeViewItem;
            }
            return container;
        }

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            // --- This method's logic is now simpler ---

            var targetItem = GetNearestContainer(e.OriginalSource as UIElement);

            // Clear the tag of the old target if we've moved to a new one
            if (_lastDropTarget != null && _lastDropTarget != targetItem)
            {
                _lastDropTarget.Tag = null;
            }

            if (targetItem == null)
            {
                _lastDropPosition = null; // We are not over a valid target
                return;
            }

            _lastDropTarget = targetItem;

            // --- Calculate new position ---
            Point position = e.GetPosition(targetItem);
            double oneThird = targetItem.ActualHeight / 3;
            string newPositionTag = null;

            if (position.Y < oneThird)
            {
                newPositionTag = "DropBefore";
            }
            else if (position.Y > targetItem.ActualHeight - oneThird)
            {
                newPositionTag = "DropAfter";
            }

            // Update the visual tag and our "sticky" field
            targetItem.Tag = newPositionTag;
            _lastDropPosition = newPositionTag; // This is the important part

            e.Handled = true;
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            // --- THIS METHOD IS NOW MUCH MORE ROBUST ---

            var draggedWorkItem = e.Data.GetData(typeof(WorkItem)) as WorkItem;

            // Use the last known valid target and position. These will not be null
            // if the user dropped on a valid blue line.
            var targetWorkItem = _lastDropTarget?.DataContext as WorkItem;
            string dropPosition = _lastDropPosition;

            // --- Cleanup ---
            // Clear the visual tags on all relevant items.
            if (_lastDropTarget != null) _lastDropTarget.Tag = null;
            var sourceItem = sender as TreeViewItem;
            if (sourceItem != null) sourceItem.Tag = null;
            _lastDropPosition = null; // Reset for the next drag operation

            // --- Validation ---
            if (targetWorkItem == null || draggedWorkItem == null || targetWorkItem == draggedWorkItem || dropPosition == null)
            {
                return;
            }

            // --- Call the ViewModel ---
            var viewModel = DataContext as GanttViewModel;
            if (viewModel == null) return;

            viewModel.ReorderItems(draggedWorkItem.Id, targetWorkItem.Id, dropPosition);
        }

        // Helper to find the Border inside the TreeViewItem's template
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // NEW: Handles the Column Header Dropdown Click
        private void HeaderDropdown_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.ContextMenu != null)
            {
                // Ensure the menu appears attached to the button
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
