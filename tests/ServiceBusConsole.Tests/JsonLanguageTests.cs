using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Core.Rendering.Syntax;

namespace ServiceBusConsole.Tests;

public sealed class JsonLanguageTests
{
    [Fact]
    public async Task Highlight_JsonArrayOfLongStrings_DoesNotBacktrackForever()
    {
        using var services = new ServiceCollection().AddSyntaxLanguages().BuildServiceProvider();
        var highlighter = new SyntaxHighlightingService(services.GetRequiredService<ISyntaxLanguageRegistry>(), new SyntaxThemeRegistry(), new SpectreMarkupFormatter());
        var item = new string('a', 80);
        var json = $$"""
            {
              "key": "value",
              "items": [
                "{{item}}",
                "{{item}}"
              ],
              "escaped": "a \"quoted\" word",
              "n": -1.5e3,
              "ok": true
            }
            """;

        var result = await Task.Run(() => highlighter.Highlight(new SyntaxHighlightRequest(json, "json", null, false, SyntaxOptions.Default)))
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var text = string.Join("\n", result.Lines);
        text.ShouldContain(item);
        text.ShouldContain("quoted");
        text.ShouldContain("#9cdcfe", Case.Insensitive);
    }
}
