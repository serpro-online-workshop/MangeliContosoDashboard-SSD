---

description: "Task list for Document Upload and Management"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-upload-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Tests**: Included. The plan adds a test project because the constitution (Principle III) and spec SC-007 require repeatable, role-by-role access tests. Write each story's tests first and confirm they fail before implementing.
**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Maps the task to a user story (US1 to US8) from spec.md
- All paths are relative to the repository root `/home/s069607207/ambienteWorkshop/MangeliContosoDashboard-SSD`
- `DocumentService.cs` is touched by many stories, so those tasks are never marked [P]

## Path Conventions

- Web project: `ContosoDashboard/` (Models, Data, Services, Endpoints, Pages, Shared)
- Test project: `ContosoDashboard.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration, ignore rules, and the test project

- [ ] T001 Add a `DocumentStorage` section with `RootPath` set to `AppData/uploads` in `ContosoDashboard/appsettings.json`
- [ ] T002 [P] Add `AppData/` and `*.db` entries to `.gitignore`
- [ ] T003 Create `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` (xUnit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, Microsoft.EntityFrameworkCore.Sqlite 8.0.x, project reference to `ContosoDashboard/ContosoDashboard.csproj`); pin versions that restore without NU1603 warnings
- [ ] T004 Create `ContosoDashboard.sln` at the repository root containing both projects (depends on T003)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, storage, scanning, validation, and the authorization core that every story depends on

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T005 [P] Create `ContosoDashboard/Models/Document.cs` with `Document`, `DocumentTag`, `DocumentShare`, `DocumentActivity`, a `DocumentCategories` constants class (Project Documents, Team Resources, Personal Files, Reports, Presentations, Other), and a `DocumentActions` constants class (Upload, Download, Preview, Delete, Share). Apply data-model.md constraints verbatim: `Title` `[Required]` `[MaxLength(255)]`; `Description` nullable `[MaxLength(2000)]`; `Category` `[Required]` `[MaxLength(50)]` stored as text; `OriginalFileName` `[Required]` `[MaxLength(255)]`; `FileType` `[Required]` `[MaxLength(255)]`; `FileSize` `long`; `FilePath` `[Required]` `[MaxLength(500)]`; `DocumentTag.Tag` `[MaxLength(50)]`; `DocumentShare.SharedWithDepartment` nullable `[MaxLength(100)]`; `DocumentActivity.Action` `[MaxLength(20)]`, `DocumentTitle` and `DocumentFileType` snapshots, and no navigation or foreign key to `Document`
- [ ] T006 [P] Append `DocumentShared` and `DocumentAdded` to the end of `NotificationType` in `ContosoDashboard/Models/Notification.cs` (keep existing integer values)
- [ ] T007 Add `Documents` navigation collections to `ContosoDashboard/Models/User.cs`, `ContosoDashboard/Models/Project.cs`, and `ContosoDashboard/Models/TaskItem.cs` (depends on T005)
- [ ] T008 Register `Documents`, `DocumentTags`, `DocumentShares`, `DocumentActivities` DbSets in `ContosoDashboard/Data/ApplicationDbContext.cs` with relationships (uploader restrict delete; project and task optional; tags and shares cascade from document; activity no foreign key), a unique index on `Document.FilePath`, indexes on `UploadedByUserId`, `ProjectId`, `TaskId`, `Category`, `UploadDate`, unique (`DocumentId`, `Tag`), unique share indexes, and activity indexes (depends on T005)
- [ ] T009 [P] Create `ContosoDashboard/Services/FileStorageService.cs` with `IFileStorageService` (`UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`) and `LocalFileStorageService`: root from `DocumentStorage:RootPath` resolved against the content root (outside `wwwroot`), reject any resolved path outside the root, idempotent delete, `GetUrlAsync` throws `NotSupportedException`
- [ ] T010 [P] Create `ContosoDashboard/Services/MalwareScanService.cs` with `IMalwareScanService`, `MalwareScanResult`, and `SimulatedMalwareScanService` that returns not clean when the stream contains the EICAR test signature
- [ ] T011 [P] Create `ContosoDashboard/Services/DocumentValidator.cs`: reject empty files, size over 26,214,400 bytes, extensions outside `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`, and content whose signature does not match the extension (PDF `%PDF`, JPEG `FF D8 FF`, PNG `89 50 4E 47`, OOXML `50 4B`, legacy Office `D0 CF 11 E0`, text without NUL bytes); provide a server-side extension to MIME type map and a file-name sanitizer; return user-facing error messages
- [ ] T012 Create `ContosoDashboard/Services/DocumentService.cs` with the `IDocumentService` interface per contracts/service-interfaces.md (request, result, query, content, report records included), the `DocumentService` class with constructor (context, storage, scanner, validator, `INotificationService`), a central `GetVisibleDocuments(user)` query implementing the rules in research.md D2 (uploader, Administrator, project member or manager, direct or department share, Team Lead of the uploader's department excluding Personal Files), `CanManage` and `CanShare` helpers, and a `LogActivityAsync` helper that stores title and file-type snapshots; load the requesting user from the database, not from claims (depends on T005, T008, T009, T010, T011)
- [ ] T013 Register `IFileStorageService`, `IMalwareScanService`, `DocumentValidator`, and `IDocumentService` (scoped) in `ContosoDashboard/Program.cs` (depends on T009, T010, T011, T012)
- [ ] T014 [P] Add the `Department` claim to the claims list in `ContosoDashboard/Pages/Login.cshtml.cs`
- [ ] T015 [P] Create `ContosoDashboard.Tests/TestSupport/` with an in-memory SQLite `ApplicationDbContext` factory (connection kept open, `EnsureCreated`), a temp-directory `IFileStorageService` fixture with cleanup, seeded users for all four roles in two departments, a project with members, and a helper that builds the EICAR string at runtime from concatenated parts so no literal is committed (depends on T003, T008)

**Checkpoint**: Foundation ready - user stories can begin

---

## Phase 3: User Story 1 - Upload a Document with Metadata (Priority: P1) 🎯 MVP

**Goal**: An employee uploads one or more files with metadata, sees progress and per-file results, and finds the document in their list.

**Independent Test**: Log in as Ni Kang, upload a valid PDF with title and category, and confirm it appears in My Documents with date, uploader, size, and type; confirm oversize, wrong type, disguised, and EICAR files are rejected with specific messages.

### Tests for User Story 1

- [ ] T016 [P] [US1] Write `ContosoDashboard.Tests/DocumentValidatorTests.cs`: size limit, zero-byte, extension allow-list, signature mismatch (renamed executable as `.pdf`), MIME type mapping, file-name sanitizing, scanner rejecting the runtime-built EICAR string
- [ ] T017 [P] [US1] Write `ContosoDashboard.Tests/DocumentUploadTests.cs`: valid upload stores file at `{userId}/personal/{guid}.ext` and a row with recorded date, uploader, size, type; missing title or category rejected; unknown category rejected; project upload by a non-member rejected; Administrator allowed; multi-file batch with one invalid file still stores the valid ones; storage failure leaves no row; database failure deletes the stored file; `Upload` activity written once

### Implementation for User Story 1

- [ ] T018 [US1] Implement `UploadAsync` in `ContosoDashboard/Services/DocumentService.cs`: validate, authorize project association (member, manager, or Administrator), scan, build the unique relative path before any database write, save the file, insert the row and normalized tags (trimmed, lowercase, unique, at most 10, each 1 to 50 chars), delete the file if the insert fails, log `Upload` activity, return a per-file `DocumentUploadResult` (depends on T012)
- [ ] T019 [US1] Implement `GetMyDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` returning the user's uploaded documents newest first (filters and sorting come in US2) (depends on T018)
- [ ] T020 [P] [US1] Create `ContosoDashboard/Shared/DocumentUploadForm.razor`: `InputFile` with `@key` and multiple files, shared category, project, description, tags, per-file editable title pre-filled from the file name, copy each file to a `MemoryStream` immediately and clear the `IBrowserFile` reference, per-file progress and success or error results, validation messages for missing required fields
- [ ] T021 [P] [US1] Create `ContosoDashboard/Shared/DocumentList.razor`: table showing title, category, upload date, file size, and associated project with an empty-state message
- [ ] T022 [US1] Create `ContosoDashboard/Pages/Documents.razor` at `/documents` with `[Authorize]`, injecting `IDocumentService` and `AuthenticationStateProvider`, hosting `DocumentUploadForm` and `DocumentList` for My Documents (depends on T019, T020, T021)
- [ ] T023 [P] [US1] Add a Documents link to `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: User Story 1 is fully functional and testable on its own (MVP)

---

## Phase 4: User Story 2 - Browse, Filter, Sort, and Search Documents (Priority: P1)

**Goal**: Users sort, filter, and search documents they are permitted to see, and project members see project documents.

**Independent Test**: With documents across categories and projects, filter by category, sort by size, and search by a tag; verify results are correct and exclude documents the user cannot access.

### Tests for User Story 2

- [ ] T024 [P] [US2] Write `ContosoDashboard.Tests/DocumentServiceAuthorizationTests.cs`: visibility matrix for Employee, Team Lead, Project Manager, Administrator; Team Lead sees same-department documents but never other users' Personal Files; project member sees project documents; non-member does not; user removed from a project loses project documents but keeps own; search never returns unauthorized documents
- [ ] T025 [P] [US2] Write `ContosoDashboard.Tests/DocumentQueryTests.cs`: sort by title, upload date, category, file size (both directions); filter by category, project, date range; search by title, description, tag (case-insensitive), uploader name, project name; empty result is not an error

### Implementation for User Story 2

- [ ] T026 [US2] Extend `GetMyDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` with `DocumentQuery` filtering (category, project, date range) and sorting (title, upload date, category, file size, ascending or descending) (depends on T019)
- [ ] T027 [US2] Implement `SearchAsync` and `GetProjectDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` using `GetVisibleDocuments`; search matches title, description, tags, uploader name, and project name (depends on T026)
- [ ] T028 [US2] Add sortable column headers and filter controls (category, project, date range) to `ContosoDashboard/Shared/DocumentList.razor` (depends on T021)
- [ ] T029 [US2] Add a search box and wire filters, sorting, and search results into `ContosoDashboard/Pages/Documents.razor` (depends on T027, T028)
- [ ] T030 [US2] Add a Project Documents card using `DocumentList` and `GetProjectDocumentsAsync` to `ContosoDashboard/Pages/ProjectDetails.razor` (depends on T027, T028)

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Download and Preview Documents (Priority: P2)

**Goal**: Users download any accessible document and preview PDFs and images in the browser; unauthorized requests get nothing.

**Independent Test**: Upload a PDF and a JPEG, preview and download both, then confirm another user without access gets 404 for both URLs.

### Tests for User Story 3

- [ ] T031 [P] [US3] Write `ContosoDashboard.Tests/DocumentDownloadTests.cs`: authorized user opens download and preview; unauthorized or missing id returns null (same result); preview of a non-PDF or non-image type is refused; `Download` and `Preview` activities written as distinct actions; deleted document returns null

### Implementation for User Story 3

- [ ] T032 [US3] Implement `OpenForDownloadAsync` and `OpenForPreviewAsync` in `ContosoDashboard/Services/DocumentService.cs`: access check, stream from storage, content type from the server map, sanitized download name, activity logging (depends on T012)
- [ ] T033 [US3] Create `ContosoDashboard/Endpoints/DocumentEndpoints.cs` with `GET /documents/{id}/download` (attachment) and `GET /documents/{id}/preview` (inline, only PDF, JPEG, PNG, otherwise 415, sets `X-Frame-Options: SAMEORIGIN` on this response only), both `RequireAuthorization()`, returning 404 when the service returns null (depends on T032)
- [ ] T034 [US3] Map the document endpoints in `ContosoDashboard/Program.cs` after `UseAuthorization()` (depends on T033)
- [ ] T035 [US3] Add Download and Preview actions to `ContosoDashboard/Shared/DocumentList.razor` (preview only for PDF and images)
- [ ] T036 [US3] Add a preview panel to `ContosoDashboard/Pages/Documents.razor` using an `<iframe>` for PDF and an `<img>` for JPEG and PNG pointing at the preview endpoint (depends on T034, T035)

**Checkpoint**: Stories 1 to 3 work independently

---

## Phase 6: User Story 4 - Manage Document Metadata, Versions, and Deletion (Priority: P2)

**Goal**: Uploaders edit metadata, replace the file, and permanently delete after confirmation; Project Managers delete in their projects.

**Independent Test**: Edit a title and category, replace the file, then delete after confirming; the row and the file are gone and non-owners are denied.

### Tests for User Story 4

- [ ] T037 [P] [US4] Write `ContosoDashboard.Tests/DocumentManagementTests.cs`: owner edits metadata and tags; replacement file validated and scanned, new path stored, old file deleted, size and type updated; failed replacement keeps the old file and row; owner, Administrator, project's Project Manager, and department Team Lead (non-Personal Files) can manage; unrelated user and share recipient cannot; delete removes row and file; cancelled delete is a UI concern (no service call)

### Implementation for User Story 4

- [ ] T038 [US4] Implement `UpdateMetadataAsync` in `ContosoDashboard/Services/DocumentService.cs` (title, description, category, tags with the same validation as upload) (depends on T018)
- [ ] T039 [US4] Implement `ReplaceFileAsync` in `ContosoDashboard/Services/DocumentService.cs`: validate and scan, write new GUID path, update row, then delete the old file; compensate on failure (depends on T038)
- [ ] T040 [US4] Implement `DeleteAsync` in `ContosoDashboard/Services/DocumentService.cs`: manage check, remove file and row, log `Delete` activity with title and file-type snapshots (depends on T039)
- [ ] T041 [P] [US4] Create `ContosoDashboard/Shared/DocumentEditDialog.razor` for editing metadata and replacing the file (same `MemoryStream` pattern as upload)
- [ ] T042 [US4] Add Edit and Delete actions (visible only when permitted) with a delete confirmation to `ContosoDashboard/Shared/DocumentList.razor` (depends on T035, T041)
- [ ] T043 [US4] Wire edit, replace, and delete handlers and list refresh into `ContosoDashboard/Pages/Documents.razor` (depends on T040, T042)

**Checkpoint**: Stories 1 to 4 work independently

---

## Phase 7: User Story 5 - Share Documents with Colleagues (Priority: P2)

**Goal**: Owners share read-only with users or departments; recipients get notified and see documents in Shared with Me.

**Independent Test**: Share with Floris; he receives a notification, sees the document in Shared with Me, and cannot edit, delete, or re-share; a third user cannot see it.

### Tests for User Story 5

- [ ] T044 [P] [US5] Write `ContosoDashboard.Tests/DocumentSharingTests.cs`: share with user and with department; `DocumentShared` notification created for recipients; recipient can view, preview, download; recipient cannot update, replace, delete, attach, or re-share; revoke removes access; non-recipient cannot see; cannot share with self; only the owner can share; `Share` activity written

### Implementation for User Story 5

- [ ] T045 [US5] Implement `ShareWithUserAsync`, `ShareWithDepartmentAsync`, `RevokeShareAsync`, and `GetSharesAsync` in `ContosoDashboard/Services/DocumentService.cs`: owner-only, exactly one target per share, notification through `INotificationService` (type `DocumentShared`), `Share` activity (depends on T040)
- [ ] T046 [US5] Implement `GetSharedWithMeAsync` in `ContosoDashboard/Services/DocumentService.cs` (direct and department shares) (depends on T045)
- [ ] T047 [P] [US5] Create `ContosoDashboard/Shared/DocumentShareDialog.razor` to pick users or a department, list current shares, and revoke
- [ ] T048 [US5] Add a Shared with Me tab and the Share action (owner only) to `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Shared/DocumentList.razor` (depends on T046, T047)

**Checkpoint**: Stories 1 to 5 work independently

---

## Phase 8: User Story 6 - Project and Task Document Integration (Priority: P3)

**Goal**: Documents appear on a task detail page, attach to tasks (inheriting the project), and project members are notified of new project documents.

**Independent Test**: Open a task, upload a document there, and confirm it appears in the project's documents and that other project members receive a notification.

### Tests for User Story 6

- [ ] T049 [P] [US6] Write `ContosoDashboard.Tests/TaskDocumentTests.cs`: upload with `TaskId` sets the task's project; attaching an existing owned document sets task and project; attaching someone else's document denied; user without access to the task denied; `DocumentAdded` notification sent to other project members (not the uploader); no notification for personal documents

### Implementation for User Story 6

- [ ] T050 [US6] Implement `AttachToTaskAsync` and `GetTaskDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs`, authorizing the task through the existing task access rules (depends on T040)
- [ ] T051 [US6] Extend `UploadAsync` in `ContosoDashboard/Services/DocumentService.cs` to accept `TaskId` (project inherited from the task) and to send `DocumentAdded` notifications to the other project members (depends on T050)
- [ ] T052 [US6] Create `ContosoDashboard/Pages/TaskDetails.razor` at `/tasks/{taskId:int}` with `[Authorize]`, using `ITaskService.GetTaskByIdAsync` and showing task information, a documents panel (`DocumentList`), upload via `DocumentUploadForm`, and attach-existing (depends on T051)
- [ ] T053 [US6] Link task rows to `/tasks/{TaskId}` in `ContosoDashboard/Pages/Tasks.razor` (replace the details TODO) (depends on T052)

**Checkpoint**: Stories 1 to 6 work independently

---

## Phase 9: User Story 7 - Dashboard Visibility (Priority: P3)

**Goal**: The dashboard shows the five most recent uploads and a document count card.

**Independent Test**: Upload six documents; the widget lists the five newest and the card shows six; a user with none sees the empty state and zero.

### Tests for User Story 7

- [ ] T054 [P] [US7] Write `ContosoDashboard.Tests/DashboardDocumentTests.cs`: recent returns at most 5 newest first; count equals the user's uploaded documents; zero state

### Implementation for User Story 7

- [ ] T055 [US7] Implement `GetRecentDocumentsAsync` and `GetDocumentCountAsync` in `ContosoDashboard/Services/DocumentService.cs` (depends on T040)
- [ ] T056 [US7] Add `TotalDocuments` to `DashboardSummary` and populate it in `ContosoDashboard/Services/DashboardService.cs` (depends on T055)
- [ ] T057 [US7] Add a Documents summary card and a Recent Documents widget (with empty-state message and link to `/documents`) to `ContosoDashboard/Pages/Index.razor`, injecting `IDocumentService` and adjusting the summary card column widths for five cards (depends on T056)
- [ ] T058 [P] [US7] Add widget styles to `ContosoDashboard/wwwroot/css/site.css`

**Checkpoint**: Stories 1 to 7 work independently

---

## Phase 10: User Story 8 - Activity Audit and Reporting (Priority: P3)

**Goal**: Every action is recorded; Administrators view reports; others are denied.

**Independent Test**: Upload, preview, download, share, and delete; the Administrator report shows all actions including the deleted document; a non-admin gets denied.

### Tests for User Story 8

- [ ] T059 [P] [US8] Write `ContosoDashboard.Tests/DocumentActivityTests.cs`: exactly one activity per upload, download, preview, share, delete; activity survives document deletion with title and file-type snapshot; `GetReportAsync` returns null for non-Administrators; report totals for types, uploaders, previews, downloads, and combined access

### Implementation for User Story 8

- [ ] T060 [US8] Implement `GetReportAsync` in `ContosoDashboard/Services/DocumentService.cs` (Administrator only; uploads by type and by user, previews, downloads, combined, optional date range) (depends on T040)
- [ ] T061 [US8] Create `ContosoDashboard/Pages/DocumentReports.razor` at `/documents/reports` with `[Authorize(Policy = "Administrator")]` showing the report (depends on T060)
- [ ] T062 [US8] Add an Administrator-only Reports link (wrapped in `AuthorizeView`) to `ContosoDashboard/Shared/NavMenu.razor` (depends on T061)
- [ ] T063 [US8] Audit `ContosoDashboard/Services/DocumentService.cs` so every upload, download, preview, share, and delete path calls `LogActivityAsync` exactly once and no activity is ever updated or removed (depends on T060)

**Checkpoint**: All user stories are independently functional

---

## Phase 11: Polish & Cross-Cutting Concerns

- [ ] T064 [P] Update `README.md`: feature description, `/documents` routes, storage location and upload limits, simulated scanner limitation, SQLite database reset note (delete `ContosoDashboard/ContosoDashboard.db` once because of `EnsureCreated()`), production alternatives (Azure Blob Storage, a real antivirus engine, migrations, full-text search)
- [ ] T065 [P] Write `ContosoDashboard.Tests/DocumentPerformanceTests.cs` seeding 500 documents and asserting list and search complete in under 2 seconds
- [ ] T066 Security review of `ContosoDashboard/Services/DocumentService.cs`, `ContosoDashboard/Services/FileStorageService.cs`, and `ContosoDashboard/Endpoints/DocumentEndpoints.cs`: path traversal, IDOR on every method, header sanitization, no physical paths or user file names in storage paths, unauthorized and missing both return 404
- [ ] T067 Run `dotnet build` and `dotnet test` at the repository root; fix any warnings or failures (no NU1603 or other restore warnings)
- [ ] T068 Run every scenario in `specs/001-document-upload-management/quickstart.md`, including the offline and storage safety checks

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup; blocks all user stories
- **User Stories (Phases 3 to 10)**: Depend on Foundational
  - US1 is the MVP; US2 to US8 build on the shared `DocumentService.cs` and `DocumentList.razor`, so implement in priority order for a single developer
  - Parallel work across stories is possible for tests, new component files, and pages, but service-method tasks must be serialized because they edit the same file
- **Polish (Phase 11)**: Depends on all desired stories

### User Story Dependencies

- **US1 (P1)**: After Foundational; no other story dependencies
- **US2 (P1)**: After US1 (extends `GetMyDocumentsAsync`, `DocumentList`, `Documents.razor`)
- **US3 (P2)**: After Foundational; UI tasks need US1's `DocumentList` and `Documents.razor`
- **US4 (P2)**: After US1; delete is reused by US8 retention tests
- **US5 (P2)**: After US4 in the service file ordering; independent in behavior
- **US6 (P3)**: After US4 (service ordering); needs US1 upload form
- **US7 (P3)**: After US1
- **US8 (P3)**: After US4 (delete retention test) and US3 (download and preview activity)

### Within Each Story

- Tests first, confirm they fail
- Service methods before UI; new components before pages that use them
- Commit after each task or logical group

### Parallel Opportunities

- Setup: T002 alongside T001 and T003
- Foundational: T005, T006, T009, T010, T011, T014 together; T015 after T003 and T008
- US1: T016 and T017 together; T020, T021, T023 together
- US2: T024 and T025 together
- Test tasks of different stories (T031, T037, T044, T049, T054, T059) can be authored in parallel once Foundational is done

---

## Parallel Example: User Story 1

```text
# Tests together:
T016 Write ContosoDashboard.Tests/DocumentValidatorTests.cs
T017 Write ContosoDashboard.Tests/DocumentUploadTests.cs

# UI files together (after T018 and T019 contracts exist):
T020 Create ContosoDashboard/Shared/DocumentUploadForm.razor
T021 Create ContosoDashboard/Shared/DocumentList.razor
T023 Add Documents link in ContosoDashboard/Shared/NavMenu.razor
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup, then Phase 2 Foundational
2. Phase 3 User Story 1
3. Stop and validate with quickstart scenarios 1 to 4
4. Demo

### Incremental Delivery

1. Setup + Foundational
2. US1 (upload) and US2 (browse and search): the core storage-and-find loop
3. US3 (download and preview), US4 (manage), US5 (share): complete day-to-day use
4. US6 (task integration), US7 (dashboard), US8 (audit and reports)
5. Polish and full quickstart validation

---

## Notes

- [P] tasks touch different files with no dependency on incomplete tasks
- The [Story] label maps each task to spec.md user stories for traceability
- `ContosoDashboard.db` must be deleted once after the new entities are added, because the app uses `EnsureCreated()`
- Never commit a literal EICAR string; tests build it at runtime
- Verify tests fail before implementing
