using System.Windows.Controls;

namespace WpfResourceGantt.ProjectManagement.Features.ResourceGantt
{
    public partial class ResourceGanttView : UserControl
    {
        public ResourceGanttView()
        {
            InitializeComponent();
        }

        private void HeaderContainer_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (DataContext is ResourceGanttViewModel vm)
            {
                vm.TimelineWidth = e.NewSize.Width;
            }
        }
    }
}
