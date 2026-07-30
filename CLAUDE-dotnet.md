# .NET Backend Development

## Critical Rules

- **Never use Console.Write** — always use log4net (see logging pattern below)
- **Never commit appsettings** — `appsettings*.json` files are gitignored for all .NET projects
- **Both backends share JWT keys** — RSA PEM files in `secrets/`

## Controller Conventions

Every controller follows this structure:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = $"{NegotiateDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}")]
public class TrafficEventsController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(TrafficEventsController));
```

### Required Attributes on Actions

```csharp
[HttpGet("{id:guid}")]
[EndpointName("TrafficEvents_GetSingleEventById")]
[ProducesResponseType(typeof(ApiResponse<TrafficEventDetailDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
```

- `[EndpointName("Controller_Action")]` — **required** for TypeScript client generation
- `[ProducesResponseType]` — on every action for OpenAPI docs

### Response Wrappers

All responses use standard wrappers from `WebApiPoco/Common/`:

```csharp
// Success
return Ok(new ApiResponse<TrafficEventDetailDto> { Success = true, Data = result });

// Not found
return NotFound(new ErrorResponse { ErrorCode = "EVENT_NOT_FOUND", Message = "..." });

// Validation error
return BadRequest(new ErrorResponse { ErrorCode = "VALIDATION_ERROR", Message = ex.Message });
```

### DatexPushReceiver Authentication

The **DatexPushReceiver** service uses **HTTP Basic Authentication** for push endpoints:
- `DatexPushController` — secured with `[Authorize(AuthenticationSchemes = "Basic")]`. Credentials configured via `DatexPushReceiver:BasicAuth:Username` / `Password`. The `/api/datexpush/status` health-check endpoint is `[AllowAnonymous]`.
- `ProAssistPushController` — remains **unauthenticated by design**. ProAssist systems push assistance data without credentials, protected by rate limiting only.

### Error Handling

`GlobalExceptionHandlerMiddleware` catches all unhandled exceptions automatically. Controllers only need explicit try/catch for **business validation** (`ArgumentException`). Do NOT add redundant try/catch around service calls.

---

## Patterns

### Service Pattern

```csharp
public interface ITrafficEventService
{
    Task<DataViewOutput<TrafficEventListItemDto>> GetTrafficEventsAsync(filter);
    Task<TrafficEventDetailDto?> GetTrafficEventByIdAsync(Guid id);
}
```

### Filter DTOs

Inherit from `DataViewFilterBase<T>` — provides `pageNumber`, `pageSize`, `sortBy`, `sortDirection`, `searchTerm`.

### Mapper Pattern

Static classes converting entities to DTOs:
```csharp
TrafficEventMapper.ToListItemDto(entity)
TrafficEventMapper.ToDetailDto(entity)
```

---

## C# / .NET Rules

- **log4net** for all logging — static field, NOT dependency injection:
  ```csharp
  private static readonly ILog logger = LogManager.GetLogger(typeof(ClassName));
  ```
- **Logger field MUST be lowercase `logger`** — never `Logger`. This is a private field, not a public property. All references (`logger.Info(...)`, `logger.Error(...)`, etc.) must use lowercase.
- **DTOs** as classes in `WebApiPoco/`, separate from database entities
- **EF Core**: Use `AsNoTracking()` for read-only queries
- Use explicit entity configurations (`IEntityTypeConfiguration<T>`)
- Avoid lazy loading; project only required fields
- Prefer async/await for all I/O operations

---

## Database

- **Entities**: `Database/Entities/`
- **Enums**: `Database/Entities/Enums/` (auto-exposed in OpenAPI)
- **Configurations**: `Database/Entities/Configurations/`

Key entities: `TrafficEvent` (main DATEX II situation), `TrafficEventValidity`, `TrafficEventGeometry`, `ApplicationUser`.

Assembly metadata centralized in `apps/netcorebackends/Directory.Build.props`.

