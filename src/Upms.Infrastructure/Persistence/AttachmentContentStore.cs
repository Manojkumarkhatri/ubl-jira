using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Upms.Application.Work;

namespace Upms.Infrastructure.Persistence;

/// <summary>A file's bytes, one row per attachment (Phase 4 research R12). Kept out of the entity model's reads and written
/// and read as streams through SqlClient, so a 25 MB file is never held whole in memory.</summary>
public sealed class AttachmentContent
{
    private AttachmentContent()
    {
    }

    public long AttachmentId { get; private set; }

    public byte[] Content { get; private set; } = [];
}

internal sealed class AttachmentContentStore(AppDbContext db) : IAttachmentContentStore
{
    public async Task WriteAsync(long attachmentId, Stream content, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = (SqlCommand)db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;
            command.CommandText = "INSERT INTO [AttachmentContents] ([AttachmentId], [Content]) VALUES (@id, @content)";
            command.CommandTimeout = 300;
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = attachmentId;
            command.Parameters.Add("@content", SqlDbType.VarBinary, -1).Value = content; // streamed by SqlClient
            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<Stream?> OpenReadAsync(long attachmentId, CancellationToken ct)
    {
        // Its own connection, so the stream can be read while the context does other work, and lives as long as the stream.
        var connection = new SqlConnection(db.Database.GetConnectionString());
        try
        {
            await connection.OpenAsync(ct);
            var command = connection.CreateCommand();
            command.CommandText = "SELECT [Content] FROM [AttachmentContents] WHERE [AttachmentId] = @id";
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = attachmentId;
            var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow, ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.DisposeAsync();
                await command.DisposeAsync();
                await connection.DisposeAsync();
                return null;
            }

            return new OwnedStream(reader.GetStream(0), reader, command, connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task DeleteAsync(long attachmentId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [AttachmentContents] WHERE [AttachmentId] = {attachmentId}", ct);

    /// <summary>A read-only stream that closes its reader, command and connection with it.</summary>
    private sealed class OwnedStream(Stream inner, SqlDataReader reader, SqlCommand command, SqlConnection connection) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            await reader.DisposeAsync();
            await command.DisposeAsync();
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                reader.Dispose();
                command.Dispose();
                connection.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
