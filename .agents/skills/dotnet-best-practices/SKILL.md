---
name: dotnet-best-practices
description: Implement or improve C# code in the GustosApp .NET 8 solution while preserving its layered architecture, behavior, and xUnit test suite.
---

# GustosApp .NET best practices

Inspect the surrounding code and project files before editing. Prefer a focused change that follows the local style over a broad rewrite.

## Required quality bar

- Follow `AGENTS.md`, especially its test, verification, secret-management, and Git rules.
- Add or update xUnit tests for every Domain or Application logic change. Use Moq and FluentAssertions where they improve clarity, and keep Arrange, Act, Assert structure.
- Add a regression test for every bug fix. Never comment out, relax, ignore, or delete a test merely to obtain a passing build.
- Keep nullable reference types meaningful. Fix warnings with correct initialization, `required`, nullable annotations, or explicit guards rather than suppression by default.
- Use async I/O end to end and propagate `CancellationToken`. Avoid sync-over-async.
- Use constructor injection and appropriate service lifetimes. Add an interface only where it creates a useful boundary or test seam.

## Architecture and maintainability

- Keep Domain provider-independent, Application focused on use cases, Infrastructure responsible for EF Core and external systems, and API responsible for HTTP/SignalR concerns.
- Patterns such as Repository, Strategy, Factory, or Result are optional tools, not requirements. Apply them only when they reduce coupling or clarify behavior.
- Keep controllers thin and business decisions in Application or Domain.
- Prefer focused classes and methods, meaningful domain names, and removal of duplication supported by tests.
- Preserve public API compatibility unless the requested change explicitly allows a breaking change.

## Configuration, errors, and security

- Keep .NET and Microsoft.Extensions/EF Core package major versions compatible with the `net8.0` target.
- Bind external settings through typed options when practical and validate required values at startup.
- Use structured `ILogger` messages without credentials or personal data.
- Represent expected failures with specific exceptions or application results and map them consistently to HTTP `ProblemDetails`.
- Parameterize data access, validate input at trust boundaries, and keep all secrets outside tracked files.

## Verification

Run the Release build and complete test suite specified in `AGENTS.md`. Inspect the test summaries for projects that report zero discovered tests, not only the process exit code.
