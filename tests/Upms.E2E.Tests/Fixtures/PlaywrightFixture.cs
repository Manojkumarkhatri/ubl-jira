using Microsoft.Playwright;

namespace Upms.E2E.Tests.Fixtures;

/// <summary>A headless Chromium per test class. Traces of failed tests are kept in <c>traces/</c>.</summary>
public abstract class BrowserTest(AppFixture app) : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly List<IBrowserContext> _contexts = [];

    protected AppFixture App { get; } = app;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>A fresh browser session (its own cookies), like a second person's browser.</summary>
    protected async Task<IPage> NewPageAsync(int width = 1280, int height = 900)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = App.BaseUrl,
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        _contexts.Add(context);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        return page;
    }

    protected async Task<IPage> SignInAsync(string userName, string password)
    {
        var page = await NewPageAsync();
        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("User name").FillAsync(userName);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        return page;
    }

    public async ValueTask DisposeAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        foreach (var context in _contexts)
        {
            var path = failed
                ? Path.Combine(AppContext.BaseDirectory, "traces", $"{TestContext.Current.Test?.TestDisplayName.Replace('/', '_')}-{Guid.NewGuid():N}.zip")
                : null;
            await context.Tracing.StopAsync(new TracingStopOptions { Path = path });
            await context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        GC.SuppressFinalize(this);
    }
}
