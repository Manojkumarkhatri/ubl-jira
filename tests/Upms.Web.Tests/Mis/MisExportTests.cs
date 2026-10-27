using System.IO.Compression;
using System.Xml.Linq;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Web.Endpoints;

namespace Upms.Web.Tests.Mis;

public sealed class MisExportTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>A portfolio with two spaces, one late; text that needs escaping, and a cell that looks like a formula.</summary>
    private static MisReport Report() => new(
        MisLevel.Portfolio,
        "Data & <AI>",
        new DateOnly(2026, 10, 20),
        new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero),
        "Sara Malik",
        new MisTotals(2, 10, 3, 4, 3, 2, 1, 1, 1, 0, 1),
        [
            new MisSpaceRow("IT-PMO", "Data & <AI>", "MOB", "Mobile App", ProjectTemplate.Scrum, "Omar Ali", 6, 2, 2, 2, 2, 1,
                "Go-live", new DateOnly(2026, 11, 1), MisHealth.OffTrack, new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero)),
            new MisSpaceRow("IT-PMO", "Data & <AI>", "CBU", "Core Banking", ProjectTemplate.Kanban, "", 4, 1, 2, 1, 0, 0,
                null, null, MisHealth.OnTrack, null),
        ],
        [new MisContainerRow("MOB", "MOB-1", "Release 1", "Epic", "In Progress", StatusCategory.InProgress, "Omar Ali",
            new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 10), 4, 1, true)],
        [new MisItemRow("MOB", "MOB-7", "Task", "=HYPERLINK(\"x\")", "To Do", StatusCategory.ToDo, null, new DateOnly(2026, 10, 15), 5, "High")],
        [new MisItemRow("MOB", "MOB-9", "Milestone", "Go-live", "To Do", StatusCategory.ToDo, "Omar Ali", new DateOnly(2026, 11, 1), 12, "Medium")],
        [new MisPersonRow("Omar Ali", 5, 2, 3)]);

    [Fact]
    public void The_workbook_is_a_valid_package_with_one_sheet_per_table()
    {
        using var zip = new ZipArchive(new MemoryStream(MisExport.Xlsx(Report())));
        var parts = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", parts);
        Assert.Contains("xl/workbook.xml", parts);
        Assert.Contains("xl/styles.xml", parts);
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            XDocument.Load(stream); // throws when a part is not well-formed XML
        }

        var workbook = Load(zip, "xl/workbook.xml");
        var sheets = workbook.Descendants(Main + "sheet").Select(s => (string)s.Attribute("name")!).ToList();
        Assert.Equal(["Summary", "Spaces", "Epics and stages", "Overdue", "Milestones", "People"], sheets);
        Assert.Equal(6, parts.Count(p => p.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)));
    }

    [Fact]
    public void Text_is_escaped_and_never_a_formula_and_dates_are_real_dates()
    {
        using var zip = new ZipArchive(new MemoryStream(MisExport.Xlsx(Report())));
        var spaces = Load(zip, "xl/worksheets/sheet2.xml");
        Assert.Contains(spaces.Descendants(Main + "t"), t => t.Value == "Data & <AI>");
        Assert.Empty(spaces.Descendants(Main + "f"));

        var overdue = Load(zip, "xl/worksheets/sheet4.xml");
        var summary = overdue.Descendants(Main + "c").Single(c => c.Value == "=HYPERLINK(\"x\")");
        Assert.Equal("inlineStr", (string?)summary.Attribute("t"));

        // 15 October 2026 as Excel's day number, in the date style.
        var due = overdue.Descendants(Main + "c").Single(c => (string?)c.Attribute("r") == "G2");
        Assert.Equal("46310", due.Value);
        Assert.Equal("2", (string?)due.Attribute("s"));
    }

    [Fact]
    public void The_print_page_escapes_text_and_shows_every_section()
    {
        var page = MisExport.PrintPage(Report(), "/");

        Assert.Contains("Portfolio MIS &#183; Data &amp; &lt;AI&gt;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<AI>", page, StringComparison.Ordinal);
        foreach (var section in new[] { "Spaces", "Epics and stages", "Milestones", "Overdue work", "People" })
        {
            Assert.Contains($"<h2>{section} ", page, StringComparison.Ordinal);
        }

        Assert.Contains("Off track", page, StringComparison.Ordinal);
        Assert.Contains("js/print-page.js", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_file_name_keeps_letters_and_digits_of_the_scope()
    {
        Assert.Equal("MIS-Data-AI-2026-10-20", MisExport.FileName(Report()));
    }

    private static XDocument Load(ZipArchive zip, string path)
    {
        using var stream = zip.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }
}
