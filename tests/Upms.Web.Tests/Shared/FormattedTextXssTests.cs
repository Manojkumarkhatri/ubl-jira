using System.Text.Json;
using AngleSharp.Html.Parser;
using Upms.Domain.Work;
using Upms.Web.Components.Shared;

namespace Upms.Web.Tests.Shared;

/// <summary>Nothing a user writes can run in the browser (Phase 4 SC-007, research R3, R24): every sample of
/// <c>xss-samples.json</c> is either refused when it is checked or rendered to HTML with no script, no event handler, no
/// unsafe link and nothing loaded from elsewhere.</summary>
public sealed class FormattedTextXssTests
{
    private static readonly string[] ForbiddenElements =
        ["script", "iframe", "object", "embed", "style", "svg", "math", "form", "input", "button", "base", "meta", "link", "img",
         "video", "audio", "source", "details"];

    public static TheoryData<string, string> Samples()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Shared", "xss-samples.json");
        using var samples = JsonDocument.Parse(File.ReadAllText(path));
        var data = new TheoryData<string, string>();
        foreach (var sample in samples.RootElement.EnumerateArray())
        {
            data.Add(sample.GetProperty("name").GetString()!, sample.GetProperty("document").GetRawText());
        }

        return data;
    }

    [Fact]
    public void P4_SC007_There_are_at_least_100_samples()
    {
        Assert.True(Samples().Count >= 100);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void P4_SC007_A_sample_is_refused_or_rendered_harmless(string name, string document)
    {
        var parsed = FormattedText.Parse(document, "Body");
        if (!parsed.IsSuccess)
        {
            Assert.Contains(parsed.Error!.Code, new[] { FormattedText.UnsupportedFormattingCode, FormattedText.InvalidLinkCode });
            return;
        }

        var html = FormattedTextRenderer.ToHtml(parsed.Value!);
        var body = new HtmlParser().ParseDocument($"<!doctype html><html><body>{html}</body></html>").Body!;
        var elements = body.QuerySelectorAll("*").ToList();

        Assert.All(elements, e =>
        {
            Assert.DoesNotContain(e.LocalName, ForbiddenElements);
            Assert.DoesNotContain(e.Attributes, a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase));
            Assert.Null(e.GetAttribute("style"));
            Assert.Null(e.GetAttribute("src"));
        });
        Assert.All(elements.Where(e => e.LocalName == "a"), a =>
        {
            var href = a.GetAttribute("href")!;
            Assert.True(Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto",
                $"{name}: unsafe link {href}");
            Assert.Equal("noopener noreferrer nofollow", a.GetAttribute("rel"));
        });
    }
}
