# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

## Summary

Add centralized document upload, organization, search, preview, download, sharing, and audit to
ContosoDashboard. Documents are stored on the local filesystem outside `wwwroot` behind an
`IFileStorageService` abstraction, with metadata, shares, tags, and activity records in the
existing SQLite database through EF Core. All access decisions live in a single `DocumentService`
(IDOR protection, role rules, Personal Files privacy). Files are served only through authorized
endpoints. A replaceable `IMalwareScanService` (simulated, EICAR-detecting) and a content-signature
validator protect uploads. UI is added as Blazor pages and components (My Documents, Shared with
Me, project documents section, task detail page, dashboard widget, admin report). See
[research.md](research.md) for decisions.

## Technical Context

**Language/Version**: C# 12 on .NET 8.0 (ASP.NET Core 8.0, Blazor Server)  
**Primary Dependencies**: Microsoft.EntityFrameworkCore.Sqlite 8.0.0 (existing), Bootstrap 5.3 + Bootstrap Icons (existing). No new runtime packages.  
**Storage**: SQLite (`Data Source=ContosoDashboard.db`) for metadata; local filesystem under `AppData/uploads` (outside `wwwroot`) for file content  
**Testing**: xUnit test project `ContosoDashboard.Tests` using in-memory SQLite and a temp-directory file store (new; the repository has no tests today)  
**Target Platform**: Linux/Windows/macOS developer machines running Kestrel, fully offline  
**Project Type**: Single ASP.NET Core Blazor Server web application  
**Performance Goals**: Upload of 25 MB in under 30 s; list of 500 documents under 2 s; search under 2 s; preview under 3 s (spec SC-001 to SC-003)  
**Constraints**: Offline only, no cloud SDKs; files never reachable by URL under `wwwroot`; DocumentId is an integer; Category stored as text; FileType column 255 chars; database created by `EnsureCreated()` (no migrations)  
**Scale/Scope**: Training scale (tens of users, hundreds of documents); about 9 new source files in `Models`/`Services`/`Endpoints`, 7 new pages and components, 6 edited files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| I. Training-Only Scope | Simplifications (simulated scanner, `EnsureCreated()`, local storage) documented with production alternative | PASS - documented in research.md and README update |
| II. Offline-First with Infrastructure Abstraction | Storage and scanning behind interfaces, chosen by DI/config, no cloud dependency | PASS - `IFileStorageService`, `IMalwareScanService` |
| III. Defense-in-Depth Security (NON-NEGOTIABLE) | Middleware + page `[Authorize]` + service-level checks; IDOR protection; no secrets committed; OWASP safe | PASS - endpoints use `RequireAuthorization()`, every service method re-checks access, GUID paths, traversal guard, extension and signature allow-list |
| IV. Layered Architecture | Pages use services only; new entities registered in `ApplicationDbContext` with relationships and indexes | PASS - pages call `IDocumentService`; entities added with indexes |
| V. Simplicity and SDD | Work follows spec; complexity justified; unique path before DB insert | PASS with two justified additions (see Complexity Tracking) |
| Technology constraints | SQLite via EF Core; connection string in config; security headers kept; no restore warnings | PASS - the preview endpoint relaxes `X-Frame-Options` to `SAMEORIGIN` for that route only; other headers unchanged |

**Post-design re-check**: PASS. No new violations introduced by the data model or contracts.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── http-endpoints.md
│   └── service-interfaces.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Program.cs                         # edit: register services, map document endpoints, storage options
├── appsettings.json                   # edit: DocumentStorage section
├── Data/
│   └── ApplicationDbContext.cs        # edit: DbSets, relationships, indexes for new entities
├── Models/
│   ├── Document.cs                    # new: Document, DocumentTag, DocumentShare, DocumentActivity, constants
│   └── Notification.cs                # edit: add NotificationType.DocumentShared, DocumentAdded
├── Services/
│   ├── FileStorageService.cs          # new: IFileStorageService + LocalFileStorageService
│   ├── MalwareScanService.cs          # new: IMalwareScanService + SimulatedMalwareScanService
│   ├── DocumentValidator.cs           # new: extension allow-list, size, content-signature checks
│   ├── DocumentService.cs             # new: IDocumentService + implementation (all authorization rules)
│   └── DashboardService.cs            # edit: document count in DashboardSummary
├── Endpoints/
│   └── DocumentEndpoints.cs           # new: authorized download and preview endpoints
├── Pages/
│   ├── Documents.razor                # new: /documents (My Documents, Shared with Me, search)
│   ├── DocumentReports.razor          # new: /documents/reports (Administrator only)
│   ├── TaskDetails.razor              # new: /tasks/{taskId:int} with documents panel
│   ├── ProjectDetails.razor           # edit: Project Documents card
│   ├── Index.razor                    # edit: Recent Documents widget, document count card
│   ├── Tasks.razor                    # edit: link task rows to TaskDetails
│   └── Login.cshtml.cs                # edit: add Department claim
├── Shared/
│   ├── DocumentUploadForm.razor       # new: multi-file upload form with progress
│   ├── DocumentList.razor             # new: reusable table with sort, filter, actions
│   ├── DocumentShareDialog.razor      # new: share/revoke with users or department
│   └── NavMenu.razor                  # edit: Documents link
└── wwwroot/css/site.css               # edit: small widget styles

ContosoDashboard.Tests/                # new
├── ContosoDashboard.Tests.csproj
├── DocumentServiceAuthorizationTests.cs
├── DocumentUploadTests.cs
├── DocumentValidatorTests.cs
├── DocumentActivityTests.cs
└── TestSupport/                       # in-memory SQLite context, temp storage, seeded users

README.md                              # edit: feature docs, SQLite reset, limitations
.gitignore                             # edit: AppData/ and *.db
```

**Structure Decision**: Keep the existing single web project and its Models/Data/Services/Pages/Shared
layering (Principle IV). Add one `Endpoints` folder because file retrieval must be an HTTP endpoint
(Blazor components cannot stream authorized binary responses). Add one separate test project because
the constitution requires access-control behavior to be covered by automated tests or documented manual
steps and SC-007 requires tests across every role.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New `ContosoDashboard.Tests` project (repo has none) | SC-007 requires role-by-role access tests; constitution Principle III review gate | Manual-only verification does not give repeatable proof of IDOR protection across four roles and sharing rules |
| Minimal API endpoints alongside Blazor/Razor Pages | Files live outside `wwwroot` and must be streamed after an authorization check (FR-011) | Serving via static files bypasses authorization; reading files in a Blazor component cannot return a browser download or inline preview |
