---
name: dotnet-design-pattern-review
description: Review GustosApp C# code for appropriate architecture and design patterns without requiring patterns the solution does not use or changing code unless asked.
---

# GustosApp design review

Review the requested scope against the architecture documented in `AGENTS.md`. When the user requests review only, do not edit files.

## Patterns actually used

- Use Case/Application Service for business workflows.
- Repository for persistence contracts and EF Core implementations.
- Dependency Injection through ASP.NET Core and `IServiceCollection` extensions.
- Adapter or provider abstractions for Firebase, Google APIs, Redis, Mercado Pago, email, OCR, Gemini, and ONNX.
- Middleware for cross-cutting HTTP error handling.
- SignalR hubs for real-time communication.

These patterns are descriptive, not mandatory everywhere. Do not require any named design pattern or syntax style without a concrete benefit.

## Review criteria

- Dependency direction: Domain remains provider-independent; Application does not depend on API or concrete Infrastructure implementations.
- Responsibilities: controllers and hubs coordinate transport, use cases own workflows, repositories own persistence, and Domain owns business invariants.
- Testability: dependencies have useful seams and Domain/Application logic is covered with xUnit, Moq, and FluentAssertions. Flag bug fixes without regression coverage and tests that are commented out or undiscovered.
- Async correctness: I/O is asynchronous, cancellation flows through calls, and no sync-over-async is introduced.
- Data access: queries filter and project before materialization, avoid N+1 behavior, and use tracking intentionally.
- Error handling: expected failures have stable types or codes and HTTP responses are mapped consistently rather than from message text.
- Security: authorization is explicit, inputs and webhooks are validated, secrets stay out of Git, and detailed errors are environment-aware.
- Maintainability: abstractions solve observed coupling, large classes have cohesive responsibilities, and duplication or complexity is supported by concrete evidence.

Report findings in priority order with file and line evidence, user impact, and a focused remediation. Distinguish correctness or security problems from optional design improvements. Avoid recommending microservices or additional layers solely for architectural purity.
