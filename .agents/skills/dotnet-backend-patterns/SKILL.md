---
name: dotnet-backend-patterns
description: Implement or review the GustosApp ASP.NET Core backend, including use cases, EF Core repositories, API endpoints, SignalR hubs, and external integrations.
---

# GustosApp backend patterns

Use the existing modular monolith and improve it incrementally. Do not introduce a new architectural style unless the requested change needs it.

## Project boundaries

- `GustosApp.Domain` owns entities, enums, domain behavior, and provider-independent contracts. Keep it free of ASP.NET Core, EF Core, configuration, and vendor SDK concerns.
- `GustosApp.Application` owns use cases, application services, validation, and integration contracts. It depends on Domain.
- `GustosApp.Infraestructure` owns `GustosDbContext`, EF Core repositories, caching, storage, payment, email, OCR, Google/Firebase, Gemini, and ONNX implementations. Keep the existing project spelling.
- `GustosApp.API` is the composition and transport layer: controllers, DTOs, AutoMapper, FluentValidation, middleware, authentication/authorization, and SignalR hubs.

Place interfaces at the innermost layer that consumes the abstraction and implementations in Infrastructure. Register implementations through the existing `IServiceCollection` extension methods.

## Data and integrations

- Target .NET 8 and SQL Server with Entity Framework Core. Keep related Microsoft package major versions compatible with the target framework.
- Compose an `IQueryable` before materialization. Filter, project, order, and paginate in SQL; avoid loading a complete table for in-memory filtering.
- Use `AsNoTracking()` for read-only queries and explicit includes or projections to avoid N+1 queries and over-fetching.
- Treat Firebase, Google services, Redis, Mercado Pago, email, OCR, Gemini, and ONNX as external providers behind Application contracts.
- Bind provider settings from configuration and validate required options at startup. Never commit credentials.
- For payment webhooks and other retryable external events, consider signature validation and idempotency as part of correctness.

## API and asynchronous work

- Use async/await through the full I/O call chain, accept and propagate `CancellationToken`, and avoid `.Result`, `.Wait()`, `async void`, and unnecessary `Task.Run`.
- Validate request DTOs at the API boundary with the project's FluentValidation setup.
- Map entities to DTOs rather than exposing tracked EF entities.
- Translate expected failures consistently through typed exceptions, application results, or the error middleware and `ProblemDetails`; do not branch on exception message text.
- Apply authorization policies at endpoints and test both allowed and forbidden paths.
- Keep detailed SignalR errors development-only.

## Testing

Follow the repository `AGENTS.md`. Tests use xUnit, Moq, and FluentAssertions. Unit-test Domain and Application behavior, and use API or integration tests for routing, serialization, authentication, authorization, EF mappings, and middleware. Every bug fix needs a regression test, and production logic must not be changed without the corresponding test update.
