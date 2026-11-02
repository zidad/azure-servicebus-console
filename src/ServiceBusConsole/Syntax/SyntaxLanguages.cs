using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Core.Rendering.Syntax;

namespace ServiceBusConsole.Syntax;

public static class SyntaxLanguages
{
    /// <summary>
    /// RazorConsole's highlighter languages, with <see cref="JsonLanguage"/> replacing ColorCode's JSON, and the
    /// <see cref="Theme"/>'s colors as the default syntax theme.
    /// </summary>
    public static IServiceCollection AddSyntaxLanguages(this IServiceCollection services) =>
        services
            .AddSingleton<ISyntaxLanguageRegistry>(_ =>
            {
                var languages = new ColorCodeLanguageRegistry();
                languages.Register("json", new JsonLanguage());
                return languages;
            })
            .AddSingleton<ISyntaxThemeRegistry>(_ =>
            {
                var themes = new SyntaxThemeRegistry();
                themes.Register(SyntaxThemeRegistry.DefaultThemeKey, new PaletteSyntaxTheme(Theme.Palette));
                return themes;
            });
}
