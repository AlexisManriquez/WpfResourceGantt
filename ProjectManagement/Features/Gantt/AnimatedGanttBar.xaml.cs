using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    public partial class AnimatedGanttBar : UserControl
    {
        // 1. GOOD / HEALTHY (Emerald Theme)
        private readonly SolidColorBrush _goodBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
        private readonly SolidColorBrush _goodFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

        // 2. WARNING / AT RISK (Amber Theme)
        private readonly SolidColorBrush _warningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        private readonly SolidColorBrush _warningFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

        // 3. BAD / CRITICAL (Red Theme)
        private readonly SolidColorBrush _badBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
        private readonly SolidColorBrush _badFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

        // Dependency Properties
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(double), typeof(AnimatedGanttBar), new PropertyMetadata(0.0, OnDataChanged));

        public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(
            "StartDate", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MinValue, OnDataChanged));

        public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(
            "EndDate", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MaxValue, OnDataChanged));

        public static readonly DependencyProperty TotalTimelineStartProperty = DependencyProperty.Register(
            "TotalTimelineStart", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MinValue, OnDataChanged));

        public static readonly DependencyProperty TotalTimelineEndProperty = DependencyProperty.Register(
            "TotalTimelineEnd", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MaxValue, OnDataChanged));

        public static readonly DependencyProperty IsSummaryProperty = DependencyProperty.Register(
            "IsSummary", typeof(bool), typeof(AnimatedGanttBar), new PropertyMetadata(false, OnDataChanged));

        public static readonly DependencyProperty HealthStatusProperty = DependencyProperty.Register(
            "HealthStatus", typeof(MetricStatus), typeof(AnimatedGanttBar), new PropertyMetadata(MetricStatus.Good, OnDataChanged));

        public static readonly DependencyProperty IsCriticalProperty = DependencyProperty.Register(
            "IsCritical", typeof(bool), typeof(AnimatedGanttBar), new PropertyMetadata(false, OnDataChanged));

        public static readonly DependencyProperty ScheduleHealthProperty = DependencyProperty.Register(
            "ScheduleHealth", typeof(MetricStatus), typeof(AnimatedGanttBar), new PropertyMetadata(MetricStatus.Good, OnDataChanged));

        public static readonly DependencyProperty IsBaselinedProperty = DependencyProperty.Register(
            "IsBaselined", typeof(bool), typeof(AnimatedGanttBar), new PropertyMetadata(false, OnDataChanged));

        public static readonly DependencyProperty BaselineStartDateProperty = DependencyProperty.Register(
            "BaselineStartDate", typeof(DateTime?), typeof(AnimatedGanttBar), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty BaselineEndDateProperty = DependencyProperty.Register(
            "BaselineEndDate", typeof(DateTime?), typeof(AnimatedGanttBar), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty IsMilestoneProperty = DependencyProperty.Register(
            "IsMilestone", typeof(bool), typeof(AnimatedGanttBar), new PropertyMetadata(false, OnDataChanged));

        private Grid _taskBarGrid;
        private Grid _summaryBarGrid;
        private Border _taskProgressBar;
        private Border _summaryHealthBackground;
        private Border _summaryProgressFill;
        private Border _baselineBar;
        private Canvas _milestonesCanvas;
        private Polygon _milestoneMarker;

        private WorkItem _currentWorkItem;

        public bool IsMilestone
        {
            get => (bool)GetValue(IsMilestoneProperty);
            set => SetValue(IsMilestoneProperty, value);
        }

        public MetricStatus HealthStatus
        {
            get => (MetricStatus)GetValue(HealthStatusProperty);
            set => SetValue(HealthStatusProperty, value);
        }

        public bool IsSummary
        {
            get => (bool)GetValue(IsSummaryProperty);
            set => SetValue(IsSummaryProperty, value);
        }
        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public DateTime StartDate
        {
            get => (DateTime)GetValue(StartDateProperty);
            set => SetValue(StartDateProperty, value);
        }

        public DateTime EndDate
        {
            get => (DateTime)GetValue(EndDateProperty);
            set => SetValue(EndDateProperty, value);
        }

        public DateTime TotalTimelineStart
        {
            get => (DateTime)GetValue(TotalTimelineStartProperty);
            set => SetValue(TotalTimelineStartProperty, value);
        }

        public DateTime TotalTimelineEnd
        {
            get => (DateTime)GetValue(TotalTimelineEndProperty);
            set => SetValue(TotalTimelineEndProperty, value);
        }

        public bool IsCritical
        {
            get => (bool)GetValue(IsCriticalProperty);
            set => SetValue(IsCriticalProperty, value);
        }

        public MetricStatus ScheduleHealth
        {
            get => (MetricStatus)GetValue(ScheduleHealthProperty);
            set => SetValue(ScheduleHealthProperty, value);
        }

        public bool IsBaselined
        {
            get => (bool)GetValue(IsBaselinedProperty);
            set => SetValue(IsBaselinedProperty, value);
        }

        public DateTime? BaselineStartDate
        {
            get => (DateTime?)GetValue(BaselineStartDateProperty);
            set => SetValue(BaselineStartDateProperty, value);
        }

        public DateTime? BaselineEndDate
        {
            get => (DateTime?)GetValue(BaselineEndDateProperty);
            set => SetValue(BaselineEndDateProperty, value);
        }

        public AnimatedGanttBar()
        {
            InitializeComponent();
            this.SizeChanged += (s, e) => UpdateBar(true);

            // Hook into DataContext to listen for child property changes
            this.DataContextChanged += OnDataContextChanged;
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AnimatedGanttBar)?.UpdateBar();
        }

        // ──── START OF NEW SUBSCRIPTION LOGIC ──── //

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old item
            if (_currentWorkItem != null)
            {
                _currentWorkItem.PropertyChanged -= WorkItem_PropertyChanged;
                if (_currentWorkItem.Children is INotifyCollectionChanged incc)
                    incc.CollectionChanged -= Children_CollectionChanged;

                if (_currentWorkItem.Children != null)
                {
                    foreach (var child in _currentWorkItem.Children)
                    {
                        if (child is INotifyPropertyChanged npc)
                            npc.PropertyChanged -= Child_PropertyChanged;
                    }
                }
            }

            // Subscribe to new item
            _currentWorkItem = e.NewValue as WorkItem;

            if (_currentWorkItem != null)
            {
                _currentWorkItem.PropertyChanged += WorkItem_PropertyChanged;
                if (_currentWorkItem.Children is INotifyCollectionChanged incc)
                    incc.CollectionChanged += Children_CollectionChanged;

                if (_currentWorkItem.Children != null)
                {
                    foreach (var child in _currentWorkItem.Children)
                    {
                        if (child is INotifyPropertyChanged npc)
                            npc.PropertyChanged += Child_PropertyChanged;
                    }
                }
            }

            UpdateBar();
        }

        private void Children_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems)
                {
                    if (oldItem is INotifyPropertyChanged npc) npc.PropertyChanged -= Child_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    if (newItem is INotifyPropertyChanged npc) npc.PropertyChanged += Child_PropertyChanged;
                }
            }
            UpdateBar();
        }

        private void WorkItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Failsafe for missing parent DPs
            if (e.PropertyName == "ActualFinishDate" || e.PropertyName == "Progress")
            {
                UpdateBar();
            }
        }

        private void Child_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When a child milestone finishes, force the parent bar to redraw its embedded milestones!
            if (e.PropertyName == "ActualFinishDate" || e.PropertyName == "EndDate" || e.PropertyName == "IsMilestone")
            {
                UpdateBar();
            }
        }

        // ──── END OF NEW SUBSCRIPTION LOGIC ──── //

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _taskBarGrid = this.FindName("TaskBarGrid") as Grid;
            _summaryBarGrid = this.FindName("SummaryBarGrid") as Grid;
            _taskProgressBar = this.FindName("TaskProgressBar") as Border;
            _summaryHealthBackground = this.FindName("SummaryHealthBackground") as Border;
            _summaryProgressFill = this.FindName("SummaryProgressFill") as Border;
            _baselineBar = this.FindName("BaselineBar") as Border;
            _milestonesCanvas = this.FindName("MilestonesCanvas") as Canvas;
        }

        private void UpdateBar(bool animate = false)
        {
            // --- 1. SMART GUARD CLAUSE ---
            if (_summaryBarGrid == null || _taskBarGrid == null)
            {
                _taskBarGrid = this.FindName("TaskBarGrid") as Grid;
                _summaryBarGrid = this.FindName("SummaryBarGrid") as Grid;
                _taskProgressBar = this.FindName("TaskProgressBar") as Border;
                _summaryHealthBackground = this.FindName("SummaryHealthBackground") as Border;
                _summaryProgressFill = this.FindName("SummaryProgressFill") as Border;
                _baselineBar = this.FindName("BaselineBar") as Border;
                _milestonesCanvas = this.FindName("MilestonesCanvas") as Canvas;

                if (_summaryBarGrid == null || _taskBarGrid == null) return;
            }

            // --- CRITICAL VIRTUALIZATION FIX ---
            if (_summaryProgressFill != null)
                _summaryProgressFill.BeginAnimation(FrameworkElement.WidthProperty, null);
            if (_taskProgressBar != null)
                _taskProgressBar.BeginAnimation(FrameworkElement.WidthProperty, null);
            // -----------------------------------

            if (ActualWidth == 0 || TotalTimelineStart >= TotalTimelineEnd)
            {
                return;
            }

            // --- MILESTONE DIAMOND RENDERING ---
            if (IsMilestone)
            {
                _summaryBarGrid.Visibility = Visibility.Collapsed;
                _taskBarGrid.Visibility = Visibility.Collapsed;
                if (_baselineBar != null) _baselineBar.Visibility = Visibility.Collapsed;

                double milestoneTimelineDays = (TotalTimelineEnd - TotalTimelineStart).TotalDays;
                if (milestoneTimelineDays <= 0) return;

                DateTime milestoneDate = EndDate;
                if (milestoneDate < TotalTimelineStart || milestoneDate > TotalTimelineEnd)
                {
                    if (_milestoneMarker != null) _milestoneMarker.Visibility = Visibility.Collapsed;
                    return;
                }

                double milestoneOffsetDays = (milestoneDate - TotalTimelineStart).TotalDays;
                double centerX = (milestoneOffsetDays / milestoneTimelineDays) * this.ActualWidth;
                double size = 14;
                double half = size / 2;

                if (_milestoneMarker == null)
                {
                    _milestoneMarker = new Polygon
                    {
                        Points = new PointCollection
                        {
                            new Point(half, 0),    // Top
                            new Point(size, half),  // Right
                            new Point(half, size),  // Bottom
                            new Point(0, half)      // Left
                        },
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")),
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B21B6")),
                        StrokeThickness = 1.5,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsHitTestVisible = false
                    };
                    var rootGrid = this.Content as Grid;
                    if (rootGrid != null) rootGrid.Children.Add(_milestoneMarker);
                }

                _milestoneMarker.Visibility = Visibility.Visible;
                _milestoneMarker.Margin = new Thickness(centerX - half, 0, 0, 0);
                return;
            }
            else
            {
                if (_milestoneMarker != null) _milestoneMarker.Visibility = Visibility.Collapsed;
            }

            if (!IsSummary && StartDate >= EndDate)
            {
                return;
            }

            // --- 2. CALCULATE VISIBLE BOUNDS (CLAMPING) ---
            DateTime visibleStart = StartDate < TotalTimelineStart ? TotalTimelineStart : StartDate;
            DateTime visibleEnd = EndDate > TotalTimelineEnd ? TotalTimelineEnd : EndDate;

            if (visibleEnd <= visibleStart)
            {
                _summaryBarGrid.Visibility = Visibility.Collapsed;
                _taskBarGrid.Visibility = Visibility.Collapsed;
                if (_baselineBar != null) _baselineBar.Visibility = Visibility.Collapsed;
                return;
            }

            // --- 3. GANTT GEOMETRY CALCULATIONS ---
            double totalTimelineDays = (TotalTimelineEnd - TotalTimelineStart).TotalDays;
            if (totalTimelineDays <= 0) return;

            double availableWidth = this.ActualWidth;
            double offsetDays = (visibleStart - TotalTimelineStart).TotalDays;
            double leftMargin = (offsetDays / totalTimelineDays) * availableWidth;
            double visibleDurationDays = (visibleEnd - visibleStart).TotalDays;
            double visualBarWidth = (visibleDurationDays / totalTimelineDays) * availableWidth;

            if (double.IsNaN(visualBarWidth) || visualBarWidth < 0) visualBarWidth = 0;
            if (double.IsNaN(leftMargin)) leftMargin = 0;

            // --- 4. PROGRESS CALCULATION (Clamped) ---
            double totalItemDuration = (EndDate - StartDate).TotalDays;
            if (totalItemDuration <= 0) totalItemDuration = 1;

            DateTime completedDate = StartDate.AddDays(totalItemDuration * Progress);
            DateTime visibleCompletedEnd = completedDate;

            if (visibleCompletedEnd > visibleEnd) visibleCompletedEnd = visibleEnd;
            if (visibleCompletedEnd < visibleStart) visibleCompletedEnd = visibleStart;

            double visibleProgressDays = (visibleCompletedEnd - visibleStart).TotalDays;
            double finalProgressWidth = (visibleProgressDays / totalTimelineDays) * availableWidth;

            if (finalProgressWidth < 0 || double.IsNaN(finalProgressWidth)) finalProgressWidth = 0;

            // --- 5. POSITION AND ANIMATE ---
            var duration = animate ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromMilliseconds(0);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (IsSummary)
            {
                _summaryBarGrid.Visibility = Visibility.Visible;
                _taskBarGrid.Visibility = Visibility.Collapsed;

                _summaryBarGrid.Margin = new Thickness(leftMargin, 0, 0, 0);
                _summaryBarGrid.Width = visualBarWidth;

                switch (HealthStatus)
                {
                    case MetricStatus.Warning:
                        _summaryHealthBackground.Background = _warningBrush;
                        _summaryProgressFill.Background = _warningFill;
                        break;
                    case MetricStatus.Bad:
                        _summaryHealthBackground.Background = _badBrush;
                        _summaryProgressFill.Background = _badFill;
                        break;
                    case MetricStatus.Good:
                    default:
                        _summaryHealthBackground.Background = _goodBrush;
                        _summaryProgressFill.Background = _goodFill;
                        break;
                }

                if (animate && _summaryProgressFill != null && finalProgressWidth > 0)
                {
                    var animation = new DoubleAnimation(0, finalProgressWidth, duration) { EasingFunction = easing };
                    _summaryProgressFill.BeginAnimation(FrameworkElement.WidthProperty, animation);
                }
                else if (_summaryProgressFill != null)
                {
                    _summaryProgressFill.Width = finalProgressWidth;
                }
            }
            else // Normal Task
            {
                _taskBarGrid.Visibility = Visibility.Visible;
                _summaryBarGrid.Visibility = Visibility.Collapsed;

                _taskBarGrid.Margin = new Thickness(leftMargin, 0, 0, 0);
                _taskBarGrid.Width = visualBarWidth;

                if (animate && _taskProgressBar != null && finalProgressWidth > 0)
                {
                    var animation = new DoubleAnimation(0, finalProgressWidth, duration) { EasingFunction = easing };
                    _taskProgressBar.BeginAnimation(FrameworkElement.WidthProperty, animation);
                }
                else if (_taskProgressBar != null)
                {
                    _taskProgressBar.Width = finalProgressWidth;
                }
            }

            // --- 5.5 RENDER EMBEDDED MILESTONES ---
            if (IsSummary && _milestonesCanvas != null && this.DataContext is WorkItem item && item.Children != null)
            {
                _milestonesCanvas.Children.Clear();
                var embeddedMilestones = item.Children.Where(c => c.IsMilestone && c.EndDate >= TotalTimelineStart && c.EndDate <= TotalTimelineEnd);

                foreach (var ms in embeddedMilestones)
                {
                    double msOffsetDays = (ms.EndDate - TotalTimelineStart).TotalDays;
                    double centerX = (msOffsetDays / totalTimelineDays) * availableWidth;

                    // --- 1. RENDER PLANNED MARKER (Orange Triangle) ---
                    double size = 12;
                    var planMarker = new Polygon
                    {
                        Points = new PointCollection
                        {
                            new Point(0, 0),
                            new Point(size, 0),
                            new Point(size / 2, size)
                        },
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C00")), // DarkOrange
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D2691E")), // Chocolate
                        StrokeThickness = 1,
                        ToolTip = $"PLANNED: {ms.Name} on {ms.EndDate:M/d}"
                    };

                    Canvas.SetLeft(planMarker, centerX - (size / 2));
                    Canvas.SetTop(planMarker, -6);
                    _milestonesCanvas.Children.Add(planMarker);

                    // --- 2. RENDER ACTUAL MARKER (Blue Diamond) ---
                    if (ms.ActualFinishDate.HasValue)
                    {
                        double actualOffsetDays = (ms.ActualFinishDate.Value - TotalTimelineStart).TotalDays;
                        double actualX = (actualOffsetDays / totalTimelineDays) * availableWidth;

                        var actualMarker = new Polygon
                        {
                            Points = new PointCollection
                            {
                                new Point(size / 2, 0),
                                new Point(size, size / 2),
                                new Point(size / 2, size),
                                new Point(0, size / 2)
                            },
                            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")), // Blue
                            Stroke = Brushes.White,
                            StrokeThickness = 1,
                            ToolTip = $"ACTUAL: {ms.Name} completed {ms.ActualFinishDate.Value:M/d}"
                        };

                        Canvas.SetLeft(actualMarker, actualX - (size / 2));
                        Canvas.SetTop(actualMarker, -6); // Same baseline as baseline triangle
                        _milestonesCanvas.Children.Add(actualMarker);
                    }

                    // --- 3. RENDER TEXT LABEL ---
                    var labelText = ms.Name + "\n" + ms.EndDate.ToString("M/d");
                    if (ms.ActualFinishDate.HasValue)
                        labelText += "\n(ACT: " + ms.ActualFinishDate.Value.ToString("M/d") + ")";

                    var label = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 9,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                        TextAlignment = TextAlignment.Center,
                        LineHeight = 10,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        FontWeight = FontWeights.SemiBold
                    };

                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, centerX - (label.DesiredSize.Width / 2));
                    Canvas.SetTop(label, -6 - label.DesiredSize.Height - 2);
                    _milestonesCanvas.Children.Add(label);
                }
            }
            else if (_milestonesCanvas != null)
            {
                _milestonesCanvas.Children.Clear();
            }

            // --- 6. POSITION BASELINE BAR ---
            if (IsBaselined && BaselineStartDate.HasValue && BaselineEndDate.HasValue && _baselineBar != null)
            {
                DateTime bStart = BaselineStartDate.Value;
                DateTime bEnd = BaselineEndDate.Value;

                DateTime vBStart = bStart < TotalTimelineStart ? TotalTimelineStart : bStart;
                DateTime vBEnd = bEnd > TotalTimelineEnd ? TotalTimelineEnd : bEnd;

                if (vBEnd > vBStart)
                {
                    _baselineBar.Visibility = Visibility.Visible;
                    double bOffsetDays = (vBStart - TotalTimelineStart).TotalDays;
                    double bLeftMargin = (bOffsetDays / totalTimelineDays) * availableWidth;
                    double bVisibleDurationDays = (vBEnd - vBStart).TotalDays;
                    double bVisualWidth = (bVisibleDurationDays / totalTimelineDays) * availableWidth;

                    _baselineBar.Margin = new Thickness(bLeftMargin, 2, 0, 0);
                    _baselineBar.Width = bVisualWidth;
                }
                else
                {
                    _baselineBar.Visibility = Visibility.Collapsed;
                }
            }
            else if (_baselineBar != null)
            {
                _baselineBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
