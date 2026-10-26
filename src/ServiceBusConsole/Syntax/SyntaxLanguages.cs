using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Core.Rendering.Syntax;

namespace ServiceBusConsole.Syntax;

public static class SyntaxLanguages
{
    /// <summary>RazorConsole's highlighter languages, with <see cref="JsonLanguage"/> replacing ColorCode's JSON.</summary>
    public static IServiceCollection AddSyntaxLanguages(this IServiceCollection services) =>
        services.AddSingleton<ISyntaxLanguageRegistry>(_ =>
        {
            var languages = new ColorCodeLanguageRegistry();
            languages.Register("json", new JsonLanguage());
            return languages;
        });
}
