using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public sealed record DocumentFilePayload
{
    public string FileName { get; init; } = string.Empty;
    public long Size { get; init; }
    public byte[] Content { get; init; } = [];
}

public sealed record DocumentUploadRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public int? ProjectId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DocumentFilePayload? File { get; init; }
}

public sealed record DocumentUploadResult(bool Success, int? DocumentId, string? Error)
{
    public static DocumentUploadResult Failed(string error) => new(false, null, error);
}

public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, int requestingUserId);
    Task<List<Document>> GetMyDocumentsAsync(int userId);
}

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IMalwareScanService _malwareScanner;
    private readonly DocumentValidator _validator;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService storage,
        IMalwareScanService malwareScanner,
        DocumentValidator validator)
    {
        _context = context;
        _storage = storage;
        _malwareScanner = malwareScanner;
        _validator = validator;
    }

    public async Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, int requestingUserId)
    {
        if (request.File is null)
        {
            return DocumentUploadResult.Failed("Select a file to upload.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255)
        {
            return DocumentUploadResult.Failed("Enter a title of 1 to 255 characters.");
        }

        if (!DocumentCategories.All.Contains(request.Category, StringComparer.Ordinal))
        {
            return DocumentUploadResult.Failed("Select a valid document category.");
        }

        if (request.Description?.Length > 2000)
        {
            return DocumentUploadResult.Failed("The description must be 2000 characters or fewer.");
        }

        if (request.File.Size != request.File.Content.LongLength)
        {
            return DocumentUploadResult.Failed("The uploaded file size could not be verified.");
        }

        var actor = await _context.Users.SingleOrDefaultAsync(user => user.UserId == requestingUserId);
        if (actor is null)
        {
            return DocumentUploadResult.Failed("Your account could not be verified.");
        }

        if (request.ProjectId is int projectId && !await CanUploadToProjectAsync(projectId, actor))
        {
            return DocumentUploadResult.Failed("You are not authorized to upload to this project.");
        }

        var originalFileName = DocumentValidator.SanitizeFileName(request.File.FileName);
        var content = request.File.Content;
        await using var contentStream = new MemoryStream(content, writable: false);
        var validation = await _validator.ValidateAsync(originalFileName, request.File.Size, contentStream);
        if (!validation.IsValid)
        {
            return DocumentUploadResult.Failed(validation.Error ?? "The file is invalid.");
        }

        var scan = await _malwareScanner.ScanAsync(contentStream, originalFileName);
        if (!scan.IsClean)
        {
            return DocumentUploadResult.Failed(scan.Reason ?? "The file was rejected by the malware scan.");
        }

        var tags = NormalizeTags(request.Tags, out var tagError);
        if (tagError is not null)
        {
            return DocumentUploadResult.Failed(tagError);
        }

        var relativePath = Path.Combine(
            requestingUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.ProjectId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "personal",
            $"{Guid.NewGuid():N}{validation.Extension}");
        var fileStored = false;

        try
        {
            contentStream.Position = 0;
            await _storage.UploadAsync(contentStream, relativePath, validation.ContentType);
            fileStored = true;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var document = new Document
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category = request.Category,
                OriginalFileName = originalFileName,
                FileType = validation.ContentType,
                FileSize = request.File.Size,
                FilePath = relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                UploadedByUserId = requestingUserId,
                ProjectId = request.ProjectId,
                UploadDate = now,
                UpdatedDate = now,
                Tags = tags.Select(tag => new DocumentTag { Tag = tag }).ToList()
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            _context.DocumentActivities.Add(new DocumentActivity
            {
                DocumentId = document.DocumentId,
                DocumentTitle = document.Title,
                DocumentFileType = document.FileType,
                UserId = requestingUserId,
                Action = DocumentActions.Upload,
                ActivityDate = now
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new DocumentUploadResult(true, document.DocumentId, null);
        }
        catch (Exception exception) when (exception is IOException or DbUpdateException or InvalidOperationException)
        {
            if (fileStored)
            {
                try
                {
                    await _storage.DeleteAsync(relativePath);
                }
                catch (IOException)
                {
                }
            }

            return DocumentUploadResult.Failed("The document could not be saved. Please try again.");
        }
    }

    public async Task<List<Document>> GetMyDocumentsAsync(int userId)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(document => document.UploadedByUserId == userId)
            .Include(document => document.Project)
            .Include(document => document.Tags)
            .OrderByDescending(document => document.UploadDate)
            .ToListAsync();
    }

    private async Task<bool> CanUploadToProjectAsync(int projectId, User actor)
    {
        if (actor.Role == UserRole.Administrator)
        {
            return await _context.Projects.AnyAsync(project => project.ProjectId == projectId);
        }

        return await _context.Projects.AnyAsync(project =>
            project.ProjectId == projectId &&
            (project.ProjectManagerId == actor.UserId || project.ProjectMembers.Any(member => member.UserId == actor.UserId)));
    }

    private static List<string> NormalizeTags(IReadOnlyList<string> suppliedTags, out string? error)
    {
        var tags = suppliedTags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tags.Count > 10 || tags.Any(tag => tag.Length > 50))
        {
            error = "Use no more than 10 tags, each 50 characters or fewer.";
            return [];
        }

        error = null;
        return tags;
    }
}
