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
        var result = await page.RunAxe(new AxeRunOptions { RunOnly = RunOnlyOptions.Tags(WcagTags) });
        var report = string.Join(Environment.NewLine, result.Violations.Select(v =>
            $"{v.Id} ({v.Impact}): {v.Help}{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", v.Nodes.Select(n => n.Html))));
        Assert.True(result.Violations.Length == 0, $"Accessibility violations on {page.Url}:{Environment.NewLine}{report}");
    }
}
