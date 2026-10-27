using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Upms.E2E.Tests.Fixtures;

/// <summary>Fails on any WCAG 2.0/2.1/2.2 A or AA violation found by axe-core (SC-008).</summary>
public static class AxeAssertions
{
    private static readonly string[] WcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

    public static async Task AssertNoAccessibilityViolationsAsync(this IPage page)
    {
        // A dialog still sliding in is partly transparent, and axe would measure its colours mid-way.
        await page.EvaluateAsync("""
            () => Promise.all(document.getAnimations()
                .filter(a => a.effect?.getTiming().iterations !== Infinity)
                .map(a => a.finished.catch(() => null)))
            """);
        await page.AssertSaysSpaceNotProjectAsync();
        var result = await page.RunAxe(new AxeRunOptions { RunOnly = RunOnlyOptions.Tags(WcagTags) });
        var report = string.Join(Environment.NewLine, result.Violations.Select(v =>
            $"{v.Id} ({v.Impact}): {v.Help}{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", v.Nodes.Select(n => n.Html))));
        Assert.True(result.Violations.Length == 0, $"Accessibility violations on {page.Url}:{Environment.NewLine}{report}");
    }

    /// <summary>Fails when the page's visible text says "project" where it means a space (Jira parity pass SC-004). Run with every
    /// accessibility check, so every page a journey checks is covered; the product's own name is left out.</summary>
    public static async Task AssertSaysSpaceNotProjectAsync(this IPage page)
    {
        var text = await page.EvaluateAsync<string>("() => document.body.innerText");
        var said = System.Text.RegularExpressions.Regex.Matches(text.Replace("UBL-Project Management", "", StringComparison.Ordinal),
                @".{0,40}\bprojects?\b.{0,40}", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(m => m.Value.Trim())
            .ToList();
        Assert.True(said.Count == 0, $"The page {page.Url} still says \"project\":{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", said)}");
    }
}
