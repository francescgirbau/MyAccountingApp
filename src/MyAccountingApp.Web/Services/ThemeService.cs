using System.Text.Json;
using Microsoft.JSInterop;
using MudBlazor;

namespace MyAccountingApp.Web.Services;

/// <summary>
/// Owns the MudBlazor theme and the user's three-state theme preference
/// (follow the system, force light, force dark), persisted in browser local storage.
/// </summary>
public class ThemeService
{
    private const string StorageKey = "myaccountingapp.theme";

    private readonly IJSRuntime _jsRuntime;
    private ThemePreference _preference = ThemePreference.System;
    private bool _systemPrefersDark;
    private bool _isDarkMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="jsRuntime">Interop to browser storage and the system colour scheme.</param>
    public ThemeService(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Gets the theme handed to <c>MudThemeProvider</c>.
    /// </summary>
    /// <remarks>
    /// Only <c>PaletteDark</c> is defined: the light palette stays at the MudBlazor defaults
    /// because that is what the app has always rendered. Dark mode is therefore purely additive
    /// and no page, table or dialog has to be touched to support it.
    /// </remarks>
    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#9297F5",
            PrimaryContrastText = "#14141A",
            Secondary = "#F8A4C8",
            SecondaryContrastText = "#14141A",
            Tertiary = "#5CD0B3",
            TertiaryContrastText = "#14141A",
            Info = "#64B5F6",
            InfoContrastText = "#14141A",
            Success = "#66BB6A",
            SuccessContrastText = "#14141A",
            Warning = "#FFB74D",
            WarningContrastText = "#14141A",
            Error = "#EF5350",
            ErrorContrastText = "#14141A",
            Background = "#1B1B1F",
            BackgroundGray = "#131316",
            Surface = "#232329",
            DrawerBackground = "#1B1B1F",
            DrawerIcon = "#C7C4D0",
            DrawerText = "#C7C4D0",
            AppbarBackground = "#16161A",
            AppbarText = "#E6E3EC",
            TextPrimary = "#E6E3EC",
            TextSecondary = "#B9B6C4",
            ActionDefault = "#E6E3EC",
            LinesDefault = "#3A3A44",
            LinesInputs = "#4A4A57",
            Divider = "#3A3A44",
            TableLines = "#3A3A44",
            TableHover = "#2B2B33",
            TableStriped = "#26262D",
        },
    };

    /// <summary>
    /// Gets the current user preference.
    /// </summary>
    public ThemePreference Preference => this._preference;

    /// <summary>
    /// Gets a value indicating whether the app currently renders dark.
    /// </summary>
    public bool IsDarkMode => this._isDarkMode;

    /// <summary>
    /// Gets a value indicating whether the app follows the system colour scheme.
    /// </summary>
    public bool FollowsSystem => this._preference == ThemePreference.System;

    /// <summary>
    /// Reads the stored preference and the system colour scheme, then resolves the initial mode.
    /// </summary>
    /// <returns>A task that completes once the initial mode is resolved and applied.</returns>
    public async Task InitializeAsync()
    {
        string? storedValue = await this.InvokeJsAsync<string>("themeStore.get", StorageKey);
        bool systemPrefersDark = await this.InvokeJsAsync<bool>("themeStore.systemPrefersDark");

        this._systemPrefersDark = systemPrefersDark;
        this._preference = ThemeResolver.ParsePreference(storedValue);
        await this.ApplyAsync();
    }

    /// <summary>
    /// Stores a new preference and re-resolves the effective mode.
    /// </summary>
    /// <param name="preference">The preference chosen by the user.</param>
    /// <returns>A task that completes once the preference is stored and applied.</returns>
    public async Task SetPreferenceAsync(ThemePreference preference)
    {
        this._preference = preference;
        await this.InvokeJsAsync<object>("themeStore.set", StorageKey, ThemeResolver.ToStoredValue(preference));
        await this.ApplyAsync();
    }

    /// <summary>
    /// Handles a system colour scheme change. It only has an effect while the user
    /// follows the system; an explicit light/dark choice is never overridden.
    /// </summary>
    /// <param name="systemPrefersDark">Whether the system now asks for dark mode.</param>
    /// <returns>A task that completes once the mode has been re-resolved.</returns>
    public async Task OnSystemDarkModeChangedAsync(bool systemPrefersDark)
    {
        this._systemPrefersDark = systemPrefersDark;
        if (!this.FollowsSystem)
        {
            return;
        }

        await this.ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        this._isDarkMode = ThemeResolver.ResolveDarkMode(this._systemPrefersDark, this._preference);

        // Mirrors the mode onto <html data-theme> so the few styles MudBlazor does not own
        // (loading spinner, error banner) can follow it too.
        await this.InvokeJsAsync<object>("themeStore.apply", this._isDarkMode);
    }

    private async Task<T> InvokeJsAsync<T>(string identifier, params object?[] args)
    {
        try
        {
            return await this._jsRuntime.InvokeAsync<T>(identifier, args);
        }
        catch (JSException)
        {
            // theme.js missing or storage blocked: the app keeps working, the choice is just not persisted.
            return default!;
        }
        catch (JsonException)
        {
            return default!;
        }
        catch (InvalidOperationException)
        {
            // JS interop unavailable (for example during prerender): fall back to the defaults.
            return default!;
        }
    }
}
