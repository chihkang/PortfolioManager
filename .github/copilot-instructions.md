---
applyTo: "**"
---

# Project Context & Coding Guidelines

You are an expert .NET developer working on **PortfolioManager**, a .NET 10 Web API application using MongoDB and Quartz.NET.

## 1. Architecture & Core Components

- **Framework**: .NET 10 (ASP.NET Core Web API).
- **Database**: MongoDB (using official `MongoDB.Driver`).
- **Background Jobs**: Quartz.NET for scheduled tasks (e.g., `RecordDailyValueJob`).
- **Caching**: `IDistributedCache` (Redis/Memory) via `PortfolioCacheService` and `DistributedCacheExtensions`.
- **External Services**: `IExchangeRateService` for fetching financial data (Typed Client).
- **Messaging**: MediatR is configured (though currently lightly used).

### Key Files
- `Program.cs`: DI setup, Middleware (ForwardedHeaders, Swagger), Quartz configuration, MongoDB settings binding.
- `Services/MongoDbService.cs`: Centralized MongoDB access. Exposes `IMongoCollection<T>` via `Lazy` properties.
- `Controllers/*.cs`: API endpoints. Use Primary Constructors.
- `GlobalUsings.cs`: Common namespaces (`MongoDB.Driver`, `PortfolioManager.Models`, etc.).

## 2. Coding Conventions & Patterns

- **Primary Constructors**: ALWAYS use C# 12+ primary constructors for dependency injection.
  ```csharp
  public class PortfolioController(MongoDbService mongoDbService, ILogger<PortfolioController> logger) : ControllerBase
  ```
- **Configuration**:
  - Use the Options pattern (`IOptions<T>`) for settings.
  - Bind in `Program.cs` using `AddOptions<T>().Bind(...)`.
- **Data Access**:
  - **NO Repository Pattern**: Inject `MongoDbService` and access collections directly.
  - Use `Lazy<IMongoCollection<T>>` in `MongoDbService` for initialization.
- **Logging**:
  - Use `ILogger` with **Structured Logging**.
  - Use Raw String Literals (`"""`) for multi-line log templates.
  ```csharp
  logger.LogInformation("""
      Job started:
      Trigger: {TriggerName}
      """, context.Trigger.Key.Name);
  ```
- **Performance**:
  - Use `ReadOnlySpan<char>` for string parsing in hot paths (e.g., ID validation).
  - Use `switch` expressions for concise logic.
- **Caching**:
  - Use `IDistributedCache` with `GetAsync<T>`/`SetAsync<T>` extensions in `DistributedCacheExtensions.cs`.

## 3. Infrastructure & Deployment (Zeabur/Docker)

- **Environment Variables**:
  - `ASPNETCORE_ENVIRONMENT`: `Production`
  - `MongoDbSettings__ConnectionString`: MongoDB connection string.
  - `MongoDbSettings__DatabaseName`: Database name.
- **Network**:
  - App listens on port `8080` in Production (`ASPNETCORE_URLS=http://0.0.0.0:8080`).
  - **Forwarded Headers**: Configured to trust `X-Forwarded-*` for Zeabur reverse proxy.
  - **Timezone**: `Asia/Taipei` is enforced in Dockerfile and Quartz triggers.

## 4. Development Workflow

- **Build**: `dotnet build`
- **Run**: `dotnet run` (Swagger at `/swagger`)
- **Docker Build**: `docker build -t portfoliomanager .`
- **Docker Run**:
  ```bash
  docker run --rm -p 8080:8080 \
    -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
    -e MongoDbSettings__ConnectionString="..." \
    portfoliomanager
  ```

## 5. Common Tasks

### Adding a New Feature
1.  Define Entity in `Models/Entities`.
2.  Add collection to `MongoDbService.cs` (using `Lazy`).
3.  Create `Controller` using Primary Constructor.
4.  Implement logic using `mongoDbService.{Collection}.Find/Insert/Update`.

### Debugging
- **502 Bad Gateway**: Check `ASPNETCORE_URLS` matches container port (8080).
- **HTTPS Redirect Loop**: Ensure `ForwardedHeaders` middleware is active (handled in `Program.cs`).
