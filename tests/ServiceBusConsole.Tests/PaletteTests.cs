using Spectre.Console;

namespace ServiceBusConsole.Tests;

public sealed class PaletteTests
{
    [Test]
    [Arguments(null, null, "dark")]
    [Arguments("auto", "15;0", "dark")]
    [Arguments("auto", "0;15", "light")]
    [Arguments(null, "0;7", "light")]
    [Arguments(null, "12;8", "dark")]
    [Arguments(null, "0;default;15", "light")]
    [Arguments(null, "garbage", "dark")]
    [Arguments("light", "15;0", "light")]
    [Arguments("DARK", "0;15", "dark")]
    public void For_PicksByFlagThenBackground(string? theme, string? colorFgBg, string expected) =>
        Palette.For(theme, colorFgBg).ShouldBe(expected == "light" ? Palette.Light : Palette.Dark);

    [Test]
    public void For_UnknownTheme_IsNull() => Palette.For("solarized", null).ShouldBeNull();

    [Test]
    public void Palettes_UseOnlyTheTerminalsPaletteColors_ButTheFade()
    {
        foreach (var palette in new[] { Palette.Dark, Palette.Light })
        {
            var colors = typeof(Palette).GetProperties()
                .Where(p => p.PropertyType == typeof(Color) && p.Name is not (nameof(Palette.Changed) or nameof(Palette.ChangedFadeTo)))
                .Select(p => (p.Name, Markup: p.GetValue(palette) is Color c ? c.ToMarkup() : ""));
            colors.Where(c => c.Markup.StartsWith('#')).ShouldBeEmpty();
        }
    }
}
