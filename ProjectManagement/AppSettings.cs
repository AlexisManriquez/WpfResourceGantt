namespace WpfResourceGantt.ProjectManagement
{
    /// <summary>
    /// Central application configuration flags.
    /// These are compile-time toggles that control deployment behavior.
    /// </summary>
    public static class AppSettings
    {
        // ════════════════════════════════════════════════════════════════
        // DEPLOYMENT TOGGLE
        // ════════════════════════════════════════════════════════════════
        // Set to TRUE  → Production: Auto-login via Windows identity (no combobox)
        // Set to FALSE → Development: Show user selection combobox
        // ════════════════════════════════════════════════════════════════
        public const bool UseWindowsAuthentication = true;
    }
}
