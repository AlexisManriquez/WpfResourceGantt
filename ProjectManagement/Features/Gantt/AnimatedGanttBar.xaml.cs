using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfResourceGantt.ProjectManagement.Features.Gantt
{
    /// <summary>
    /// Interaction logic for AnimatedGanttBar.xaml
    /// </summary>
    public partial class AnimatedGanttBar : UserControl
    {
        // 1. GOOD / HEALTHY (Emerald Theme)
        // Background: Emerald-400 | Border: Emerald-600
        private readonly SolidColorBrush _goodBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
        private readonly SolidColorBrush _goodFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

        // 2. WARNING / AT RISK (Amber Theme)
        // Background: Amber-400 | Border: Amber-600
        private readonly SolidColorBrush _warningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        private readonly SolidColorBrush _warningFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

        // 3. BAD / CRITICAL (Red Theme)
        // Background: Red-500 | Border: Red-700
        private readonly SolidColorBrush _badBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
        private readonly SolidColorBrush _badFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        // 1. Dependency Property for the task's progress (0.0 to 1.0)
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(double), typeof(AnimatedGanttBar), new PropertyMetadata(0.0, OnDataChanged));

        // 2. Dependency Property for the task's start date
        public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(
            "StartDate", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MinValue, OnDataChanged));

        // 3. Dependency Property for the task's end date
        public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(
            "EndDate", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MaxValue, OnDataChanged));

        // 4. Dependency Property for the overall timeline's start date
        public static readonly DependencyProperty TotalTimelineStartProperty = DependencyProperty.Register(
            "TotalTimelineStart", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MinValue, OnDataChanged));

        // 5. Dependency Property for the overall timeline's end date
        public static readonly DependencyProperty TotalTimelineEndProperty = DependencyProperty.Register(
            "TotalTimelineEnd", typeof(DateTime), typeof(AnimatedGanttBar), new PropertyMetadata(DateTime.MaxValue, OnDataChanged));

        // 6. Dependency Property to flag if this is a summary bar
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

        private Grid _taskBarGrid;
        private Grid _summaryBarGrid;
        private Border _taskProgressBar;
        private Border _summaryHealthBackground;

        private Border _summaryProgressFill;
        private Border _criticalPathBorder;
        public MetricStatus HealthStatus // This property is now the correct type
        {
            get => (MetricStatus)GetValue(HealthStatusProperty);
            set => SetValue(HealthStatusProperty, value);
        }

        // Standard .NET Property wrappers for the Dependency Properties
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
            // We use the SizeChanged event instead of Loaded because it fires
            // whenever the control's size is determined or changes, which is more reliable.
            this.SizeChanged += (s, e) => UpdateBar(true);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AnimatedGanttBar)?.UpdateBar();
        }
        // This single callback handles any data changes and redraws the bar
        private static void OnDateOrProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AnimatedGanttBar)?.UpdateBar();
        }
        private Border _baselineBar;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Use the FindName method to get a reference to the elements inside the template.
            // The name in the string must EXACTLY match the x:Name in your XAML.
            _taskBarGrid = this.FindName("TaskBarGrid") as Grid;
            _summaryBarGrid = this.FindName("SummaryBarGrid") as Grid;
            _taskProgressBar = this.FindName("TaskProgressBar") as Border;
            _summaryHealthBackground = this.FindName("SummaryHealthBackground") as Border;
            _summaryProgressFill = this.FindName("SummaryProgressFill") as Border;
            _criticalPathBorder = this.Template.FindName("CriticalPathBorder", this) as Border;
            _baselineBar = this.FindName("BaselineBar") as Border;
        }

        // This handles the initial drawing and any resizing of the window
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBar(true); // Animate on first load/resize
        }

        private void UpdateBar(bool animate = false)
        {
            // --- 1. SMART GUARD CLAUSE ---
            // Ensure the internal template parts are actually loaded before trying to access them
            if (_summaryBarGrid == null || _taskBarGrid == null)
            {
                // Fallback: try to find them if they are null (sometimes happens with virtualization/recycled controls)
                _taskBarGrid = this.FindName("TaskBarGrid") as Grid;
                _summaryBarGrid = this.FindName("SummaryBarGrid") as Grid;
                _taskProgressBar = this.FindName("TaskProgressBar") as Border;
                _summaryHealthBackground = this.FindName("SummaryHealthBackground") as Border;
                _summaryProgressFill = this.FindName("SummaryProgressFill") as Border;
                _baselineBar = this.FindName("BaselineBar") as Border;

                if (_summaryBarGrid == null || _taskBarGrid == null) return;
            }

            if (ActualWidth == 0 || TotalTimelineStart >= TotalTimelineEnd || (!IsSummary && StartDate >= EndDate))
            {
                return;
            }

            // --- 2. CALCULATE VISIBLE BOUNDS (CLAMPING) ---
            DateTime visibleStart = StartDate < TotalTimelineStart ? TotalTimelineStart : StartDate;
            DateTime visibleEnd = EndDate > TotalTimelineEnd ? TotalTimelineEnd : EndDate;

            // Check if the item is completely outside the current view
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

            if (finalProgressWidth < 0) finalProgressWidth = 0;

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

                if (animate && _summaryProgressFill != null)
                {
                    var animation = new DoubleAnimation(0, finalProgressWidth, duration) { EasingFunction = easing };
                    _summaryProgressFill.BeginAnimation(WidthProperty, animation);
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
                // APPLY: Health Colors to Leaf Tasks
                switch (HealthStatus)
                {
                    case MetricStatus.Warning:
                        TaskProgressBar.Background = _warningFill;
                        break;
                    case MetricStatus.Bad:
                        TaskProgressBar.Background = _badFill;
                        break;
                    case MetricStatus.Good:
                    default:
                        TaskProgressBar.Background = _goodFill;
                        break;
                }

                // APPLY: Critical Path Border
                if (_criticalPathBorder != null)
                {
                    _criticalPathBorder.Visibility = IsCritical ? Visibility.Visible : Visibility.Collapsed;
                    _criticalPathBorder.BorderBrush = Brushes.Red;
                }
                if (animate && _taskProgressBar != null)
                {
                    var animation = new DoubleAnimation(0, finalProgressWidth, duration) { EasingFunction = easing };
                    _taskProgressBar.BeginAnimation(WidthProperty, animation);
                }
                else if (_taskProgressBar != null)
                {
                    _taskProgressBar.Width = finalProgressWidth;
                }
            }

            // --- 6. POSITION BASELINE BAR ---
            if (IsBaselined && BaselineStartDate.HasValue && BaselineEndDate.HasValue && _baselineBar != null)
            {
                DateTime bStart = BaselineStartDate.Value;
                DateTime bEnd = BaselineEndDate.Value;

                // Clamping
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
