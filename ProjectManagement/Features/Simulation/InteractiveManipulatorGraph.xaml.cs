using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    public partial class InteractiveManipulatorGraph : UserControl
    {
        public static readonly DependencyProperty StatusDateProperty =
            DependencyProperty.Register("StatusDate", typeof(DateTime), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(DateTime.Today, OnStatusDateChanged));
        public static readonly DependencyProperty DataPointsProperty =
            DependencyProperty.Register("DataPoints", typeof(IEnumerable<SimulationDataPoint>), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(null, OnDataPointsChanged));

        public static readonly DependencyProperty PlannedStartDateProperty =
            DependencyProperty.Register("PlannedStartDate", typeof(DateTime?), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(null, OnRedrawRequired));

        public static readonly DependencyProperty PlannedEndDateProperty =
            DependencyProperty.Register("PlannedEndDate", typeof(DateTime?), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(null, OnRedrawRequired));
        public static readonly DependencyProperty ViewStartDateProperty =
    DependencyProperty.Register("ViewStartDate", typeof(DateTime?), typeof(InteractiveManipulatorGraph),
        new PropertyMetadata(null, OnRedrawRequired));

        public static readonly DependencyProperty ViewEndDateProperty =
            DependencyProperty.Register("ViewEndDate", typeof(DateTime?), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(null, OnRedrawRequired));

        public DateTime? ViewStartDate { get => (DateTime?)GetValue(ViewStartDateProperty); set => SetValue(ViewStartDateProperty, value); }
        public DateTime? ViewEndDate { get => (DateTime?)GetValue(ViewEndDateProperty); set => SetValue(ViewEndDateProperty, value); }
        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register("EditMode", typeof(GraphEditMode), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(GraphEditMode.Progress, OnEditModeChanged));
        private static void OnRedrawRequired(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((InteractiveManipulatorGraph)d).UpdateLineAndThumbPositions();
        public DateTime? PlannedStartDate { get => (DateTime?)GetValue(PlannedStartDateProperty); set => SetValue(PlannedStartDateProperty, value); }
        public DateTime? PlannedEndDate { get => (DateTime?)GetValue(PlannedEndDateProperty); set => SetValue(PlannedEndDateProperty, value); }
        private Polyline _graphLine;
        private Polyline _baselineLine;
        private List<Thumb> _thumbs = new List<Thumb>();

        public DateTime StatusDate
        {
            get => (DateTime)GetValue(StatusDateProperty);
            set => SetValue(StatusDateProperty, value);
        }
        public IEnumerable<SimulationDataPoint> DataPoints
        {
            get => (IEnumerable<SimulationDataPoint>)GetValue(DataPointsProperty);
            set => SetValue(DataPointsProperty, value);
        }

        public GraphEditMode EditMode
        {
            get => (GraphEditMode)GetValue(EditModeProperty);
            set => SetValue(EditModeProperty, value);
        }

        public static readonly DependencyProperty MaxActualHoursProperty =
            DependencyProperty.Register("MaxActualHours", typeof(double), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(100.0, OnRedrawRequired)); private const double ThumbSize = 14;
        public double MaxActualHours
        {
            get => (double)GetValue(MaxActualHoursProperty);
            set => SetValue(MaxActualHoursProperty, value);
        }

        // Stores the dynamic upper limit (expands if Actuals > Planned)
        private double _currentRenderMaxHours = 100.0;
        private const double GraphMargin = 15;
        public InteractiveManipulatorGraph()
        {
            InitializeComponent();
        }
        private static void OnStatusDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
           ((InteractiveManipulatorGraph)d).UpdateLineAndThumbPositions();
        private static void OnDataPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var graph = (InteractiveManipulatorGraph)d;
            if (e.OldValue is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= graph.DataCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += graph.DataCollectionChanged;

            graph.RedrawGraph();
        }

        private static void OnEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var graph = (InteractiveManipulatorGraph)d;

            // REMOVE THIS LINE:
            // graph.YAxisLabel.Text = graph.EditMode == GraphEditMode.Progress ? "Progress %" : "Actual Hrs";

            // The unit logic is now handled dynamically inside UpdateLabels()
            // which is called by RedrawGraph() -> UpdateLineAndThumbPositions()
            graph.RedrawGraph();
        }

        private void DataCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => RedrawGraph();
        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawGraph();

        private void RedrawGraph()
        {
            // 1. Safety check: Don't draw if the control isn't ready or visible
            if (GraphCanvas.ActualWidth == 0 || GraphCanvas.ActualHeight == 0) return;

            GraphCanvas.Children.Clear();
            _thumbs.Clear();
            DrawBackgroundGrid();
            _baselineLine = new Polyline
            {
                Stroke = Brushes.DimGray,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Opacity = 0.5,
                Visibility = EditMode == GraphEditMode.Progress ? Visibility.Visible : Visibility.Collapsed
            };
            GraphCanvas.Children.Add(_baselineLine);
            if (DataPoints == null || !DataPoints.Any())
            {
                _graphLine = null;
                return;
            }

            var points = DataPoints.OrderBy(p => p.Date).ToList();

            // 2. Initialize the Polyline (The Connector)
            _graphLine = new Polyline
            {
                Stroke = EditMode == GraphEditMode.Progress
                    ? new SolidColorBrush(Color.FromRgb(255, 191, 0)) // Tactical Gold
                    : Brushes.LimeGreen,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Opacity = 0.8
            };

            // Always add the line first so it is behind the dots
            GraphCanvas.Children.Add(_graphLine);

            // 3. Initialize the Thumbs (The Dots)
            foreach (var point in points)
            {
                var thumb = new Thumb
                {
                    Width = ThumbSize,
                    Height = ThumbSize,
                    DataContext = point,
                    Cursor = Cursors.Hand,
                    Template = CreateThumbTemplate(EditMode)
                };
                thumb.DragDelta += Thumb_DragDelta;
                _thumbs.Add(thumb);
                GraphCanvas.Children.Add(thumb);
            }

            // 4. Force a position update now that everything is on the Canvas
            UpdateLineAndThumbPositions();
        }
        private void DrawBackgroundGrid()
        {
            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;

            // 4 segments (25, 50, 75, 100)
            for (int i = 1; i <= 4; i++)
            {
                double y = GetClampedY(i * 0.25, h);
                var gridLine = new Line
                {
                    X1 = 0,
                    X2 = w,
                    Y1 = y,
                    Y2 = y,
                    Stroke = (Brush)FindResource("TacticalSeparatorBrush"),
                    StrokeThickness = 0.5,
                    Opacity = 0.3
                };
                GraphCanvas.Children.Add(gridLine);
            }
        }
        private void UpdateLineAndThumbPositions()
        {
            if (GraphCanvas.ActualWidth == 0 || _graphLine == null) return;

            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            double usableWidth = w - (GraphMargin * 2);

            var sortedPoints = DataPoints?.OrderBy(p => p.Date).ToList();
            if (sortedPoints == null || sortedPoints.Count < 2) return;

            // ESTABLISH UNIFIED X-AXIS RANGE
            // Use user-defined View dates first, then fallback to Planned dates, then fallback to Data dates
            DateTime graphStart = ViewStartDate ?? PlannedStartDate ?? sortedPoints.First().Date;
            DateTime graphEnd = ViewEndDate ?? PlannedEndDate ?? sortedPoints.Last().Date;

            // Only auto-expand the END boundary if the user hasn't explicitly locked it
            if (!ViewEndDate.HasValue)
            {
                if (sortedPoints.Last().Date > graphEnd)
                    graphEnd = sortedPoints.Last().Date;
                if (StatusDate > graphEnd)
                    graphEnd = StatusDate;
            }

            // Only auto-expand the START boundary if the user hasn't explicitly locked it
            if (!ViewStartDate.HasValue)
            {
                if (sortedPoints.First().Date < graphStart)
                    graphStart = sortedPoints.First().Date;
                if (StatusDate < graphStart)
                    graphStart = StatusDate;
            }

            // Failsafe: Avoid division by zero and invert start/end if user enters them backwards
            if (graphStart >= graphEnd)
                graphEnd = graphStart.AddDays(1);

            double totalCalendarTicks = (graphEnd - graphStart).Ticks;

            _currentRenderMaxHours = MaxActualHours > 0 ? MaxActualHours : 100.0;

            // If the user drags Actual Hours higher than the plan, expand the axis dynamically
            if (sortedPoints.Any())
            {
                double maxDataHours = sortedPoints.Max(p => p.ActualHours);
                if (maxDataHours > _currentRenderMaxHours)
                    _currentRenderMaxHours = maxDataHours * 1.1; // Add 10% headroom
            }

            PointCollection linePoints = new PointCollection();

            for (int i = 0; i < sortedPoints.Count; i++)
            {
                var p1 = sortedPoints[i];
                double x1 = GraphMargin + (((p1.Date - graphStart).Ticks / totalCalendarTicks) * usableWidth);
                double val1 = EditMode == GraphEditMode.Progress ? p1.Progress : (p1.ActualHours / _currentRenderMaxHours);
                double y1 = GetClampedY(val1, h);

                // Add the actual dot position
                linePoints.Add(new Point(x1, y1));

                // ──── DRAW BUSINESS-DAY PATH TO NEXT DOT ────
                if (i < sortedPoints.Count - 1)
                {
                    var p2 = sortedPoints[i + 1];
                    double val2 = EditMode == GraphEditMode.Progress ? p2.Progress : (p2.ActualHours / _currentRenderMaxHours);
                    int segmentBusinessDays = WorkBreakdownItem.GetBusinessDaysSpan(p1.Date, p2.Date);

                    // Draw a point for every calendar day between dots to mirror the staircase
                    for (DateTime date = p1.Date.AddDays(1); date < p2.Date; date = date.AddDays(1))
                    {
                        double x = GraphMargin + (((date - graphStart).Ticks / totalCalendarTicks) * usableWidth);

                        int elapsedInSegment = WorkBreakdownItem.GetBusinessDaysSpan(p1.Date, date);
                        double t = segmentBusinessDays > 0 ? (double)elapsedInSegment / segmentBusinessDays : 0;

                        double interpolatedVal = val1 + (val2 - val1) * t;
                        linePoints.Add(new Point(x, GetClampedY(interpolatedVal, h)));
                    }
                }

                // Update the visual Thumb (Dot) position
                var thumb = _thumbs[i];
                Canvas.SetLeft(thumb, x1 - (ThumbSize / 2));
                Canvas.SetTop(thumb, y1 - (ThumbSize / 2));
            }

            _graphLine.Points = linePoints;

            // Pass the unified boundaries to the rest of the render functions
            UpdateBaselinePositions(w, h, graphStart, graphEnd, totalCalendarTicks);
            UpdateStatusLine(w, h, graphStart, graphEnd, totalCalendarTicks);
            UpdateLabels(w, h, graphStart, graphEnd, totalCalendarTicks);
        }
        private double GetClampedY(double normalizedValue, double canvasHeight)
        {
            // normalizedValue is 0.0 to 1.0
            // We map this to (canvasHeight - Margin) down to (Margin)
            double usableHeight = canvasHeight - (GraphMargin * 2);
            return canvasHeight - GraphMargin - (normalizedValue * usableHeight);
        }
        private void UpdateBaselinePositions(double width, double height, DateTime graphStart, DateTime graphEnd, double totalCalendarTicks)
        {
            if (_baselineLine == null || !PlannedStartDate.HasValue || !PlannedEndDate.HasValue) return;

            DateTime start = PlannedStartDate.Value;
            DateTime end = PlannedEndDate.Value;
            int totalBusinessDays = WorkBreakdownItem.GetBusinessDaysSpan(start, end);

            if (totalBusinessDays <= 0) return;

            PointCollection baselinePoints = new PointCollection();
            double usableWidth = width - (GraphMargin * 2);

            // Create a point for every calendar day to show the weekend "plateaus"
            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                // Align this element identically to the data dots timeline
                double x = GraphMargin + (((date - graphStart).Ticks / totalCalendarTicks) * usableWidth);

                int elapsedBusinessDays = WorkBreakdownItem.GetBusinessDaysSpan(start, date);
                double progress = (double)elapsedBusinessDays / totalBusinessDays;
                double y = GetClampedY(progress, height);

                baselinePoints.Add(new Point(x, y));
            }

            _baselineLine.Points = baselinePoints;
        }
        private void UpdateStatusLine(double width, double height, DateTime graphStart, DateTime graphEnd, double totalCalendarTicks)
        {
            var statusLine = GraphCanvas.Children.OfType<Line>().FirstOrDefault(l => (string)l.Tag == "StatusLine");

            if (statusLine == null)
            {
                statusLine = new Line
                {
                    Tag = "StatusLine",
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };

                Canvas.SetZIndex(statusLine, 10);
                GraphCanvas.Children.Add(statusLine);
            }

            double usableWidth = width - (GraphMargin * 2);
            double statusX = GraphMargin + (((StatusDate - graphStart).Ticks / totalCalendarTicks) * usableWidth);

            // Clamp line to canvas bounds for safety
            statusLine.X1 = statusLine.X2 = Math.Max(0, Math.Min(width, statusX));
            statusLine.Y1 = 0;
            statusLine.Y2 = height;

            statusLine.Opacity = (statusX < GraphMargin || statusX > width - GraphMargin) ? 0.3 : 1.0;
        }
        private void UpdateLabels(double width, double height, DateTime graphStart, DateTime graphEnd, double totalCalendarTicks)
        {
            YAxisCanvas.Children.Clear();
            XAxisCanvas.Children.Clear();

            // Y-Axis Ticks and Labels
            for (int i = 0; i <= 4; i++)
            {
                double percentage = i * 0.25;
                double y = GetClampedY(percentage, height);

                // Tick mark
                var tick = new Line { X1 = 45, X2 = 50, Y1 = y, Y2 = y, Stroke = Brushes.Gray, StrokeThickness = 1 };
                YAxisCanvas.Children.Add(tick);

                string labelText;
                if (EditMode == GraphEditMode.Progress)
                {
                    labelText = $"{percentage * 100}%"; // Show 0%, 25%, 50%, etc.
                }
                else
                {
                    double val = _currentRenderMaxHours * percentage;
                    labelText = $"{Math.Round(val, 0)}h"; // Show hours scaled to plan
                }

                var txt = new TextBlock
                {
                    Text = labelText,
                    Foreground = (Brush)FindResource("TacticalTextMutedBrush"),
                    FontSize = 10,
                    Width = 40,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetTop(txt, y - 7);
                Canvas.SetLeft(txt, 0);
                YAxisCanvas.Children.Add(txt);
            }

            // X-Axis Ticks and Labels (Dynamic based on Dashboard logic)
            double usableWidth = width - (GraphMargin * 2);
            double totalDays = (graphEnd - graphStart).TotalDays;

            if (totalDays <= 0) return;

            List<DateTime> labelDates = new List<DateTime>();

            if (totalDays < 60) // Less than 2 months: Show Weeks
            {
                for (DateTime d = graphStart.Date; d <= graphEnd.Date; d = d.AddDays(14)) // Every 2 weeks
                    labelDates.Add(d);
            }
            else if (totalDays < 365) // Less than a year: Show Months
            {
                DateTime iterator = new DateTime(graphStart.Year, graphStart.Month, 1);
                if (graphStart.Day > 15) iterator = iterator.AddMonths(1);

                while (iterator <= graphEnd.Date)
                {
                    labelDates.Add(iterator);
                    iterator = iterator.AddMonths(2); // Every 2 months
                }
            }
            else // Years: Show Years
            {
                DateTime iterator = new DateTime(graphStart.Year, 1, 1);
                if (graphStart.Month > 6) iterator = iterator.AddYears(1);

                while (iterator <= graphEnd.Date)
                {
                    labelDates.Add(iterator);
                    iterator = iterator.AddYears(1);
                }
            }

            foreach (var date in labelDates)
            {
                double daysFromStart = (date - graphStart).TotalDays;
                double percent = daysFromStart / totalDays;

                // Simple boundary check to prevent drawing out of bounds
                if (percent < 0 || percent > 1) continue;

                double x = GraphMargin + (percent * usableWidth);

                var tick = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = 5, Stroke = Brushes.Gray, StrokeThickness = 1 };
                XAxisCanvas.Children.Add(tick);

                string format = totalDays < 60 ? "MMM dd" : (totalDays < 365 ? "MMM" : "yyyy");
                var txt = new TextBlock { Text = date.ToString(format), FontSize = 10, Foreground = (Brush)FindResource("TacticalTextMutedBrush") };
                txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(txt, x - (txt.DesiredSize.Width / 2));
                Canvas.SetTop(txt, 8);
                XAxisCanvas.Children.Add(txt);
            }
        }
        private double GetYPosition(SimulationDataPoint point, double canvasHeight)
        {
            // Use local variable to avoid repeating logic
            double value = EditMode == GraphEditMode.Progress ? point.Progress : point.ActualHours;
            double max = EditMode == GraphEditMode.Progress ? 1.0 : MaxActualHours;

            // Safety check for MaxActualHours being 0
            if (max <= 0) return canvasHeight;

            // Clamp and calculate
            double clampedValue = Math.Max(0, Math.Min(max, value));

            // Map 0 -> canvasHeight, Max -> 0
            return canvasHeight - ((clampedValue / max) * canvasHeight);
        }
        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is SimulationDataPoint point)
            {
                double h = GraphCanvas.ActualHeight;
                double usableHeight = h - (GraphMargin * 2);

                double newTop = Canvas.GetTop(thumb) + e.VerticalChange;
                // Clamp within internal margins
                newTop = Math.Max(GraphMargin - (ThumbSize / 2), Math.Min(h - GraphMargin - (ThumbSize / 2), newTop));

                // Convert clamped Y back to 0.0 - 1.0
                double relativeY = newTop + (ThumbSize / 2) - GraphMargin;
                double val = 1.0 - (relativeY / usableHeight);
                val = Math.Max(0, Math.Min(1.0, val));

                if (EditMode == GraphEditMode.Progress) point.Progress = val;
                else point.ActualHours = val * _currentRenderMaxHours;

                UpdateLineAndThumbPositions();
            }
        }

        private ControlTemplate CreateThumbTemplate(GraphEditMode mode)
        {
            Brush fillBrush = mode == GraphEditMode.Progress ? (Brush)FindResource("TacticalPrimaryBrush") : Brushes.LimeGreen;
            var template = new ControlTemplate(typeof(Thumb));
            var factory = new FrameworkElementFactory(typeof(Ellipse));
            factory.SetValue(Shape.FillProperty, fillBrush);
            factory.SetValue(Shape.StrokeProperty, Brushes.White);
            factory.SetValue(Shape.StrokeThicknessProperty, 2.0);
            template.VisualTree = factory;
            return template;
        }
    }
}
