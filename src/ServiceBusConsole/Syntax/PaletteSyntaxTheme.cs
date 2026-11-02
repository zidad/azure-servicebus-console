using ColorCode.Common;
using RazorConsole.Core.Rendering.Syntax;

namespace ServiceBusConsole.Syntax;

/// <summary>JSON highlighting in the <see cref="Palette"/>'s colors; RazorConsole's default theme is fixed RGB for dark backgrounds.</summary>
internal sealed class PaletteSyntaxTheme(Palette palette) : SyntaxTheme(SyntaxThemeRegistry.DefaultThemeKey, Style.Plain,
    new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase)
    {
        [ScopeName.JsonKey] = new(palette.JsonKey),
        [ScopeName.JsonString] = new(palette.JsonString),
        [ScopeName.JsonNumber] = new(palette.JsonNumber),
        [ScopeName.JsonConst] = new(palette.JsonConst),
    });
