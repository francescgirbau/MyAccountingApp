using System.Text.Json;
using Microsoft.JSInterop;
using MudBlazor;
using MyAccountingApp.Web.Services;
using MyAccountingApp.Web.Tests.Fakes;

namespace MyAccountingApp.Web.Tests.Services;

public class ThemeServiceTests
{
    private const string Get = "themeStore.get";
    private const string Set = "themeStore.set";
    private const string SystemPrefersDark = "themeStore.systemPrefersDark";
    private const string Apply = "themeStore.apply";
    private const string StorageKey = "myaccountingapp.theme";

    [Fact]
    public void Theme_DefinesOnlyTheDarkPalette_SoLightModeKeepsMudBlazorDefaults()
    {
        PaletteLight defaults = new();
        MudTheme theme = ThemeService.Theme;

        Assert.NotNull(theme.PaletteDark);
        Assert.Equal(defaults.Primary, theme.PaletteLight?.Primary);
        Assert.Equal(defaults.Background, theme.PaletteLight?.Background);
        Assert.Equal(defaults.Surface, theme.PaletteLight?.Surface);
        Assert.NotEqual(defaults.Surface, theme.PaletteDark?.Surface);
    }

    [Fact]
    public async Task InitializeAsync_StoredDark_RendersDark()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, "dark")
            .Returns(SystemPrefersDark, false);
        ThemeService sut = new(jsRuntime);

        await sut.InitializeAsync();

        Assert.Equal(ThemePreference.Dark, sut.Preference);
        Assert.True(sut.IsDarkMode);
        Assert.False(sut.FollowsSystem);
        Assert.Equal(new object?[] { true }, jsRuntime.CallsTo(Apply).Last());
    }

    [Fact]
    public async Task InitializeAsync_StoredLight_SystemAsksForDark_StaysLight()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, "light")
            .Returns(SystemPrefersDark, true);
        ThemeService sut = new(jsRuntime);

        await sut.InitializeAsync();

        Assert.Equal(ThemePreference.Light, sut.Preference);
        Assert.False(sut.IsDarkMode);
    }

    [Fact]
    public async Task InitializeAsync_NothingStored_FollowsTheSystem()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, null)
            .Returns(SystemPrefersDark, true);
        ThemeService sut = new(jsRuntime);

        await sut.InitializeAsync();

        Assert.Equal(ThemePreference.System, sut.Preference);
        Assert.True(sut.IsDarkMode);
        Assert.True(sut.FollowsSystem);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotWriteStorage()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime().Returns(SystemPrefersDark, false);
        ThemeService sut = new(jsRuntime);

        await sut.InitializeAsync();

        Assert.Empty(jsRuntime.CallsTo(Set));
    }

    [Theory]
    [InlineData("js")]
    [InlineData("json")]
    [InlineData("interop")]
    public async Task InitializeAsync_WhenJavaScriptFails_KeepsWorkingWithDefaults(string failureKind)
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime().FailsEverything(FailureFor(failureKind));
        ThemeService sut = new(jsRuntime);

        await sut.InitializeAsync();

        Assert.Equal(ThemePreference.System, sut.Preference);
        Assert.False(sut.IsDarkMode);
    }

    [Fact]
    public async Task SetPreferenceAsync_Dark_StoresTheChoiceAndAppliesIt()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, "light")
            .Returns(SystemPrefersDark, false);
        ThemeService sut = new(jsRuntime);
        await sut.InitializeAsync();

        await sut.SetPreferenceAsync(ThemePreference.Dark);

        Assert.Equal(ThemePreference.Dark, sut.Preference);
        Assert.True(sut.IsDarkMode);
        Assert.Equal(new object?[] { StorageKey, "dark" }, jsRuntime.CallsTo(Set).Single());
        Assert.Equal(new object?[] { true }, jsRuntime.CallsTo(Apply).Last());
    }

    [Fact]
    public async Task SetPreferenceAsync_BackToSystem_ReResolvesFromTheSystem()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, null)
            .Returns(SystemPrefersDark, false);
        ThemeService sut = new(jsRuntime);
        await sut.InitializeAsync();
        await sut.SetPreferenceAsync(ThemePreference.Dark);

        await sut.SetPreferenceAsync(ThemePreference.System);

        Assert.Equal(ThemePreference.System, sut.Preference);
        Assert.False(sut.IsDarkMode);
        Assert.Equal(new object?[] { StorageKey, "system" }, jsRuntime.CallsTo(Set).Last());
    }

    [Fact]
    public async Task OnSystemDarkModeChanged_WhileFollowingSystem_AppliesTheNewMode()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, null)
            .Returns(SystemPrefersDark, false);
        ThemeService sut = new(jsRuntime);
        await sut.InitializeAsync();

        await sut.OnSystemDarkModeChangedAsync(systemPrefersDark: true);

        Assert.True(sut.IsDarkMode);
        Assert.Equal(new object?[] { true }, jsRuntime.CallsTo(Apply).Last());
    }

    [Fact]
    public async Task OnSystemDarkModeChanged_WhileForced_IgnoresTheSystem()
    {
        FakeJSRuntime jsRuntime = new FakeJSRuntime()
            .Returns(Get, null)
            .Returns(SystemPrefersDark, true);
        ThemeService sut = new(jsRuntime);
        await sut.InitializeAsync();
        await sut.SetPreferenceAsync(ThemePreference.Dark);
        int appliedBefore = jsRuntime.CallsTo(Apply).Count;

        await sut.OnSystemDarkModeChangedAsync(systemPrefersDark: false);

        Assert.True(sut.IsDarkMode);
        Assert.Equal(appliedBefore, jsRuntime.CallsTo(Apply).Count);
    }

    private static Exception FailureFor(string failureKind) => failureKind switch
    {
        "js" => new JSException("themeStore is not defined"),
        "json" => new JsonException("Unexpected token when deserializing"),
        _ => new InvalidOperationException("JavaScript interop calls cannot be issued at this time."),
    };
}
