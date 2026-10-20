# Quickstart: Validate Document Upload and Management

Run these after implementation to prove the feature end to end. Contract details are in [contracts/](contracts/) and entities in [data-model.md](data-model.md).

## Prerequisites

- .NET 8 SDK
- Existing database must be recreated once because the app uses `EnsureCreated()`: delete `ContosoDashboard/ContosoDashboard.db` if it exists
- Sample files: a small PDF, a JPEG, a `.exe` renamed to `.pdf`, a file larger than 25 MB, and the EICAR test text saved as `eicar.txt` (create it locally; do not commit it)

## 1. Automated tests

```bash
cd /home/s069607207/ambienteWorkshop/MangeliContosoDashboard-SSD
dotnet restore && dotnet build && dotnet test
```

Expected: all tests pass; no restore warnings. Tests cover the role visibility matrix, Personal Files privacy, read-only shares, validation, scan rejection, upload failure cleanup, activity retention after delete, and report authorization.

## 2. Run the app

```bash
cd ContosoDashboard && dotnet run
```

Open the URL shown in the console. Sign in via `/login` using the mock users: `ni.kang@contoso.com` (Employee), `floris.kregel@contoso.com` (Team Lead), `camille.nicole@contoso.com` (Project Manager), `admin@contoso.com` (Administrator).

## 3. Scenarios

| # | Steps | Expected |
|---|-------|----------|
| 1 | As Ni Kang, open Documents, upload the PDF with a title and category `Reports` | Progress shown; success message; document listed with date, size, type |
| 2 | Upload 3 files at once with shared category and per-file titles | Three independent results |
| 3 | Upload the 26 MB file, the disguised `.exe`, a `.zip`, and `eicar.txt` | Each rejected with a specific reason; nothing stored |
| 4 | Omit title or category | Submission blocked with field messages |
| 5 | Upload with project "ContosoDashboard Development" (Ni Kang is a member) | Succeeds; Camille receives a notification |
| 6 | Sort by size, filter by category and date range, search a tag | Results match; only permitted documents |
| 7 | Preview the PDF and the JPEG; download both | Inline render within 3 s; download keeps original name |
| 8 | Edit title and tags; replace the file; delete after confirming | Changes persist; delete is permanent; file removed from `AppData/uploads` |
| 9 | Share a document with Floris; sign in as Floris | Notification and entry under Shared with Me; edit, delete, share actions absent |
| 10 | As Floris, look for Ni Kang's `Personal Files` document | Not visible in lists or search |
| 11 | As another user, open `/documents/{id}/download` for a document you cannot access | 404 |
| 12 | Open a task from My Tasks, upload a document there | Appears in the project's documents; project members notified |
| 13 | Open the dashboard | Recent Documents lists the last 5 uploads; document count card is correct |
| 14 | As Administrator, open `/documents/reports`; as Ni Kang try the same URL | Report with types, uploaders, previews and downloads; non-admin denied |
| 15 | Delete a document, then open the report | Delete and earlier actions still appear with title and type |

## 4. Offline check

Disconnect from the network (or block outbound traffic) and repeat scenarios 1, 7, and 9. Expected: identical behavior (the Bootstrap CDN assets may not load, which does not affect document functions).

## 5. Storage safety check

- Confirm uploads appear only under `ContosoDashboard/AppData/uploads/{userId}/{projectId|personal}/{guid}.ext`
- Confirm `GET /AppData/uploads/...` returns 404 (not served as static files)
