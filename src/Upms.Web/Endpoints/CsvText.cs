namespace Upms.Web.Endpoints;

/// <summary>Spreadsheet cells for the exports.</summary>
public static class CsvText
{
    /// <summary>A quoted cell; text that a spreadsheet would run as a formula starts with an apostrophe (OWASP CSV injection).</summary>
    public static string Cell(string value)
    {
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
