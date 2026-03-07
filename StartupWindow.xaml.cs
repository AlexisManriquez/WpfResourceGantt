using System;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt
{
    public partial class StartupWindow : Window
    {
        private StartupViewModel _viewModel;

        public StartupWindow()
        {
            InitializeComponent();
            _viewModel = new StartupViewModel();
            this.DataContext = _viewModel;

            _viewModel.OnLoginSuccess += OnLoginSuccess;

            // Allow dragging the window
            this.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };
        }

        private void OnLoginSuccess(User selectedUser)
        {
            // Close this window and let App.xaml.cs handle the launch
            this.Tag = selectedUser; // Pass user back via Tag or DialogResult
            this.DialogResult = true;
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
