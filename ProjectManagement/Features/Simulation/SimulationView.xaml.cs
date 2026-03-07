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
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    /// <summary>
    /// Interaction logic for SimulationView.xaml
    /// </summary>
    public partial class SimulationView : UserControl
    {
        public SimulationView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is SimulationViewModel vm)
            {
                // We only allow selecting WorkBreakdownItems (Tasks/Gates) for manipulation
                if (e.NewValue is WorkBreakdownItem item)
                {
                    vm.SelectedItem = item;
                }
                else
                {
                    vm.SelectedItem = null;
                }
            }
        }
    }

}
