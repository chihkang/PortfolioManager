# Architecture Overview

PortfolioManager is built following modern .NET practices with a focus on simplicity, performance, and maintainability.

## Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Framework** | .NET 10 (ASP.NET Core) | Web API hosting |
| **Database** | MongoDB | Document storage |
| **Caching** | IDistributedCache (Memory/Redis) | Performance optimization |
| **Scheduling** | Quartz.NET | Background jobs |
| **API Documentation** | Swagger/OpenAPI | Interactive API docs |
| **Dependency Injection** | Built-in ASP.NET Core DI | Service management |

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     Client Layer                         │
│              (HTTP Clients, Web Apps, etc.)             │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                  API Controllers                         │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│  │   User   │ │Portfolio │ │  Stock   │ │  Daily   │  │
│  │Controller│ │Controller│ │Controller│ │  Value   │  │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘  │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                  Service Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │MongoDbService│  │PortfolioDV   │  │ExchangeRate  │ │
│  │              │  │Service       │  │Service       │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│  ┌──────────────┐                                       │
│  │PortfolioCache│                                       │
│  │Service       │                                       │
│  └──────────────┘                                       │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│               Data & External Layers                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │   MongoDB    │  │Distributed   │  │External APIs │ │
│  │   Database   │  │Cache         │  │(Exchange Rate)│ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│              Background Jobs (Quartz)                    │
│  ┌──────────────────────────────────────────────────┐   │
│  │ RecordDailyValueJob - Daily portfolio snapshots  │   │
│  │ Schedule: Mon-Fri 13:35, Sat 05:35 (Asia/Taipei)│   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Core Design Principles

### 1. No Repository Pattern

**Decision**: Direct MongoDB collection access via `MongoDbService`.

**Rationale**:
- MongoDB.Driver already provides an abstraction layer
- Reduces unnecessary complexity and code
- Improves performance by eliminating extra indirection
- Maintains testability through dependency injection

**Implementation**:
```csharp
public class PortfolioController(MongoDbService mongoDbService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<Portfolio>> GetPortfolio(string id)
    {
        var portfolio = await mongoDbService.Portfolios
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();
        return portfolio != null ? Ok(portfolio) : NotFound();
    }
}
```

### 2. Primary Constructors (C# 12+)

**Convention**: All controllers and services use primary constructors for dependency injection.

**Benefits**:
- Reduced boilerplate code
- Clear declaration of dependencies
- Improved readability

**Example**:
```csharp
public class UserController(
    MongoDbService mongoDbService,
    ILogger<UserController> logger) : ControllerBase
{
    // Dependencies available as parameters directly
}
```

### 3. Lazy Collection Initialization

**Pattern**: MongoDB collections are initialized lazily in `MongoDbService`.

**Rationale**:
- Defers initialization until first access
- Improves startup performance
- Thread-safe by default

**Implementation**:
```csharp
private readonly Lazy<IMongoCollection<User>> _users;

public IMongoCollection<User> Users => _users.Value;
```

### 4. Performance Optimizations

#### ReadOnlySpan<char> for String Operations
Used in hot paths for efficient string parsing without allocations:

```csharp
ReadOnlySpan<char> idSpan = id.AsSpan();
if (idSpan.IsEmpty) return BadRequest("ID cannot be empty");
```

#### Switch Expressions
Concise pattern matching for cleaner logic:

```csharp
return result switch
{
    null => NotFound(),
    _ => Ok(result)
};
```

## Domain Model

### Entity Relationships

```
┌──────────┐         ┌──────────────┐
│   User   │ 1     1 │  Portfolio   │
│          │◄────────┤              │
│ Id       │         │ UserId       │
│ Username │         │ Stocks[]     │
│ Email    │         └──────┬───────┘
└──────────┘                │
                            │ N
                            │
                            ▼
                    ┌──────────────────┐
                    │ PortfolioStock   │
                    │ StockId          │◄────┐
                    │ Quantity         │     │
                    └──────────────────┘     │ N
                                             │
                                        ┌────┴────┐
                                        │  Stock  │
                                        │ Name    │
                                        │ Alias   │
                                        │ Price   │
                                        │ Currency│
                                        └─────────┘

┌──────────────┐        ┌──────────────────────────┐
│  Portfolio   │ 1    N │ PortfolioDailyValue      │
│              │◄───────┤                          │
│ Id           │        │ PortfolioId              │
│              │        │ Date                     │
│              │        │ TotalValueTwd            │
└──────────────┘        └──────────────────────────┘
```

### Key Entities

#### User
The account holder with a 1:1 relationship to Portfolio.

#### Portfolio
Container for investment holdings. Contains multiple `PortfolioStock` items.

#### Stock
Financial instrument (e.g., AAPL, TSLA) with price in USD or TWD.

#### PortfolioStock
Join entity linking Portfolio to Stock, storing the quantity held.

#### PortfolioDailyValue
Historical snapshot of portfolio total value in TWD for trending analysis.

## Data Flow

### Portfolio Valuation Calculation

```
1. Fetch Portfolio with Stocks
2. For each PortfolioStock:
   a. Get Stock price
   b. Get Exchange Rate (if USD)
   c. Calculate: Price × Quantity × ExchangeRate
3. Sum all values → Total Value (TWD)
4. Save PortfolioDailyValue record
```

### Daily Recording Job Flow

```
RecordDailyValueJob (Scheduled)
    ↓
1. Fetch USD-TWD Exchange Rate
    ↓
2. Get All Portfolios
    ↓
3. For Each Portfolio:
    ├─ Calculate Total Value (TWD)
    ├─ Create PortfolioDailyValue Record
    └─ Save to MongoDB
    ↓
4. Log Results & Statistics
```

## Middleware Pipeline

The ASP.NET Core middleware pipeline is configured in `Program.cs`:

```
Request
   ↓
ForwardedHeaders (X-Forwarded-* support for reverse proxies)
   ↓
HTTPS Redirection (Production only)
   ↓
Static Files
   ↓
Routing
   ↓
CORS (if configured)
   ↓
Authentication (if configured)
   ↓
Authorization
   ↓
Swagger UI
   ↓
Controllers (Endpoints)
   ↓
Response
```

## Caching Strategy

### Cache Layers

1. **Distributed Cache** (`IDistributedCache`):
   - Redis (Production) or Memory (Development)
   - Used for stock lists and frequently accessed data
   - TTL: Configurable per cache entry

2. **Application Cache**:
   - In-memory caching via `PortfolioCacheService`
   - Used for short-lived, frequently accessed data

### Cache Implementation

Extension methods in `DistributedCacheExtensions.cs` provide type-safe caching:

```csharp
// Cache retrieval with deserialization
var stocks = await cache.GetAsync<List<Stock>>("stock_list");

// Cache storage with serialization
await cache.SetAsync("stock_list", stocks, TimeSpan.FromMinutes(5));
```

## Background Jobs (Quartz.NET)

### RecordDailyValueJob

**Purpose**: Snapshot portfolio values for historical trending.

**Schedule**:
- **Weekdays**: Monday-Friday at 13:35 Asia/Taipei
- **Weekend**: Saturday at 05:35 Asia/Taipei

**Configuration**: Cron triggers in `Program.cs`:

```csharp
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.UseTimeZoneConverter();
    
    var jobKey = new JobKey("RecordDailyValueJob");
    q.AddJob<RecordDailyValueJob>(opts => opts.WithIdentity(jobKey));
    
    // Weekday trigger
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("WeekdayTrigger")
        .WithCronSchedule("0 35 13 ? * MON-FRI", 
            x => x.InTimeZone(taipei))
    );
    
    // Weekend trigger
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("WeekendTrigger")
        .WithCronSchedule("0 35 5 ? * SAT", 
            x => x.InTimeZone(taipei))
    );
});
```

## Configuration Management

### Environment-Based Settings

- **Development**: `appsettings.Development.json`
- **Production**: `appsettings.Production.json`
- **Template**: `appsettings.template.json` (for reference)

### Options Pattern

Configuration is bound using the Options pattern:

```csharp
builder.Services.AddOptions<MongoDbSettings>()
    .Bind(builder.Configuration.GetSection(MongoDbSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Environment Variables

Configuration follows ASP.NET Core conventions:
- **Double underscore** (`__`) for nested configuration
- Example: `MongoDbSettings__ConnectionString`

## Deployment Architecture

### Containerization (Docker)

**Multi-stage build** for optimized image size:

1. **Build stage**: Restore and compile
2. **Publish stage**: Create release artifacts
3. **Runtime stage**: Minimal ASP.NET Core runtime image

**Key configurations**:
- Timezone: `Asia/Taipei` (via `TZ` environment variable)
- Port: 8080 (configurable via `ASPNETCORE_URLS`)
- User: Non-root for security

### Reverse Proxy Support

ForwardedHeaders middleware handles `X-Forwarded-*` headers from reverse proxies (Nginx, Zeabur, etc.):

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

## Security Considerations

1. **Connection Strings**: Never commit to source control. Use environment variables or secrets management.
2. **MongoDB Authentication**: Always use authenticated connections in production.
3. **HTTPS**: Enforced in production via `UseHttpsRedirection()`.
4. **Input Validation**: All DTOs and inputs are validated.
5. **Non-root Container**: Docker image runs as non-root user.

## Performance Characteristics

### MongoDB Indexes

Indexes are created on startup in `MongoDbService.InitializeAsync()`:

- **Users**: `Username` (unique), `Email` (unique)
- **Portfolios**: `UserId` (unique)
- **Stocks**: `Name`, `Alias`
- **PortfolioDailyValue**: Compound index on `(PortfolioId, Date)`

### Caching Strategy

- **Stock lists**: Cached for 5 minutes
- **Exchange rates**: Cached for duration of job execution
- **Portfolio enrichment**: No caching (real-time data)

## Observability

### Structured Logging

All logs use structured logging with semantic properties:

```csharp
logger.LogInformation("""
    Portfolio created:
    Portfolio ID: {PortfolioId}
    User ID: {UserId}
    Stock Count: {StockCount}
    """,
    portfolio.Id,
    portfolio.UserId,
    portfolio.Stocks?.Count ?? 0);
```

### Metrics (Future Enhancement)

Consider adding:
- Application Insights
- Prometheus metrics
- Health check endpoints

## Testing Strategy

### Unit Tests

Located in `PortfolioManager.Tests`:
- Extension methods (cache, value conversions)
- DTO response builders
- Service logic

### Code Coverage

GitHub Actions workflow enforces code coverage reporting:
- Workflow: `.github/workflows/agentic-test-coverage.yml`
- Reports HEAD vs BASE coverage delta
- Configurable minimum coverage threshold

## Further Reading

- [Getting Started Guide](./getting-started.md)
- [Deployment Guide](./deployment.md)
- [API Reference](../api/README.md)
- [Contributing Guidelines](../../CONTRIBUTING.md)
