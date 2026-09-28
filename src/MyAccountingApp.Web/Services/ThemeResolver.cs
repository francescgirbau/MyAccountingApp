namespace MyAccountingApp.Web.Services;

/// <summary>
/// Pure decision rules for the three-state theme preference: how a stored value is parsed,
/// how a preference is written back, and how preference plus system setting resolve to a
/// concrete dark/light mode. Kept free of JS interop so it can be unit tested directly.
/// </summary>
public static class ThemeResolver
{
    /// <summary>
    /// Stored value for "follow the system".
    /// </summary>
    public const string SystemValue = "system";

    /// <summary>
    /// Stored value for "always light".
    /// </summary>
    public const string LightValue = "light";

    /// <summary>
    /// Stored value for "always dark".
    /// </summary>
    public const string DarkValue = "dark";

    /// <summary>
    /// Parses the value kept in browser storage, falling back to <see cref="ThemePreference.System"/>
    /// for anything unknown (missing value, older build, hand-edited storage).
    /// </summary>
    /// <param name="storedValue">The raw stored value.</param>
    /// <returns>The parsed preference, or <see cref="ThemePreference.System"/> when unrecognized.</returns>
    public static ThemePreference ParsePreference(string? storedValue) =>
        storedValue?.Trim().ToLowerInvariant() switch
        {
            LightValue => ThemePreference.Light,
            DarkValue => ThemePreference.Dark,
            _ => ThemePreference.System,
        };

    /// <summary>
    /// Converts a preference into the value persisted in browser storage.
    /// </summary>
    /// <param name="preference">The preference to persist.</param>
    /// <returns>The storage representation of the preference.</returns>
    public static string ToStoredValue(ThemePreference preference) =>
        preference switch
        {
            ThemePreference.Light => LightValue,
            ThemePreference.Dark => DarkValue,
            _ => SystemValue,
        };

    /// <summary>
    /// Resolves the effective mode. An explicit choice always wins; only
    /// <see cref="ThemePreference.System"/> defers to the system setting.
    /// </summary>
    /// <param name="systemPrefersDark">Whether the system currently asks for dark mode.</param>
    /// <param name="preference">The user preference.</param>
    /// <returns><c>true</c> when the app must render dark.</returns>
    public static bool ResolveDarkMode(bool systemPrefersDark, ThemePreference preference) =>
        preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => systemPrefersDark,
        };
}
