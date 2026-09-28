using Microsoft.JSInterop;
using MyAccountingApp.Web.Services;

namespace MyAccountingApp.Web.Tests.Services;

public class ThemeResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sepia")]
    [InlineData("42")]
    public void ParsePreference_UnknownValue_FallsBackToSystem(string? storedValue)
    {
        ThemePreference parsed = ThemeResolver.ParsePreference(storedValue);

        Assert.Equal(ThemePreference.System, parsed);
    }

    [Theory]
    [InlineData("light", ThemePreference.Light)]
    [InlineData("Light", ThemePreference.Light)]
    [InlineData(" LIGHT ", ThemePreference.Light)]
    [InlineData("dark", ThemePreference.Dark)]
    [InlineData("DARK", ThemePreference.Dark)]
    [InlineData("system", ThemePreference.System)]
    public void ParsePreference_KnownValue_ParsesCaseAndWhitespaceInsensitively(string storedValue, ThemePreference expected)
    {
        ThemePreference parsed = ThemeResolver.ParsePreference(storedValue);

        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(ThemePreference.Light, "light")]
    [InlineData(ThemePreference.Dark, "dark")]
    [InlineData(ThemePreference.System, "system")]
    public void ToStoredValue_MapsEveryPreference(ThemePreference preference, string expected)
    {
        string stored = ThemeResolver.ToStoredValue(preference);

        Assert.Equal(expected, stored);
    }

    [Fact]
    public void StoredValue_RoundTripsThroughParse()
    {
        foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
        {
            ThemePreference parsed = ThemeResolver.ParsePreference(ThemeResolver.ToStoredValue(preference));

            Assert.Equal(preference, parsed);
        }
    }

    [Theory]
    [InlineData(false, ThemePreference.Light, false)]
    [InlineData(true, ThemePreference.Light, false)]
    [InlineData(false, ThemePreference.Dark, true)]
    [InlineData(true, ThemePreference.Dark, true)]
    [InlineData(false, ThemePreference.System, false)]
    [InlineData(true, ThemePreference.System, true)]
    public void ResolveDarkMode_ExplicitChoiceWins_OnlySystemDefers(bool systemPrefersDark, ThemePreference preference, bool expected)
    {
        bool isDarkMode = ThemeResolver.ResolveDarkMode(systemPrefersDark, preference);

        Assert.Equal(expected, isDarkMode);
    }
}
