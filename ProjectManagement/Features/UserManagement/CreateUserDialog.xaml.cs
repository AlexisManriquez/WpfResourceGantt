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

namespace WpfResourceGantt.ProjectManagement.Features.UserManagement
{
    /// <summary>
    /// Interaction logic for CreateUserDialog.xaml
    /// </summary>
    public partial class CreateUserDialog : Window
    {
        public CreateUserDialog()
        {
            InitializeComponent();
            // You would set your DataContext here, e.g., to a ViewModel
            // this.DataContext = new CreateUserViewModel();
        }

        // Allows the window to be dragged from any empty space
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        // Handles the "Create" button click
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Logic to create the user would go here or in the ViewModel command
            this.DialogResult = true;
        }

        // Handles the "Cancel" and 'X' button clicks
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }

}
