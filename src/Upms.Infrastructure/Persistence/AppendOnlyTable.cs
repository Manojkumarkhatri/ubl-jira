namespace Upms.Infrastructure.Persistence;

/// <summary>SQL for the triggers that make history and audit tables append-only (constitution IV,
/// research R17): any UPDATE or DELETE fails.</summary>
internal static class AppendOnlyTable
{
    public static string CreateTrigger(string table, string trigger) => $"""
        CREATE TRIGGER [dbo].[{trigger}] ON [dbo].[{table}]
        INSTEAD OF UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            THROW 50001, N'{table} is append-only: rows cannot be updated or deleted.', 1;
        END
        """;

    public static string DropTrigger(string trigger) => $"DROP TRIGGER IF EXISTS [dbo].[{trigger}];";
}
