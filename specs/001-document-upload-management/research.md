# Research: Document Upload and Management

All technical-context unknowns are resolved below. No `NEEDS CLARIFICATION` remains.

## Codebase findings that shape the design

- Blazor Server + Razor Pages only; no MVC controllers. `Program.cs` uses `AddRazorPages`, `AddServerSideBlazor`, cookie auth, and a global middleware that sets `X-Frame-Options: DENY` and a CSP with `default-src 'self'`.
- Services take the requesting `userId` and enforce authorization (`ProjectService.GetProjectByIdAsync`, `TaskService.GetTaskByIdAsync`). Pages read the user id from `AuthenticationStateProvider`.
- `Login.cshtml.cs` issues claims NameIdentifier, Name, Email, Role. **No Department claim** (stakeholder doc says it is required).
- There is **no task detail page**: `Tasks.razor` has a `TODO` for details. Task integration (FR-034) needs a new page.
- Dashboard summary cards are four `col-md-3` columns; `DashboardSummary` is a small class returned by `DashboardService`.
- `NotificationType` is an enum stored as integer; new values must be appended to keep existing data valid.
- Database is created with `EnsureCreated()`: new tables are not added to an existing `ContosoDashboard.db`. Developers must delete the file once (README already documents this).

## Decisions

### D1. Authorization lives in one `DocumentService`
- **Decision**: A single `IDocumentService` implementation computes visibility (query filter) and per-action permissions (view, manage, share). It loads the requesting user from the database (role, department) rather than trusting claims.
- **Rationale**: Principle III/IV: one place for IDOR rules; DB-sourced role avoids stale or missing claims. A Department claim is still added at login to match the stakeholder document.
- **Alternatives**: Claims-only checks (fail when claims are missing; stale after role change); per-page checks (duplicated, easy to miss).

### D2. Access rules (from spec and clarifications)
- Visible if any of: uploader; Administrator; member or manager of the document's project; direct share to the user; share to the user's department; Team Lead of the uploader's department when category is not Personal Files.
- Manage (edit, replace, delete): uploader; Administrator; Project Manager of the document's project; Team Lead of the uploader's department when category is not Personal Files.
- Share/revoke: uploader only. Recipients are read-only (clarification Q3).
- Upload to a project: project member, project manager, or Administrator (FR-010).
- Personal Files are never visible to a Team Lead who is not the owner (clarification Q2).
- **Rationale**: Directly encodes FR-010, FR-025, FR-029 to FR-033.

### D3. File storage
- **Decision**: `IFileStorageService` with `UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`. `LocalFileStorageService` stores under a configured root (`AppData/uploads`, relative to the content root, outside `wwwroot`). Relative path pattern `{userId}/{projectId|personal}/{guid}.{ext}`. The service resolves the full path and rejects anything outside the root (path traversal guard). `GetUrlAsync` throws `NotSupportedException` locally because files are never URL-addressable (FR-011); a cloud implementation would return a time-limited URL.
- **Upload sequence**: validate, authorize, scan, build unique path, save file, insert database row; if the row insert fails, delete the file. Replace: write new file, update row, then delete the old file.
- **Rationale**: Matches stakeholder notes; no orphaned rows (FR-013); extension taken from the allow-list, never from user-supplied path text (FR-012).
- **Alternatives**: Storing blobs in SQLite (bloats DB, differs from cloud migration path); using `wwwroot` (bypasses authorization).

### D4. Validation
- **Decision**: `DocumentValidator` checks, in order: non-empty, size at most 25 MB, extension in the allow-list (`.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`), content signature matches the extension (PDF `%PDF`, JPEG `FF D8 FF`, PNG `89 50 4E 47`, OOXML ZIP `50 4B`, legacy Office OLE `D0 CF 11 E0`, text has no NUL bytes). The stored MIME type comes from a server-side extension map, not the browser-supplied value.
- **Rationale**: FR-002, FR-003, FR-009, and the "executable renamed to .pdf" edge case. Trusting the browser content type is unsafe.
- **Alternatives**: Extension-only check (rejects nothing disguised).

### D5. Malware scanning
- **Decision**: `IMalwareScanService.ScanAsync(Stream)` with `SimulatedMalwareScanService` that detects the EICAR test signature. Registered by DI so a real engine can replace it (clarification Q1).
- **Test hygiene**: tests build the EICAR string at runtime from concatenated pieces so the repository does not hold a literal that desktop antivirus would quarantine.
- **Alternatives**: ClamAV (external dependency, breaks offline-friendly setup); no scanning (violates FR-008).

### D6. Serving files
- **Decision**: Minimal API endpoints `GET /documents/{id}/download` and `GET /documents/{id}/preview`, both `RequireAuthorization()`, both calling `DocumentService` for access checks and activity logging. Preview only for PDF/JPEG/PNG, `Content-Disposition: inline`, and sets `X-Frame-Options: SAMEORIGIN` on that response so the PDF can render in an `<iframe>` (the global middleware sets `DENY`). CSP `default-src 'self'` already permits same-origin frames and images. Download uses `attachment` with the original name (sanitized). Unauthorized and missing documents both return 404 to avoid confirming existence.
- **Rationale**: FR-011, FR-020, FR-021, FR-033.

### D7. Blazor Server upload
- **Decision**: `InputFile` with `@key`, `maxAllowedFiles` set, `OpenReadStream(25 MB)`, copy to `MemoryStream` immediately, clear the `IBrowserFile` reference (stakeholder pattern). Progress shown per file with a status list. `InputFile` streams in chunks over SignalR, so the default hub message limit is not exceeded.
- **Rationale**: Prevents stream-disposal errors and reuse errors in Blazor; each file processed independently (FR-001, FR-004).

### D8. Data model choices
- **Decision**: Integer `DocumentId`; `Category` as text validated against a fixed list; `FileType` max length 255; `FilePath` required with a unique index; tags in a child table (`DocumentTag`) for searchable, normalized lowercase values; `DocumentActivity` has **no foreign key** to `Document` so history survives deletion, and stores title and file type snapshots (clarification Q5). `Action` stored as text.
- **Rationale**: Spec technical constraints and audit retention. The unique `FilePath` prevents the duplicate-key problem documented by stakeholders.
- **Alternatives**: Comma-separated tags (weak search and filtering); cascade-delete activity (loses audit trail).

### D9. Search and SQLite
- **Decision**: EF Core `Contains` translates to SQLite `LIKE`, which is case-insensitive for ASCII only. Accepted for training; documented as a limitation. Indexes on `UploadedByUserId`, `ProjectId`, `Category`, `UploadDate`, and `DocumentTag.Tag`.
- **Rationale**: Meets the 2-second target at 500 documents without full-text search; keeps portability to Azure SQL.

### D10. Notifications and dashboard
- **Decision**: Append `DocumentShared` and `DocumentAdded` to `NotificationType`; create notifications through the existing `INotificationService`. Add `TotalDocuments` (documents uploaded by the user) to `DashboardSummary`, plus `GetRecentDocumentsAsync(userId, 5)`. Add a fifth summary card by changing the card columns to responsive widths.

### D11. Task integration
- **Decision**: New `/tasks/{taskId:int}` page using `ITaskService.GetTaskByIdAsync` (already authorizes) with a documents panel: list, upload, and attach-existing (own documents). A document has at most one `TaskId`; attaching sets `ProjectId` to the task's project (FR-035). `Tasks.razor` links to the new page.

### D12. Reporting
- **Decision**: `/documents/reports` with policy `Administrator`, backed by `GetReportAsync` aggregating `DocumentActivity` and `Document` (uploads by type, uploads by user, previews and downloads separately and combined).

### D13. Testing
- **Decision**: xUnit project with in-memory SQLite (`DataSource=:memory:` kept open) and a temp-directory storage. Covers: per-role visibility and manage matrix, Personal Files privacy, share read-only, validator and scanner, upload sequence failure (no orphan row), activity retention after delete, report authorization. Package versions pinned at implementation time and restored without warnings.

## Production alternatives (to document, Principle I)
Azure Blob Storage for files, Microsoft Defender for Storage or ClamAV for scanning, migrations instead of `EnsureCreated()`, SQL full-text search, Azure AD for identity.
