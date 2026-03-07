using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfResourceGantt.ProjectManagement.Services;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    /// <summary>
    /// A Canvas overlay that draws clean right-angle dependency arrows
    /// between predecessor and successor Gantt bars.
    /// 
    /// Each arrow goes: predecessor EndDate → right → down → successor StartDate → arrowhead.
    /// Uses the standard "elbow connector" pattern from MS Project / Primavera.
    /// </summary>
    public class DependencyArrowCanvas : Canvas
    {
        // ── Dependency Properties (bound from XAML) ──────────────────────────

        public static readonly DependencyProperty WorkItemsProperty =
            DependencyProperty.Register("WorkItems", typeof(ObservableCollection<WorkItem>),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(null, OnWorkItemsChanged));

        public ObservableCollection<WorkItem> WorkItems
        {
            get => (ObservableCollection<WorkItem>)GetValue(WorkItemsProperty);
            set => SetValue(WorkItemsProperty, value);
        }

        public static readonly DependencyProperty TimelineStartProperty =
            DependencyProperty.Register("TimelineStart", typeof(DateTime),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(DateTime.Today, OnTimelineChanged));

        public DateTime TimelineStart
        {
            get => (DateTime)GetValue(TimelineStartProperty);
            set => SetValue(TimelineStartProperty, value);
        }

        public static readonly DependencyProperty TimelineEndProperty =
            DependencyProperty.Register("TimelineEnd", typeof(DateTime),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(DateTime.Today.AddMonths(6), OnTimelineChanged));

        public DateTime TimelineEnd
        {
            get => (DateTime)GetValue(TimelineEndProperty);
            set => SetValue(TimelineEndProperty, value);
        }

        public static readonly DependencyProperty TimelineWidthProperty =
            DependencyProperty.Register("TimelineWidth", typeof(double),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(800.0, OnTimelineChanged));

        public double TimelineWidth
        {
            get => (double)GetValue(TimelineWidthProperty);
            set => SetValue(TimelineWidthProperty, value);
        }

        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register("HorizontalOffset", typeof(double),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(0.0, OnTimelineChanged));

        public double HorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register("RowHeight", typeof(double),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(36.0));

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public static readonly DependencyProperty TreeViewReferenceProperty =
            DependencyProperty.Register("TreeViewReference", typeof(TreeView),
                typeof(DependencyArrowCanvas),
                new PropertyMetadata(null));

        public TreeView TreeViewReference
        {
            get => (TreeView)GetValue(TreeViewReferenceProperty);
            set => SetValue(TreeViewReferenceProperty, value);
        }

        // ── Arrow styling constants ─────────────────────────────────────────

        private static readonly SolidColorBrush NormalArrowBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)); // Slate-500
        private static readonly SolidColorBrush WarningArrowBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)); // Amber-600
        private static readonly SolidColorBrush CriticalArrowBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red-500
        private const double ArrowThickness = 1.5;
        private const double ArrowHeadSize = 6;
        private const double ElbowGap = 8; // px gap before turning down

        // ── Property Changed Handlers ───────────────────────────────────────

        private static void OnWorkItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DependencyArrowCanvas canvas)
                canvas.InvalidateVisual();
        }

        private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DependencyArrowCanvas canvas)
                canvas.InvalidateVisual();
        }

        /// <summary>
        /// Call this from code-behind whenever the TreeView layout changes
        /// (scroll, expand/collapse, resize, etc.)
        /// </summary>
        public void Refresh()
        {
            InvalidateVisual();
        }

        // ── Core Rendering ──────────────────────────────────────────────────

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var items = WorkItems;
            if (items == null || !items.Any()) return;
            if (TimelineWidth <= 0) return;
            if (TreeViewReference == null) return;

            // Build a flat list of all visible items with their visual Y positions
            var visibleItems = new List<(WorkItem Item, double YCenter)>();
            CollectVisibleItems(items, visibleItems);

            if (!visibleItems.Any()) return;

            // Build lookup: Id → (item, yCenter)
            var lookup = new Dictionary<string, (WorkItem Item, double Y)>();
            foreach (var (item, y) in visibleItems)
            {
                if (!string.IsNullOrEmpty(item.Id) && !lookup.ContainsKey(item.Id))
                    lookup[item.Id] = (item, y);
            }

            // For each item with predecessors, draw arrows
            foreach (var (successor, succY) in visibleItems)
            {
                if (string.IsNullOrWhiteSpace(successor.Predecessors)) continue;

                var deps = PredecessorParser.Parse(successor.Predecessors);
                foreach (var dep in deps)
                {
                    // Try to find predecessor by ID or WBS
                    (WorkItem Item, double Y) predInfo = default;
                    bool found = false;

                    if (!string.IsNullOrEmpty(dep.PredecessorId))
                    {
                        // Try exact ID match
                        if (lookup.TryGetValue(dep.PredecessorId, out predInfo))
                            found = true;
                        else
                        {
                            // Try WBS match
                            var wbsMatch = visibleItems.FirstOrDefault(v => v.Item.WbsValue == dep.PredecessorId);
                            if (wbsMatch.Item != null)
                            {
                                predInfo = (wbsMatch.Item, wbsMatch.YCenter);
                                found = true;
                            }
                        }
                    }

                    if (!found) continue;

                    DrawDependencyArrow(dc, predInfo.Item, predInfo.Y, successor, succY, dep);
                }
            }
        }

        /// <summary>
        /// Recursively collects visible work items and their Y-center positions
        /// relative to the TreeView by walking the visual tree.
        /// </summary>
        private void CollectVisibleItems(IEnumerable<WorkItem> items, List<(WorkItem, double)> result)
        {
            if (TreeViewReference == null) return;

            var flatList = FlattenVisible(items);
            foreach (var item in flatList)
            {
                var container = FindContainerForItem(TreeViewReference, item);
                if (container == null) continue;

                // Get the Y position of the container relative to the TreeView
                try
                {
                    var transform = container.TransformToAncestor(TreeViewReference);
                    var point = transform.Transform(new Point(0, 0));
                    double yCenter = point.Y + container.ActualHeight / 2.0;
                    result.Add((item, yCenter));
                }
                catch
                {
                    // Container might not be in the visual tree (virtualized)
                }
            }
        }

        /// <summary>
        /// Flattens the tree into a list of visible items (respecting IsExpanded).
        /// </summary>
        private List<WorkItem> FlattenVisible(IEnumerable<WorkItem> items)
        {
            var result = new List<WorkItem>();
            foreach (var item in items)
            {
                if (!item.IsVisible) continue;
                result.Add(item);
                if (item.IsExpanded && item.Children != null && item.Children.Any())
                    result.AddRange(FlattenVisible(item.Children));
            }
            return result;
        }

        /// <summary>
        /// Finds the TreeViewItem container for a given data item.
        /// </summary>
        private TreeViewItem FindContainerForItem(ItemsControl parent, object item)
        {
            if (parent == null) return null;

            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null) return container;

            // Search recursively through expanded items
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var childContainer = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer == null) continue;

                if (childContainer.IsExpanded)
                {
                    var found = FindContainerForItem(childContainer, item);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Draws a single FS dependency arrow with an elbow connector:
        /// 
        ///   [Predecessor Bar]──┐
        ///                      │
        ///                      └──▶[Successor Bar]
        /// </summary>
        private void DrawDependencyArrow(DrawingContext dc, WorkItem pred, double predY,
            WorkItem succ, double succY, DependencyInfo dep)
        {
            // Calculate X positions based on dates
            double predEndX = DateToX(pred.EndDate);
            double succStartX = DateToX(succ.StartDate);

            // Apply horizontal scroll offset
            predEndX -= HorizontalOffset;
            succStartX -= HorizontalOffset;

            // Determine arrow color from worst schedule health of pred/succ
            var worstHealth = (MetricStatus)Math.Max((int)pred.ScheduleHealth, (int)succ.ScheduleHealth);
            Brush brush;
            switch (worstHealth)
            {
                case MetricStatus.Bad:
                    brush = CriticalArrowBrush;
                    break;
                case MetricStatus.Warning:
                    brush = WarningArrowBrush;
                    break;
                default:
                    brush = NormalArrowBrush;
                    break;
            }
            var pen = new Pen(brush, ArrowThickness);
            pen.Freeze();

            // Build the elbow path
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                if (Math.Abs(predY - succY) < 2)
                {
                    // Same row (shouldn't happen with FS, but handle gracefully)
                    ctx.BeginFigure(new Point(predEndX, predY), false, false);
                    ctx.LineTo(new Point(succStartX, succY), true, false);
                }
                else
                {
                    // Standard elbow: right → down/up → right → arrowhead
                    double midX = predEndX + ElbowGap;

                    // If successor starts to the left of predecessor end (overlap),
                    // route the connector further right to avoid crossing the bar
                    if (succStartX < midX)
                        midX = predEndX + ElbowGap;

                    ctx.BeginFigure(new Point(predEndX, predY), false, false);
                    ctx.LineTo(new Point(midX, predY), true, false);       // → right from pred end
                    ctx.LineTo(new Point(midX, succY), true, false);       // ↓ down to successor row
                    ctx.LineTo(new Point(succStartX, succY), true, false); // → right to successor start
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);

            // Draw arrowhead (pointing right →)
            DrawArrowHead(dc, succStartX, succY, brush);
        }

        /// <summary>
        /// Draws a small filled arrowhead pointing right at the given position.
        /// </summary>
        private void DrawArrowHead(DrawingContext dc, double tipX, double tipY, Brush brush)
        {
            var arrowHead = new StreamGeometry();
            using (var ctx = arrowHead.Open())
            {
                ctx.BeginFigure(new Point(tipX, tipY), true, true);
                ctx.LineTo(new Point(tipX - ArrowHeadSize, tipY - ArrowHeadSize / 2), true, false);
                ctx.LineTo(new Point(tipX - ArrowHeadSize, tipY + ArrowHeadSize / 2), true, false);
            }
            arrowHead.Freeze();
            dc.DrawGeometry(brush, null, arrowHead);
        }

        /// <summary>
        /// Converts a date to an X-pixel position on the timeline.
        /// </summary>
        private double DateToX(DateTime date)
        {
            double totalDays = (TimelineEnd - TimelineStart).TotalDays;
            if (totalDays <= 0) return 0;

            double dayOffset = (date - TimelineStart).TotalDays;
            return (dayOffset / totalDays) * TimelineWidth;
        }
    }
}
