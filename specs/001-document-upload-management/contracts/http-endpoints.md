# Contract: HTTP Endpoints

Both endpoints are minimal APIs in `ContosoDashboard/Endpoints/DocumentEndpoints.cs`, require an authenticated cookie session (`RequireAuthorization()`), and delegate every access decision to `IDocumentService`. Everything else (upload, list, edit, share, delete, reports) runs through Blazor Server components calling `IDocumentService`, not HTTP.

## GET /documents/{id}/download

| Aspect | Contract |
|--------|----------|
| Auth | Required. Unauthenticated: redirect to `/login` (cookie scheme) |
| Path | `id`: integer `DocumentId` |
| Success | `200` with file bytes. `Content-Type` from the server extension map, `Content-Disposition: attachment; filename="<sanitized original name>"`, `X-Content-Type-Options: nosniff` |
| Not found or not permitted | `404` with no body detail (does not reveal existence) |
| Side effect | Records a `Download` activity |

## GET /documents/{id}/preview

| Aspect | Contract |
|--------|----------|
| Auth | Required |
| Path | `id`: integer `DocumentId` |
| Allowed types | PDF, JPEG, PNG only; other types return `415` |
| Success | `200` with `Content-Disposition: inline`, `X-Frame-Options: SAMEORIGIN` (this response only) |
| Not found or not permitted | `404` |
| Side effect | Records a `Preview` activity |

## Cross-cutting behavior

- File names in headers are sanitized (strip path separators, control characters, quotes).
- Stored file paths and physical locations are never returned to the client.
- Responses never include directory listings or other users' paths.
- Global security headers and CSP from `Program.cs` stay unchanged for all other routes.
