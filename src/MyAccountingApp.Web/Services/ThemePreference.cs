namespace MyAccountingApp.Web.Services;

/// <summary>
/// How the application theme resolves: follow the operating system, or force one mode.
/// </summary>
public enum ThemePreference
{
    /// <summary>
    /// Follow the system preference (<c>prefers-color-scheme</c>) and react when it changes.
    /// </summary>
    System,

    /// <summary>
    /// Always light, whatever the system says.
    /// </summary>
    Light,

    /// <summary>
    /// Always dark, whatever the system says.
    /// </summary>
    Dark,
}
