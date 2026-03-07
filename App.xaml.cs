using System;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using WpfResourceGantt.ProjectManagement.data;

namespace WpfResourceGantt
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show($"NON-UI UNHANDLED EXCEPTION:\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"UI UNHANDLED EXCEPTION:\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
                "Application Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // --- ENFORCE STRICT MM/dd/yyyy DATE FORMAT GLOBALLY ---
            var customCulture = (CultureInfo)new CultureInfo("en-US").Clone();
            customCulture.DateTimeFormat.ShortDatePattern = "MM/dd/yyyy";
            customCulture.DateTimeFormat.DateSeparator = "/";

            // 1. Force background C# parsing to use this format
            Thread.CurrentThread.CurrentCulture = customCulture;
            Thread.CurrentThread.CurrentUICulture = customCulture;

            // 2. Force WPF UI elements (like DatePicker) to use this format
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(customCulture.IetfLanguageTag)));
            base.OnStartup(e);

            // PREVENT SILENT EXIT: Set ShutdownMode to Explicit during the transition between windows.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            }
            catch { }
            TemplateSeeder.SeedTemplates();
            // 1. Show Startup (Login)
            var startupWindow = new StartupWindow();
            bool? result = startupWindow.ShowDialog();

            // 2. Evaluate Result
            if (result == true)
            {
                // Verify the user was passed back
                if (startupWindow.Tag is ProjectManagement.Models.User loggedInUser)
                {
                    try
                    {
                        var mainWindow = new MainWindow(loggedInUser);
                        this.MainWindow = mainWindow;

                        // Set back to default behavior for the main window
                        this.ShutdownMode = ShutdownMode.OnMainWindowClose;

                        mainWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        string details = ex.InnerException != null ? $"\nInner Error: {ex.InnerException.Message}" : "";
                        MessageBox.Show($"FATAL: Error showing Main Window:\n{ex.Message}{details}",
                            "Startup Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    }
                }
                else
                {
                    string tagType = startupWindow.Tag?.GetType().Name ?? "null";
                    MessageBox.Show($"FATAL: User found after login, but Tag was {tagType} instead of User profile.",
                        "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            else
            {
                // If it's not true, it's either null or false.
                // If the user didn't click "X", it shouldn't be here.
                if (result == null || result == false)
                {
                    // Uncomment this for very deep debugging if even the first login fails
                    // MessageBox.Show($"TECHNICAL: Dialog returned {result}. App shutting down.");
                }
                Shutdown();
            }
        }

        public void RestartWithUser(ProjectManagement.Models.User newUser)
        {
            var oldWindow = MainWindow;
            var newWindow = new MainWindow(newUser);
            MainWindow = newWindow;
            newWindow.Show();

            // Allow the old window to close gracefully
            oldWindow?.Close();
        }
    }


}
