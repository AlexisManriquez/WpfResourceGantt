using System.Windows.Controls;

namespace WpfResourceGantt.ProjectManagement.Features.Dashboard
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            this.SizeChanged += DashboardView_SizeChanged;
        }

        private void DashboardView_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (this.DataContext is DashboardViewModel vm)
            {
                // Card Width (350) + Right Margin (20) + Left Grid Margin (20) approx
                double cardWidth = 370.0;
                double availableWidth = e.NewSize.Width > 40 ? e.NewSize.Width - 40 : e.NewSize.Width; // Subtract container padding

                int columns = (int)(availableWidth / cardWidth);
                if (columns < 1) columns = 1;

                vm.UpdateCardLayout(columns);
            }
        }
    }
}
