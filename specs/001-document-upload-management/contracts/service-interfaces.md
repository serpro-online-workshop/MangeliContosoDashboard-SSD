# Contract: Service Interfaces

Signatures describe the contract that pages and tests rely on. Parameter lists may gain optional arguments during implementation but the semantics below are fixed.

## IFileStorageService (infrastructure abstraction, Principle II)

```csharp
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string filePath, string contentType);
    Task DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
    Task<string> GetUrlAsync(string filePath, TimeSpan expiration);
}
```

- `filePath` is the relative path `{userId}/{projectId|personal}/{guid}.{ext}` built by `DocumentService`; implementations return the stored path.
- `LocalFileStorageService` rejects any path resolving outside its root and throws `NotSupportedException` from `GetUrlAsync` (no URL-addressable files).
- `DeleteAsync` is idempotent (missing file is not an error).

## IMalwareScanService

```csharp
public interface IMalwareScanService
{
    Task<MalwareScanResult> ScanAsync(Stream content, string fileName);
}
public record MalwareScanResult(bool IsClean, string? Reason);
```

- The simulated implementation returns `IsClean = false` when the stream contains the EICAR test signature.

## IDocumentService

All methods take the requesting `userId` and enforce authorization internally. A method that returns `null` or `false` for a document the user cannot see must behave identically for a missing document.

```csharp
public interface IDocumentService
{
    // Upload and validation
    Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, int requestingUserId);
    Task<DocumentUploadResult> ReplaceFileAsync(int documentId, DocumentFilePayload file, int requestingUserId);

    // Browsing and search
    Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentQuery query);
    Task<List<Document>> GetSharedWithMeAsync(int userId, DocumentQuery query);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int userId);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, int userId);
    Task<List<Document>> SearchAsync(string term, int userId);
    Task<List<Document>> GetRecentDocumentsAsync(int userId, int count);
    Task<int> GetDocumentCountAsync(int userId);

    // Retrieval (used by the HTTP endpoints)
    Task<DocumentContent?> OpenForDownloadAsync(int documentId, int userId);
    Task<DocumentContent?> OpenForPreviewAsync(int documentId, int userId);

    // Management
    Task<bool> UpdateMetadataAsync(int documentId, DocumentMetadata metadata, int userId);
    Task<bool> AttachToTaskAsync(int documentId, int taskId, int userId);
    Task<bool> DeleteAsync(int documentId, int userId);

    // Sharing
    Task<bool> ShareWithUserAsync(int documentId, int targetUserId, int userId);
    Task<bool> ShareWithDepartmentAsync(int documentId, string department, int userId);
    Task<bool> RevokeShareAsync(int documentShareId, int userId);
    Task<List<DocumentShare>> GetSharesAsync(int documentId, int userId);

    // Reporting (Administrator only)
    Task<DocumentReport?> GetReportAsync(int userId, DateTime? from, DateTime? to);
}
```

### Result and request shapes

- `DocumentUploadRequest`: `Title`, `Description?`, `Category`, `ProjectId?`, `TaskId?`, `Tags`, `File` (`DocumentFilePayload`: original name, size, buffered content).
- `DocumentUploadResult`: `Success`, `DocumentId?`, `Error?` (user-facing reason; one result per file).
- `DocumentQuery`: `Category?`, `ProjectId?`, `From?`, `To?`, `SortBy` (`Title|UploadDate|Category|FileSize`), `Descending`.
- `DocumentContent`: stream, stored content type, download file name. Caller disposes the stream.
- `DocumentReport`: uploads by type, uploads by user, previews, downloads, combined access counts.

### Behavioral guarantees (testable)

1. A user never receives a `Document` they are not permitted to see, from any list, search, or open method.
2. `Personal Files` documents of other users are never visible to a Team Lead unless owned or shared.
3. Recipients of a share cannot update, replace, delete, attach, or re-share.
4. A failed upload leaves neither a database row nor a stored file.
5. `DeleteAsync` removes the row and the file, keeps all `DocumentActivity` rows, and writes a `Delete` activity with the title and file type snapshot.
6. `GetReportAsync` returns `null` for non-Administrators.
7. Each successful upload, download, preview, share, and delete writes exactly one `DocumentActivity`.
8. Share and project-add notifications are created via `INotificationService` for recipients and other project members (not the actor).
