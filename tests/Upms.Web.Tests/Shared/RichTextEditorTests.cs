using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Upms.Application.Work;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Shared;

/// <summary>The formatted-text editor's Blazor side (Phase 4 FR-001, FR-005, FR-007; research R2, R5), with the
/// JavaScript editor mocked.</summary>
public sealed class RichTextEditorTests : BunitTestBase
{
    private const string Bold = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Plan","marks":[{"type":"bold"}]}]}]}""";

    private readonly FakeMentionService _mentions = new();
    private readonly List<string> _saved = [];
    private string? _refusal;

    public RichTextEditorTests()
    {
        Services.AddSingleton<IMentionService>(_mentions);
    }

    private IRenderedComponent<RichTextEditor> Show(RichText? initial = null, string? workItemKey = "WEB-1") =>
        Render<RichTextEditor>(p => p
            .Add(x => x.Id, "drawer-description")
            .Add(x => x.Label, "Description")
            .Add(x => x.Initial, initial)
            .Add(x => x.WorkItemKey, workItemKey)
            .Add(x => x.OnSave, (RichTextEditor.EditorSave save) =>
            {
                _saved.Add(save.Document);
                save.Error = _refusal;
            }));

    private object? Option(string name)
    {
        var options = JSInterop.Invocations.Single(i => i.Identifier == "upms.editor.create").Arguments[1]!;
        return options.GetType().GetProperty(name)!.GetValue(options);
    }

    [Fact]
    public void P4_FR001_The_editor_starts_from_the_document()
    {
        Show(new RichText(Upms.Domain.Work.TextFormat.Document, Bold, "Plan"));

        Assert.Equal(Bold, Option("content"));
        Assert.Equal("Description", Option("label"));
        Assert.Equal(32_000, Option("limit"));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.loadEditor");
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.editor.focus");
    }

    [Fact]
    public void P4_FR004_Old_plain_text_opens_as_one_paragraph_per_line()
    {
        Show(Rich.Plain("One\nTwo"));

        Assert.Equal(Rich.Document("One\nTwo"), Option("content"));
    }

    [Fact]
    public void P4_FR005_Formatting_shows_over_selected_text_and_the_hint_says_how_to_reach_it_from_the_keyboard()
    {
        var cut = Show();

        // As in Jira there is no fixed toolbar: editor.js shows its labelled toolbar over selected text, and Alt+F10 moves
        // into it. The editing area is described by this hint.
        Assert.Empty(cut.FindAll("[role=toolbar]"));
        Assert.Contains("Alt+F10", cut.Find("#drawer-description-hint").TextContent, StringComparison.Ordinal);
        Assert.Equal("drawer-description-hint", Option("hintId"));
        Assert.Equal("Description", Option("label"));
        Assert.NotNull(cut.Find("#drawer-description-area"));
    }

    [Fact]
    public void P4_FR001_Save_sends_the_documents_JSON()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Bold);
        var cut = Show();

        cut.Find("[data-testid=drawer-description-save]").Click();

        cut.WaitForAssertion(() => Assert.Equal([Bold], _saved));
        Assert.Equal("", cut.Find("#drawer-description-error").TextContent);
    }

    [Fact]
    public void A_refused_save_shows_the_reason_and_keeps_the_text()
    {
        JSInterop.Setup<string?>("upms.editor.getJson", _ => true).SetResult(Bold);
        _refusal = "Links must start with http, https or mailto.";
        var cut = Show();

        cut.Find("[data-testid=drawer-description-save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(_refusal, cut.Find("#drawer-description-error").TextContent));
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier is "upms.editor.setContent" or "upms.editor.destroy");
    }

    [Fact]
    public async Task P4_US1_AS5_At_suggestions_come_from_the_mention_service()
    {
        var cut = Show();

        var found = await cut.InvokeAsync(() => cut.Instance.SuggestMentionsAsync("bi"));

        Assert.Equal([("WEB-1", "bi")], _mentions.Calls);
        var bilal = Assert.Single(found);
        Assert.Equal(("0000000b-0000-0000-0000-000000000000", "Bilal Ahmed", "bilal"), (bilal.Id, bilal.Label, bilal.UserName));
        Assert.NotNull(Option("dotnet"));
    }

    [Fact]
    public void Without_an_item_there_are_no_suggestions()
    {
        Show(workItemKey: null);

        Assert.Null(Option("dotnet"));
    }

    [Fact]
    public void An_editor_that_cannot_load_says_so()
    {
        JSInterop.SetupVoid("upms.loadEditor").SetException(new JSException("offline"));
        var cut = Show();

        cut.WaitForAssertion(() => Assert.Contains("could not be loaded", cut.Find("#drawer-description-error").TextContent, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("[data-testid=drawer-description-save]").GetAttribute("disabled"));
    }

    [Fact]
    public async Task Leaving_the_page_destroys_the_editor()
    {
        JSInterop.Setup<int>("upms.editor.create", _ => true).SetResult(7);
        var cut = Show();

        await cut.Instance.DisposeAsync();

        Assert.Equal(7, JSInterop.Invocations.Single(i => i.Identifier == "upms.editor.destroy").Arguments[0]);
    }
}
