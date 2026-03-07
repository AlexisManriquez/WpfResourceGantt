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
using System.Windows.Shapes;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.AssignDeveloper
{
    /// <summary>
    /// Interaction logic for AssignDeveloperDialog.xaml
    /// </summary>
    public partial class AssignDeveloperDialog : UserControl
    {
        public AssignDeveloperDialog()
        {
            InitializeComponent();
        }

        // This public property will hold the user's final selection.
        public User SelectedDeveloper => (DataContext as AssignDeveloperViewModel)?.SelectedDeveloper;

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AssignDeveloperViewModel vm)
            {
                vm.Close(true); // Signal success
            }
        }

        private void UnassignButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AssignDeveloperViewModel vm)
            {
                vm.IsUnassignRequested = true;
                vm.Close(true); // Signal success with the unassign flag set
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AssignDeveloperViewModel vm)
            {
                vm.Close(false); // Signal cancellation
            }
        }
    }
}
