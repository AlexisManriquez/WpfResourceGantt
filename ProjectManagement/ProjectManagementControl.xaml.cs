using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Features.ApplyTemplate;

namespace WpfResourceGantt.ProjectManagement
{
    /// <summary>
    /// Embeddable UserControl that provides the full Project Management UI.
    /// Use this to embed the Project Management views in another WPF application.
    /// </summary>
    public partial class ProjectManagementControl : UserControl
    {
        public static readonly DependencyProperty TargetViewProperty =
            DependencyProperty.Register("TargetView", typeof(string), typeof(ProjectManagementControl),
                new PropertyMetadata(null, OnTargetViewChanged));

        public string TargetView
        {
            get { return (string)GetValue(TargetViewProperty); }
            set { SetValue(TargetViewProperty, value); }
        }

        public ProjectManagementControl()
        {
            InitializeComponent();
            this.Loaded += ProjectManagementControl_Loaded;
        }

        private void ProjectManagementControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Do NOT create a new ViewModel. The DataContext is inherited from MainWindow.
            // If we are blank, it means MainWindow hasn't set it yet, or the binding 'CurrentViewModel' is null.
        }

        private static void OnTargetViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Do nothing. The view switching is now handled by the ViewModel itself 
            // updating the 'CurrentViewModel' property.
        }
        private void OnOverlayClick(object sender, MouseButtonEventArgs e)
        {
            // Access the MainViewModel through the DataContext
            if (this.DataContext is MainViewModel mainVm)
            {
                // Check if the dialog is currently a template dialog
                if (mainVm.CurrentDialogViewModel is ApplyTemplateDialogViewModel templateVm)
                {
                    // Close it (triggering the 'False' cancel logic)
                    templateVm.CancelCommand.Execute(null);
                }
                else
                {
                    // Fallback for generic dialogs
                    mainVm.CurrentDialogViewModel = null;
                }
            }
        }
    }
}
