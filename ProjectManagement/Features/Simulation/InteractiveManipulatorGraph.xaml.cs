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

        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register("EditMode", typeof(GraphEditMode), typeof(InteractiveManipulatorGraph),
                new PropertyMetadata(GraphEditMode.Progress, OnEditModeChanged));
        private Polyline _graphLine;
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

        public double MaxActualHours { get; set; } = 500; // Dynamic scale for Actuals
        private const double ThumbSize = 12;

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

        private void UpdateLineAndThumbPositions()
        {
            if (GraphCanvas.ActualWidth == 0 || _graphLine == null) return;

            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;
            double xStep = _thumbs.Count > 1 ? width / (_thumbs.Count - 1) : 0;

            PointCollection linePoints = new PointCollection();

            for (int i = 0; i < _thumbs.Count; i++)
            {
                var thumb = _thumbs[i];
                var point = (SimulationDataPoint)thumb.DataContext;

                double x = i * xStep;
                double y = GetYPosition(point, height);

                Canvas.SetLeft(thumb, x - (ThumbSize / 2));
                Canvas.SetTop(thumb, y - (ThumbSize / 2));

                linePoints.Add(new Point(x, y));
            }

            _graphLine.Points = linePoints;

            // Update the Status Line (Red vertical line)
            UpdateStatusLine(width, height);
            UpdateLabels(width, height);
        }
        private void UpdateStatusLine(double width, double height)
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
                    // FIX: REMOVED Panel.ZIndex from here
                };

                // FIX: Set the attached property using the static method
                Canvas.SetZIndex(statusLine, 10);
                GraphCanvas.Children.Add(statusLine);
            }

            var points = DataPoints?.OrderBy(p => p.Date).ToList();
            if (points != null && points.Count > 1)
            {
                DateTime start = points.First().Date;
                DateTime end = points.Last().Date;

                double totalTicks = (end - start).Ticks;
                double elapsedTicks = (StatusDate - start).Ticks;

                if (totalTicks > 0)
                {
                    double statusX = (elapsedTicks / totalTicks) * width;
                    statusLine.X1 = statusLine.X2 = Math.Max(0, Math.Min(width, statusX));
                    statusLine.Y1 = 0;
                    statusLine.Y2 = height;
                }
            }
        }
        private void UpdateLabels(double width, double height)
        {
            YAxisCanvas.Children.Clear();
            XAxisCanvas.Children.Clear();

            if (DataPoints == null || !DataPoints.Any()) return;

            // 1. Y-AXIS LABELS (0, 25, 50, 75, 100%)
            double max = EditMode == GraphEditMode.Progress ? 100.0 : MaxActualHours;
            string unit = EditMode == GraphEditMode.Progress ? "%" : "h";
            int increments = 4; // 4 segments = 5 labels

            for (int i = 0; i <= increments; i++)
            {
                double val = (max / increments) * i;
                double y = height - (height * i / increments);

                var txt = new TextBlock
                {
                    Text = $"{val:0}{unit}",
                    Foreground = (Brush)FindResource("TacticalTextMutedBrush"),
                    FontSize = 10,
                    TextAlignment = TextAlignment.Right,
                    Width = 50
                };
                Canvas.SetTop(txt, y - 7); // Center text on the tick
                Canvas.SetLeft(txt, 0);
                YAxisCanvas.Children.Add(txt);
            }

            // 2. X-AXIS LABELS (Dates)
            var points = DataPoints.OrderBy(p => p.Date).ToList();
            if (points.Count > 1)
            {
                double xStep = width / (points.Count - 1);

                // Show a label roughly every 4 weeks to avoid clutter
                int stepSkip = Math.Max(1, points.Count / 5);

                for (int i = 0; i < points.Count; i += stepSkip)
                {
                    double x = i * xStep;
                    var txt = new TextBlock
                    {
                        Text = points[i].Date.ToString("MM/dd"),
                        Foreground = (Brush)FindResource("TacticalTextMutedBrush"),
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    // Center the text block horizontally on the X coordinate
                    txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(txt, x - (txt.DesiredSize.Width / 2));
                    Canvas.SetTop(txt, 5);
                    XAxisCanvas.Children.Add(txt);
                }
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
                double canvasHeight = GraphCanvas.ActualHeight;
                if (canvasHeight <= 0) return;

                // 1. Calculate the new Top position based on mouse delta
                double currentTop = Canvas.GetTop(thumb);
                double newTop = currentTop + e.VerticalChange;

                // 2. Clamp to canvas boundaries
                newTop = Math.Max(0, Math.Min(canvasHeight - ThumbSize, newTop));

                // 3. Convert Y position back to the data value (Progress or Actuals)
                double max = EditMode == GraphEditMode.Progress ? 1.0 : MaxActualHours;

                // Note: (1.0 - percent) because 0 is the top of the canvas
                double valPercent = 1.0 - (newTop / (canvasHeight - ThumbSize));
                double newValue = Math.Max(0, Math.Min(max, valPercent * max));

                // 4. Update the Model
                if (EditMode == GraphEditMode.Progress)
                    point.Progress = newValue;
                else
                    point.ActualHours = newValue;

                // 5. Update visuals WITHOUT clearing the canvas
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
