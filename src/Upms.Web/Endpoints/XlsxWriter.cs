using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Upms.Web.Endpoints;

/// <summary>A small Excel workbook (.xlsx, Office Open XML) with several sheets, for the MIS. Text is written as inline
/// strings, so nothing in it can run as a formula; dates are real dates and numbers real numbers, so Excel sorts and
/// filters them. Header rows are bold, frozen and filterable.</summary>
internal sealed class XlsxWriter
{
    private readonly List<XlsxSheet> _sheets = [];

    public XlsxSheet AddSheet(string name)
    {
        // Excel allows 31 characters and none of : \ / ? * [ ].
        var clean = new string((name ?? "Sheet").Where(c => ":\\/?*[]".IndexOf(c) < 0).ToArray()).Trim();
        var sheet = new XlsxSheet(clean.Length == 0 ? $"Sheet{_sheets.Count + 1}" : clean[..Math.Min(31, clean.Length)]);
        _sheets.Add(sheet);
        return sheet;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes());
            Add(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(zip, "xl/workbook.xml", Workbook());
            Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Add(zip, "xl/styles.xml", Styles);
            for (var i = 0; i < _sheets.Count; i++)
            {
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", _sheets[i].ToXml());
            }
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private string ContentTypes()
    {
        var text = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (var i = 1; i <= _sheets.Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }

        return text.Append("</Types>").ToString();
    }

    private string Workbook()
    {
        var text = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
        for (var i = 0; i < _sheets.Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"""<sheet name="{XlsxSheet.Escape(_sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }

        text.Append("</sheets>");
        // Excel keeps each filter's range as a hidden name; the element is left out when no sheet has a filter.
        var names = new StringBuilder();
        for (var i = 0; i < _sheets.Count; i++)
        {
            if (_sheets[i].FilterRange() is { } range)
            {
                names.Append(CultureInfo.InvariantCulture,
                    $"""<definedName name="_xlnm._FilterDatabase" localSheetId="{i}" hidden="1">'{XlsxSheet.Escape(_sheets[i].Name.Replace("'", "''", StringComparison.Ordinal))}'!{range}</definedName>""");
            }
        }

        if (names.Length > 0)
        {
            text.Append("<definedNames>").Append(names).Append("</definedNames>");
        }

        return text.Append("</workbook>").ToString();
    }

    private string WorkbookRels()
    {
        var text = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (var i = 0; i < _sheets.Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"""<Relationship Id="rId{i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i + 1}.xml"/>""");
        }

        text.Append(CultureInfo.InvariantCulture,
            $"""<Relationship Id="rId{_sheets.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        return text.Append("</Relationships>").ToString();
    }

    // Cell styles: 0 normal, 1 header, 2 date, 3 title, 4 percent, 5 muted, 6 bold, 7 green, 8 amber, 9 red, 10 whole number.
    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="d mmm yyyy"/></numFmts><fonts count="7"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FF172B4D"/><name val="Calibri"/></font><font><b/><sz val="15"/><color rgb="FF172B4D"/><name val="Calibri"/></font><font><sz val="10"/><color rgb="FF626F86"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FF216E4E"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FF9A6700"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFAE2E24"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE9F2FF"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top/><bottom style="thin"><color rgb="FF8590A2"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="11"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"><alignment vertical="center" wrapText="1"/></xf><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="9" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="5" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="6" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="1" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}

/// <summary>A cell with a style other than its value's usual one.</summary>
internal readonly record struct XlsxCell(object? Value, XlsxStyle Style);

internal enum XlsxStyle
{
    Normal = 0,
    Header = 1,
    Date = 2,
    Title = 3,
    Percent = 4,
    Muted = 5,
    Bold = 6,
    Good = 7,
    Warning = 8,
    Bad = 9,
    Number = 10,
}

internal sealed class XlsxSheet(string name)
{
    private readonly List<object?[]> _rows = [];
    private double[] _widths = [];
    private int? _headerRow;
    private int _headerColumns;

    public string Name { get; } = name;

    public XlsxSheet Widths(params double[] widths)
    {
        _widths = widths;
        return this;
    }

    public XlsxSheet Row(params object?[] cells)
    {
        _rows.Add(cells);
        return this;
    }

    /// <summary>The table's header: bold, frozen below and filterable.</summary>
    public XlsxSheet Header(params string[] names)
    {
        _headerRow = _rows.Count + 1;
        _headerColumns = names.Length;
        _rows.Add(names.Select(n => (object?)new XlsxCell(n, XlsxStyle.Header)).ToArray());
        return this;
    }

    public XlsxSheet Blank() => Row();

    public string? FilterRange() => _headerRow is { } row && _headerColumns > 0
        ? $"$A${row}:${Column(_headerColumns - 1)}${Math.Max(row, _rows.Count)}"
        : null;

    public string ToXml()
    {
        var xml = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        if (_headerRow is { } frozen)
        {
            xml.Append(CultureInfo.InvariantCulture,
                $"""<sheetViews><sheetView workbookViewId="0"><pane ySplit="{frozen}" topLeftCell="A{frozen + 1}" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        }

        if (_widths.Length > 0)
        {
            xml.Append("<cols>");
            for (var i = 0; i < _widths.Length; i++)
            {
                xml.Append(CultureInfo.InvariantCulture, $"""<col min="{i + 1}" max="{i + 1}" width="{_widths[i].ToString("0.#", CultureInfo.InvariantCulture)}" customWidth="1"/>""");
            }

            xml.Append("</cols>");
        }

        xml.Append("<sheetData>");
        for (var r = 0; r < _rows.Count; r++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (var c = 0; c < _rows[r].Length; c++)
            {
                AppendCell(xml, $"{Column(c)}{r + 1}", _rows[r][c]);
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData>");
        if (FilterRange() is { } range)
        {
            xml.Append(CultureInfo.InvariantCulture, $"""<autoFilter ref="{range.Replace("$", "", StringComparison.Ordinal)}"/>""");
        }

        xml.Append("""<pageMargins left="0.5" right="0.5" top="0.6" bottom="0.6" header="0.3" footer="0.3"/><pageSetup orientation="landscape" fitToWidth="1" fitToHeight="0"/></worksheet>""");
        return xml.ToString();
    }

    private static void AppendCell(StringBuilder xml, string reference, object? value)
    {
        var style = XlsxStyle.Normal;
        if (value is XlsxCell cell)
        {
            style = cell.Style;
            value = cell.Value;
        }

        var s = style == XlsxStyle.Normal ? "" : $" s=\"{(int)style}\"";
        switch (value)
        {
            case null:
                if (style != XlsxStyle.Normal)
                {
                    xml.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}"{s}/>""");
                }

                break;
            case DateOnly date:
                xml.Append(CultureInfo.InvariantCulture,
                    $"""<c r="{reference}" s="{(int)(style == XlsxStyle.Normal ? XlsxStyle.Date : style)}"><v>{Serial(date)}</v></c>""");
                break;
            case DateTimeOffset moment:
                xml.Append(CultureInfo.InvariantCulture,
                    $"""<c r="{reference}" s="{(int)(style == XlsxStyle.Normal ? XlsxStyle.Date : style)}"><v>{Serial(DateOnly.FromDateTime(moment.UtcDateTime))}</v></c>""");
                break;
            case int or long or double or decimal:
                xml.Append(CultureInfo.InvariantCulture,
                    $"""<c r="{reference}"{s}><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>""");
                break;
            default:
                xml.Append(CultureInfo.InvariantCulture,
                    $"""<c r="{reference}" t="inlineStr"{s}><is><t xml:space="preserve">{Escape(value.ToString() ?? "")}</t></is></c>""");
                break;
        }
    }

    /// <summary>Excel's day number: days since 30 December 1899.</summary>
    private static int Serial(DateOnly date) => date.DayNumber - new DateOnly(1899, 12, 30).DayNumber;

    private static string Column(int index)
    {
        var name = "";
        for (var i = index + 1; i > 0; i = (i - 1) / 26)
        {
            name = (char)('A' + ((i - 1) % 26)) + name;
        }

        return name;
    }

    /// <summary>XML text with the characters XML cannot hold left out.</summary>
    public static string Escape(string text)
    {
        var clean = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (XmlConvert.IsXmlChar(c) || char.IsSurrogate(c))
            {
                clean.Append(c);
            }
        }

        return System.Security.SecurityElement.Escape(clean.ToString()) ?? "";
    }
}
