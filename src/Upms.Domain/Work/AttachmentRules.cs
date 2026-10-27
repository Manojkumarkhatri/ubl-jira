using System.IO.Compression;
using System.Text;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>Which files can be attached and how they are named (Phase 4 FR-024, research R13): the allowed types, 25 MB,
/// file names without paths or control characters, and content that matches its type, so that a script renamed .png or
/// an HTML page renamed .txt is refused.</summary>
public static class AttachmentRules
{
    public const long MaxSize = 25 * 1024 * 1024;
    public const int FileNameMaxLength = 255;
    public const string FileTypeNotAllowedCode = "FileTypeNotAllowed";
    public const string FileTooLargeCode = "FileTooLarge";
    public const string FileContentMismatchCode = "FileContentMismatch";
    public const string NotAvailableCode = "AttachmentNotAvailable";

    private static readonly HashSet<string> Images = ["png", "jpg", "jpeg", "gif", "webp"];
    private static readonly HashSet<string> LegacyOffice = ["doc", "xls", "ppt"];
    private static readonly HashSet<string> OfficeOpenXml = ["docx", "xlsx", "pptx"];
    private static readonly HashSet<string> Texts = ["txt", "log", "csv", "json", "xml", "yaml", "yml", "md", "sql", "ipynb"];

    /// <summary>Every extension that can be attached (FR-024).</summary>
    public static IReadOnlySet<string> Extensions { get; } = new HashSet<string>([.. Images, "pdf", .. LegacyOffice, .. OfficeOpenXml, .. Texts]);

    /// <summary>The name without any folder, control characters or surrounding spaces, at most 255 characters with its
    /// extension kept.</summary>
    public static string CleanFileName(string? uploaded)
    {
        var name = uploaded ?? "";
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length <= FileNameMaxLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        return extension.Length >= FileNameMaxLength ? name[..FileNameMaxLength] : name[..(FileNameMaxLength - extension.Length)] + extension;
    }

    /// <summary>The lower-case extension without its dot, or "" for none.</summary>
    public static string ExtensionOf(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

    public static DomainError? CheckType(string fileName) =>
        Extensions.Contains(ExtensionOf(fileName)) && Path.GetFileNameWithoutExtension(fileName).Trim().Length > 0
            ? null
            : DomainError.Rule(FileTypeNotAllowedCode,
                "This type of file can't be attached. Allowed: images, PDF, Word, Excel, PowerPoint, text, log, CSV, JSON, XML, YAML, Markdown, SQL and Jupyter notebooks.");

    public static DomainError? CheckSize(long size) => size switch
    {
        <= 0 => DomainError.Invalid("File", "This file is empty."),
        > MaxSize => DomainError.Rule(FileTooLargeCode, "Files can be up to 25 MB."),
        _ => null,
    };

    /// <summary>Whether the content is what its extension says (research R13). Reads the seekable stream and leaves it at
    /// its start.</summary>
    public static DomainError? CheckContent(string extension, Stream content)
    {
        content.Position = 0;
        try
        {
            var matches = extension switch
            {
                "png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                "jpg" or "jpeg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
                "gif" => StartsWith(content, "GIF87a"u8) || StartsWith(content, "GIF89a"u8),
                "webp" => StartsWith(content, "RIFF"u8) && StartsWith(content, "WEBP"u8, offset: 8),
                "pdf" => StartsWith(content, "%PDF-"u8),
                _ when LegacyOffice.Contains(extension) => StartsWith(content, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
                _ when OfficeOpenXml.Contains(extension) => IsOfficePackage(content),
                _ when Texts.Contains(extension) => IsUtf8Text(content),
                _ => false,
            };
            return matches ? null : DomainError.Rule(FileContentMismatchCode, "This file's content doesn't match its type.");
        }
        finally
        {
            content.Position = 0;
        }
    }

    /// <summary>Images keep their type so they can be previewed; everything else is served as a download.</summary>
    public static string ContentTypeOf(string extension) => extension switch
    {
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "gif" => "image/gif",
        "webp" => "image/webp",
        _ => "application/octet-stream",
    };

    public static bool IsImage(string extension) => Images.Contains(extension);

    private static bool StartsWith(Stream content, ReadOnlySpan<byte> signature, int offset = 0)
    {
        Span<byte> head = stackalloc byte[signature.Length];
        content.Position = offset;
        return content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length && head.SequenceEqual(signature);
    }

    /// <summary>A Word, Excel or PowerPoint file is a ZIP package listing its parts in [Content_Types].xml.</summary>
    private static bool IsOfficePackage(Stream content)
    {
        if (!StartsWith(content, [0x50, 0x4B, 0x03, 0x04]))
        {
            return false;
        }

        content.Position = 0;
        try
        {
            using var package = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return package.GetEntry("[Content_Types].xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Text and technical files are UTF-8, with or without a byte order mark, and never contain a NUL byte.</summary>
    private static bool IsUtf8Text(Stream content)
    {
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var bytes = new byte[16 * 1024];
        var chars = new char[bytes.Length + 1];
        try
        {
            int read;
            while ((read = content.Read(bytes)) > 0)
            {
                if (bytes.AsSpan(0, read).Contains((byte)0))
                {
                    return false;
                }

                decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            }

            decoder.GetChars([], 0, 0, chars, 0, flush: true);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
