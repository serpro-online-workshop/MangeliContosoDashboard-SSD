<!--
Sync Impact Report (temporary; remove before commit)
- Version change: (unfilled template) → 1.0.0
- Modified principles: none renamed (all five principles defined from template placeholders)
  - [PRINCIPLE_1_NAME] → I. Training-Only Scope
  - [PRINCIPLE_2_NAME] → II. Offline-First with Infrastructure Abstraction
  - [PRINCIPLE_3_NAME] → III. Defense-in-Depth Security (NON-NEGOTIABLE)
  - [PRINCIPLE_4_NAME] → IV. Layered Architecture and Service-Level Authorization
  - [PRINCIPLE_5_NAME] → V. Simplicity and Spec-Driven Development
- Added sections: Technology and Platform Constraints; Development Workflow and Quality Gates
- Removed sections: none
- Follow-up TODOs: none
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-Only Scope

ContosoDashboard is a fictional application built solely to teach Spec-Driven Development (SDD)
with GitHub Spec Kit. It MUST NOT be presented, configured, or documented as production-ready.
Simplifications made for training (mock authentication, `EnsureCreated()`, local storage) MUST be
documented as known limitations, together with the production alternative.

Rationale: learners must be able to distinguish training shortcuts from recommended practice.

### II. Offline-First with Infrastructure Abstraction

The application MUST run fully offline with no cloud subscriptions or external service
dependencies. Every infrastructure dependency (database, file storage, authentication) MUST be
reached through an interface or configuration seam so a cloud implementation (Azure SQL, Azure
Blob Storage, Microsoft Entra ID) can replace the local one without changing business logic.
Implementations MUST be selected through dependency injection or configuration.

Rationale: training must work anywhere, while teaching a realistic cloud migration path.

### III. Defense-in-Depth Security (NON-NEGOTIABLE)

Authorization MUST be enforced at every layer: middleware, `[Authorize]` attributes or policies on
pages, and checks inside services. Services MUST verify that the current user may access the
requested resource (IDOR protection) and MUST NOT rely only on UI hiding. Role-based access
control MUST follow the hierarchy Employee < TeamLead < ProjectManager < Administrator. Secrets
and credentials MUST NOT be committed to source control. Code MUST be free of OWASP Top 10
vulnerabilities.

Rationale: security patterns are a core learning objective and the cost of a missed layer is high.

### IV. Layered Architecture and Service-Level Authorization

Code MUST be organized into Models, Data, Services, and Pages/Shared layers. Pages MUST NOT access
`ApplicationDbContext` directly; data access and business rules MUST live in services registered
through dependency injection behind interfaces. New entities MUST be registered in
`ApplicationDbContext` with explicit relationships and indexes.

Rationale: clear separation keeps features testable and keeps authorization checks in one place.

### V. Simplicity and Spec-Driven Development

Features MUST start from a specification and plan produced with Spec Kit before implementation.
Solutions MUST use the simplest design that satisfies the specification (YAGNI); added complexity
MUST be justified in the plan. Unique identifiers or file paths MUST be generated before database
insertion to avoid duplicate-key violations and orphaned records.

Rationale: the project exists to demonstrate SDD; unspecified or over-built code defeats it.

## Technology and Platform Constraints

- Framework: ASP.NET Core 8.0 with Blazor Server; UI styled with Bootstrap 5.3 and Bootstrap Icons.
- Database: SQLite through Entity Framework Core. The connection string MUST live in configuration.
- Authentication: cookie-based mock authentication with claims-based identity (training only).
- Security headers (CSP, X-Frame-Options, and similar) and cookie sliding expiration MUST be kept.
- Files MUST be stored locally behind a storage abstraction; no external services are allowed.
- NuGet package references MUST resolve without warnings on restore.

## Development Workflow and Quality Gates

- Work follows the Spec Kit flow: specify, plan, tasks, implement.
- Every change MUST build with `dotnet build` without errors before it is considered complete.
- Changes touching authentication, authorization, or data access MUST be reviewed against
  Principle III.
- The README and stakeholder documents MUST be updated when behavior, setup, or the technology
  stack changes.
- Behavior with security impact (access control, IDOR checks) SHOULD be covered by automated tests
  or documented manual test steps.

## Governance

This constitution supersedes other project practices. Amendments MUST be made through a documented
change that states the rationale and updates the version and Last Amended date. Versioning follows
semantic versioning: MAJOR for backward-incompatible principle removals or redefinitions, MINOR
for added or materially expanded principles or sections, PATCH for clarifications and wording.
Every specification, plan, and pull request MUST be reviewed for compliance with this constitution,
and any deviation MUST be justified in writing. Use `README.md` for runtime development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-20
