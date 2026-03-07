using System.Threading.Tasks;
using System.Windows.Controls;
using WpfResourceGantt.ProjectManagement.Features.Gantt;

namespace WpfResourceGantt.ProjectManagement
{
    /// <summary>
    /// Entry point for the Project Management module.
    /// Use this class to access Project Management views from another WPF application.
    /// </summary>
    public static class ProjectManagementModule
    {
        /// <summary>
        /// Gets the main Project Management control for embedding in another window.
        /// The control automatically initializes its ViewModel when loaded.
        /// </summary>
        /// <returns>A UserControl containing the full Project Management UI</returns>
        public static ProjectManagementControl GetMainControl()
        {
            return new ProjectManagementControl();
        }

        /// <summary>
        /// Gets the main Project Management control with a pre-initialized ViewModel.
        /// Use this for more control over the initialization process.
        /// </summary>
        /// <returns>A UserControl with DataContext already set</returns>
        public static async Task<ProjectManagementControl> GetMainControlAsync()
        {
            var viewModel = await MainViewModel.CreateAsync();
            return new ProjectManagementControl { DataContext = viewModel };
        }

        /// <summary>
        /// Gets just the Gantt view with a pre-initialized ViewModel.
        /// </summary>
        /// <returns>The Gantt view control</returns>
        public static async Task<GanttView> GetGanttViewAsync()
        {
            var mainVm = await MainViewModel.CreateAsync();
            return new GanttView { DataContext = mainVm.CurrentViewModel };
        }

        /// <summary>
        /// Gets a new instance of the MainViewModel.
        /// Use this if you need direct access to the ViewModel.
        /// </summary>
        public static async Task<MainViewModel> GetMainViewModelAsync()
        {
            return await MainViewModel.CreateAsync();
        }
    }
}
