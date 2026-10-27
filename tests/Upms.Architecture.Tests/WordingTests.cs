using System.Text;
using System.Text.RegularExpressions;

namespace Upms.Architecture.Tests;

/// <summary>What people read says "space", never "project" (Jira parity pass FR-019, SC-004; research R3). The code keeps
/// "project" in identifiers, tables, error codes and logs, so this test reads only text that reaches people: the markup of
/// every component, its visible attributes, and the string literals of the Domain, Application and Web projects.</summary>
public sealed partial class WordingTests
{
    // Attributes that never reach the screen: styling, wiring, test hooks and form plumbing.
    private static readonly HashSet<string> HiddenAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "id", "for", "name", "type", "role", "autocomplete", "form", "method", "action", "rel", "target", "src",
        "TestId", "aria-describedby", "aria-controls", "aria-labelledby", "key", "ref",
    };

    // Text that may say "project" on purpose, with the reason.
    private static readonly (string File, string Text)[] Allowed =
    [
        // The old addresses, kept so bookmarks and old emails still work (FR-020).
        ("LegacyAddress.razor", "projects"),
        // The search address's old key, still read so older links and saved filters work.
        ("SearchState.cs", "project"),
        // The product's name: U-PMS is the UBL-Project Management System.
        ("AuthLayout.razor", "UBL-Project Management"),
        ("Header.razor", "UBL-Project Management"),
    ];

    // Lines of Razor markup that are C# statements, not text.
    private static readonly Regex CodeLine = new(
        @"^(@(if|else|foreach|for|while|switch|do|using|inject|page|inherits|implements|attribute|layout|typeparam|rendermode|code|functions|\{)\b|@\{|\}|\{|var |case |default:|break;|return\b)",
        RegexOptions.Compiled);

    [Fact]
    public void JP_US4_AS1_No_screen_says_project()
    {
        var root = RepositoryRoot();
        var findings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "Upms.Web", "Components"), "*.razor", SearchOption.AllDirectories))
        {
            findings.AddRange(Check(root, file, RazorTexts(File.ReadAllText(file))));
        }

        foreach (var project in new[] { "Upms.Domain", "Upms.Application", "Upms.Web" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", project), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
            {
                findings.AddRange(Check(root, file, CSharpTexts(File.ReadAllText(file))));
            }
        }

        Assert.True(findings.Count == 0, "Screen text still says \"project\":" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    [Theory]
    [InlineData("<h1>Create project</h1>", "Create project")]
    [InlineData("<button aria-label=\"Star the project\">", "Star the project")]
    [InlineData("<nav class=\"project-nav\">", null)]
    [InlineData("<a href=\"spaces/@Key/board\">@project.Name</a>", null)]
    [InlineData("@code { private string M() => \"The project was saved.\"; }", "The project was saved.")]
    [InlineData("@* the project header *@<p>Spaces</p>", null)]
    public void The_reader_finds_only_visible_text(string razor, string? found)
    {
        var texts = RazorTexts(razor).Select(t => t.Text).Where(t => Says(t)).ToList();

        Assert.Equal(found is null ? [] : [found], texts);
    }

    [Theory]
    [InlineData("var project = Load(); // the project", null)]
    [InlineData("return AppError.NotFound(\"project\");", "project")]
    [InlineData("[LoggerMessage(Message = \"Denied on project {Project}\")]", null)]
    [InlineData("public const string DuplicateProjectKey = \"DuplicateProjectKey\";", null)]
    [InlineData("var text = $\"Moved to {project.Name} in the project\";", "Moved to  in the project")]
    public void The_reader_skips_code_comments_logs_and_codes(string code, string? found)
    {
        var texts = CSharpTexts(code).Select(t => t.Text).Where(t => Says(t)).ToList();

        Assert.Equal(found is null ? [] : [found], texts);
    }

    private static IEnumerable<string> Check(string root, string file, IEnumerable<(int Line, string Text)> texts)
    {
        var name = Path.GetFileName(file);
        foreach (var (line, text) in texts)
        {
            if (Says(text) && !Allowed.Any(a => a.File == name && text.Contains(a.Text, StringComparison.Ordinal)))
            {
                yield return $"  {Path.GetRelativePath(root, file)}:{line}: {text.Trim()}";
            }
        }
    }

    private static bool Says(string text) => ProjectWord().IsMatch(text);

    /// <summary>Markup text, visible attribute values and the string literals of code blocks in a component.</summary>
    internal static IEnumerable<(int Line, string Text)> RazorTexts(string razor)
    {
        razor = RazorComment().Replace(razor, m => new string('\n', m.Value.Count(c => c == '\n')));
        var codeStart = CodeBlock().Match(razor) is { Success: true } code ? code.Index : razor.Length;
        var markup = razor[..codeStart];
        foreach (var text in CSharpTexts(razor[codeStart..]))
        {
            yield return (text.Line + LineOf(razor, codeStart) - 1, text.Text);
        }

        var lines = markup.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (CodeLine.IsMatch(trimmed) || (trimmed.EndsWith(';') && !trimmed.Contains('<', StringComparison.Ordinal)))
            {
                // A C# statement inside the markup: only its string literals can reach the screen.
                foreach (var literal in CSharpTexts(trimmed.StartsWith("@page", StringComparison.Ordinal) ? "" : line))
                {
                    yield return (i + 1, literal.Text);
                }

                continue;
            }

            // Attribute values, except the hidden ones and event or binding wiring.
            foreach (Match attribute in AttributeValue().Matches(line))
            {
                var attributeName = attribute.Groups["name"].Value.TrimStart('@');
                if (!HiddenAttributes.Contains(attributeName) && !attributeName.StartsWith("data-", StringComparison.OrdinalIgnoreCase)
                    && !attributeName.StartsWith("bind", StringComparison.Ordinal) && !attributeName.StartsWith("on", StringComparison.Ordinal))
                {
                    yield return (i + 1, StripRazor(attribute.Groups["value"].Value));
                }
            }

            // Text between tags, without Razor expressions; string literals inside expressions are read as code.
            var withoutAttributes = AttributeValue().Replace(line, "");
            foreach (Match text in TextNode().Matches(">" + withoutAttributes + "<"))
            {
                yield return (i + 1, StripRazor(text.Groups[1].Value));
            }

            foreach (Match expression in RazorExpression().Matches(withoutAttributes))
            {
                foreach (var literal in CSharpTexts(expression.Value))
                {
                    yield return (i + 1, literal.Text);
                }
            }
        }
    }

    /// <summary>The string literals of C# code, outside comments, log message attributes and constants; the holes of
    /// interpolated strings are left out.</summary>
    internal static IEnumerable<(int Line, string Text)> CSharpTexts(string code)
    {
        var results = new List<(int, string)>();
        var line = 1;
        var i = 0;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                while (i < code.Length && code[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + 2;
                line += code[i..end].Count(ch => ch == '\n');
                i = end;
            }
            else if (c == '\'')
            {
                // A char literal: '"' must not open a string.
                var end = code.IndexOf('\'', i + (i + 1 < code.Length && code[i + 1] == '\\' ? 3 : 2));
                i = end < 0 ? code.Length : end + 1;
            }
            else if (c == '"' || (c is '$' or '@' && i + 1 < code.Length && code[i + 1] is '"' or '$' or '@'))
            {
                var start = i;
                var interpolated = false;
                var verbatim = false;
                while (code[i] != '"')
                {
                    interpolated |= code[i] == '$';
                    verbatim |= code[i] == '@';
                    i++;
                }

                var quotes = 0;
                while (i + quotes < code.Length && code[i + quotes] == '"')
                {
                    quotes++;
                }

                var raw = quotes >= 3;
                i += raw ? quotes : 1;
                var closing = new string('"', quotes);
                var text = new StringBuilder();
                var depth = 0;
                while (i < code.Length)
                {
                    var ch = code[i];
                    if (raw && depth == 0 && string.CompareOrdinal(code, i, closing, 0, quotes) == 0)
                    {
                        i += quotes;
                        break;
                    }

                    if (!raw && depth == 0 && ch == '"' && !(verbatim && i + 1 < code.Length && code[i + 1] == '"'))
                    {
                        i++;
                        break;
                    }

                    if (!verbatim && !raw && ch == '\\' && i + 1 < code.Length)
                    {
                        text.Append(code[i + 1]);
                        i += 2;
                        continue;
                    }

                    if (interpolated && ch == '{' && !(i + 1 < code.Length && code[i + 1] == '{'))
                    {
                        depth++;
                    }
                    else if (interpolated && ch == '}' && depth > 0)
                    {
                        depth--;
                    }
                    else if (depth == 0)
                    {
                        text.Append(ch);
                    }

                    if (ch == '\n')
                    {
                        line++;
                    }

                    i++;
                }

                var statement = LineAround(code, start);
                if (!statement.Contains("[LoggerMessage", StringComparison.Ordinal) && !ConstantDeclaration().IsMatch(statement)
                    && !statement.Contains("nameof(", StringComparison.Ordinal))
                {
                    results.Add((line, text.ToString()));
                }
            }
            else
            {
                i++;
            }
        }

        return results;
    }

    private static string LineAround(string code, int index)
    {
        var start = code.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = code.IndexOf('\n', index);
        return code[start..(end < 0 ? code.Length : end)];
    }

    private static int LineOf(string text, int index) => text[..index].Count(c => c == '\n') + 1;

    private static string StripRazor(string text) => RazorExpression().Replace(text, " ");

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Upms.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Upms.slnx was not found above the test output.");
    }

    [GeneratedRegex(@"\bprojects?\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectWord();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"^@(code|functions)\b", RegexOptions.Multiline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex("(?<name>@?[A-Za-z][\\w:.-]*)\\s*=\\s*\"(?<value>[^\"]*)\"")]
    private static partial Regex AttributeValue();

    [GeneratedRegex(@">([^<>]+)<")]
    private static partial Regex TextNode();

    [GeneratedRegex(@"@\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)|@[A-Za-z_][\w.]*(\([^()]*\))?")]
    private static partial Regex RazorExpression();

    [GeneratedRegex(@"\bconst\s+string\b")]
    private static partial Regex ConstantDeclaration();
}
