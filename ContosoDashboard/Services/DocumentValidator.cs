using System.Text;

namespace ContosoDashboard.Services;

public sealed record DocumentValidationResult(bool IsValid, string? Error, string ContentType, string Extension);

public sealed class DocumentValidator
{
    public const long MaximumFileSize = 25L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    public async Task<DocumentValidationResult> ValidateAsync(string fileName, long fileSize, Stream content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (fileSize <= 0)
        {
            return Invalid("Empty files are not supported.");
        }

        if (fileSize > MaximumFileSize)
        {
            return Invalid("Files must be 25 MB or smaller.");
        }

        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            return Invalid("This file type is not supported.");
        }

        var position = content.CanSeek ? content.Position : 0;
        var header = new byte[Math.Min(8, checked((int)Math.Min(fileSize, 8)))];
        var read = await content.ReadAsync(header);
        if (content.CanSeek)
        {
            content.Position = position;
        }

        if (!MatchesSignature(extension, header.AsSpan(0, read)))
        {
            return Invalid("The file contents do not match the file extension.");
        }

        return new DocumentValidationResult(true, null, contentType, extension.ToLowerInvariant());
    }

    public static string SanitizeFileName(string fileName)
    {
        var leafName = Path.GetFileName(fileName.Replace('\\', '/'));
        var sanitized = new string(leafName.Where(character => !char.IsControl(character) && character is not '"' and not '\'').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized[..Math.Min(sanitized.Length, 255)];
    }

    private static bool MatchesSignature(string extension, ReadOnlySpan<byte> header)
    {
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return header.StartsWith("%PDF"u8);
        }

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF });
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        }

        if (extension is ".docx" or ".xlsx" or ".pptx")
        {
            return header.StartsWith(new byte[] { 0x50, 0x4B });
        }

        if (extension is ".doc" or ".xls" or ".ppt")
        {
            return header.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 });
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return !header.Contains((byte)0) && IsTextHeader(header);
        }

        return false;
    }

    private static bool IsTextHeader(ReadOnlySpan<byte> header)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetString(header);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static DocumentValidationResult Invalid(string error) => new(false, error, string.Empty, string.Empty);
}
