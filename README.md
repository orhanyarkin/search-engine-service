<div align="center">

**English** | [Türkçe](README.tr.md)

</div>

# Search Engine Service

A search engine service that ingests data from multiple content providers (JSON & XML), normalizes it, ranks results with a weighted scoring algorithm, and serves them over a REST API.

## Tech Stack

| Layer | Technology |
|--------|-----------|
| Runtime | .NET 10 (LTS) |
| Architecture | Clean Architecture (4 layers) |
| Database | PostgreSQL + EF Core (Code-First) |
| Cache | Redis |
| Full-Text Search | Elasticsearch |
| Message Broker | RabbitMQ + MassTransit |
| Auth | JWT Bearer Token |
| Testing | xUnit + Moq + FluentAssertions |
| API Docs | OpenAPI + Scalar UI |
| Container | Docker + docker-compose |

## Technology Decisions

| Technology | Why? |
|-----------|--------|
| **.NET 10** | Chosen because it's my primary day-to-day stack, though the design would work in other languages/frameworks. LTS release, high HTTP throughput with Kestrel, a strong type system that catches errors at compile time, and the EF Core + MediatR + FluentValidation ecosystem speeds up backend development |
| **PostgreSQL** | Open-source, production-grade RDBMS; the `text[]` array type is ideal for tags, `EF.Functions.ILike` provides case-insensitive search, and JSON/JSONB allows a flexible data model |
| **Redis** | Sub-millisecond cache reads; wrapping the repository with the Decorator pattern adds a cache layer without touching application code; key-prefix based invalidation after sync |
| **Elasticsearch** | BM25 relevance scoring, fuzzy matching (typos), prefix search and wildcard queries — far more powerful full-text search than PostgreSQL ILIKE |
| **RabbitMQ** | Moves post-sync cache invalidation and Elasticsearch reindexing off the main flow into an async event-driven architecture; consumer-based message consumption via MassTransit |
| **Clean Architecture** | Domain and Application layers are independent of infrastructure; adding a new provider is a single `IContentProvider` implementation, and tests run in isolation with infrastructure mocks |
| **MediatR (CQRS)** | Command/query separation lets read and write paths be optimized independently; pipeline behaviors centralize cross-cutting concerns (validation, logging) |
| **Docker** | A single command brings up 5 services (API + PostgreSQL + Redis + Elasticsearch + RabbitMQ); consistent development/production environments |

## Architecture

A 4-layer structure following Clean Architecture principles:

```
┌─────────────────────────────────┐
│         WebAPI Layer            │  ← Controllers, Middleware, Dashboard
├─────────────────────────────────┤
│       Application Layer         │  ← Use Cases, DTOs, Validators, Scoring
├─────────────────────────────────┤
│      Infrastructure Layer       │  ← EF Core, Redis, ES, RabbitMQ, HTTP Client
├─────────────────────────────────┤
│         Domain Layer            │  ← Entities, Enums, Interfaces, Value Objects
└─────────────────────────────────┘
```

Dependency rule: inner layers never depend on outer layers. Infrastructure and WebAPI depend on Domain and Application.

## Project Structure

```
src/
├── SearchEngine.Domain/            # Entities, Enums, Interfaces, Value Objects
├── SearchEngine.Application/       # Use Cases, DTOs, Validators, Scoring Service
├── SearchEngine.Infrastructure/    # EF Core, Provider Clients, Redis, ES, RabbitMQ
├── SearchEngine.WebAPI/            # Controllers, Middleware, Dashboard (wwwroot)
tests/
├── SearchEngine.UnitTests/         # Domain + Application layer tests
├── SearchEngine.IntegrationTests/  # API integration tests
```

## Design Patterns

| Pattern | Usage |
|---------|----------|
| **Strategy** | `IContentProvider` interface — each provider has its own implementation |
| **Factory** | `ContentProviderFactory` — creates the right instance based on provider type |
| **Adapter** | Each provider adapter converts the external format into the internal domain model |
| **Repository** | `IContentRepository` — data access abstraction |
| **Decorator** | The cache layer wraps the repository (`CachedContentRepository`) |
| **Mediator (CQRS)** | Command/query separation via MediatR |
| **Circuit Breaker** | Resilience on provider HTTP calls via `Microsoft.Extensions.Http.Resilience` |
| **Rate Limiter** | Outbound request throttling toward providers via `TokenBucketRateLimiter` |
| **Pipeline Behavior** | Validation and logging in the MediatR pipeline |

## Scoring Algorithm

```
FinalScore = (BaseScore × TypeCoefficient) + FreshnessScore + EngagementScore
```

### Base Score
- **Video:** `views / 1000 + (likes / 100)`
- **Article/Text:** `reading_time + (reactions / 50)`

### Type Coefficient
- **Video:** 1.5
- **Text/Article:** 1.0

### Freshness Score (publish date → now)
| Age | Points |
|------|------|
| Within 1 week | +5 |
| Within 1 month | +3 |
| Within 3 months | +1 |
| Older | +0 |

### Engagement Score
- **Video:** `(likes / views) × 10`
- **Text/Article:** `(reactions / reading_time) × 5`

## API Endpoints

### Search
```http
GET /api/v1/search?keyword={keyword}&type={video|text}&sortBy={popularity|relevance|recency}&page=1&pageSize=10
```

Query parameters:
- `keyword` — search term (matched against titles and tags)
- `type` — content type filter: `video` or `text`
- `sortBy` — sorting: `popularity` (default), `relevance` or `recency`
- `page` / `pageSize` — pagination

### Content Detail
```http
GET /api/v1/contents/{id}
```

### Provider Sync (Manual)
```http
GET /api/v1/providers/sync
```

### Authentication
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

Use the JWT token from the response in the `Authorization: Bearer {token}` header.

### Health Check
```http
GET /health
```

## Setup & Running

### Requirements
- [Docker](https://docs.docker.com/get-docker/) and Docker Compose (the only requirement — no .NET SDK needed)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) *(only for local development and running tests)*

### Running with Docker (Recommended)

```bash
# Clone the repo
git clone https://github.com/orhanyarkin/search-engine-service.git
cd search-engine-service

# Bring up all services
docker-compose up --build
```

Services:
- **API:** http://localhost:8080
- **Scalar API Docs:** http://localhost:8080/scalar/v1
- **Dashboard:** http://localhost:8080
- **RabbitMQ Management:** http://localhost:25672 (guest/guest)

> **Note:** All ports differ from the defaults (API: 8080, PG: 15432, Redis: 16379, ES: 19200), so they won't clash with services already running locally.

### Local Development

```bash
# Bring up infrastructure services (PostgreSQL, Redis, ES, RabbitMQ)
docker-compose up postgres redis elasticsearch rabbitmq -d

# Run the API
dotnet run --project src/SearchEngine.WebAPI
```

The API runs at `http://localhost:8080` by default.

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Unit tests only
dotnet test tests/SearchEngine.UnitTests

# Integration tests only
dotnet test tests/SearchEngine.IntegrationTests
```

### Test Coverage

| Category | Test Count | Scope |
|----------|-------------|--------|
| Unit Tests | 65 | Scoring, Adapters, Handlers, Validators, Factory, Cache |
| Integration Tests | 19 | API endpoints, Auth, Search, Sync, Health |
| **Total** | **84** | |

#### Unit Test Details
- `ScoringServiceTests` — video and article score calculations, freshness, engagement
- `ContentProviderAdapterTests` — JSON and XML provider adapters
- `SearchContentsHandlerTests` — search query handler logic
- `SyncProvidersHandlerTests` — provider sync handler
- `GetContentByIdHandlerTests` — content detail handler
- `ContentProviderFactoryTests` — provider factory pattern
- `ValidationTests` — FluentValidation rules

#### Integration Test Details
- `SearchEndpointTests` — keyword search, type filter, pagination, sorting
- `ContentEndpointTests` — content detail, 404 handling
- `SyncEndpointTests` — provider sync trigger
- `AuthEndpointTests` — login, token validation, unauthorized access
- `HealthCheckTests` — health endpoint

## Authentication (JWT)

The dashboard and API endpoints are protected with JWT Bearer tokens.

**Demo credentials:**
- Username: `admin`
- Password: `admin123`

Getting a token:
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

Using the token:
```bash
curl http://localhost:8080/api/v1/search?keyword=docker \
  -H "Authorization: Bearer {token}"
```

> **Note:** Demo credentials are defined in `appsettings.json`. In production they should be overridden via environment variables.

## Rate Limiting

### API Rate Limiting (Inbound requests)

| Policy | Limit | Window | Applied Endpoints |
|--------|-------|--------|--------------------|
| Global | 100 requests | 1 minute (fixed window) | All endpoints |
| Search | 30 requests | 1 minute (sliding window) | `/api/v1/search` |
| Auth | 5 requests | 1 minute (fixed window) | `/api/v1/auth/*` |

### Provider Rate Limiting (Outbound requests)

| Parameter | Value | Description |
|-----------|-------|----------|
| Token capacity | 10 | Maximum requests per bucket |
| Replenishment period | 1 second | Token refill frequency |
| Tokens per period | 5 | 5 new request slots every second |
| Queue limit | 5 | Cap on queued requests |

HTTP requests toward providers go through a `TokenBucketRateLimiter`. Both providers (JSON and XML) share the same rate limiter, keeping the total outbound load under control.

## Background Sync

Provider data sync is triggered in two ways:

1. **Manual:** the `GET /api/v1/providers/sync` endpoint
2. **Automatic:** `BackgroundSyncService` — runs on application startup and every 30 minutes

Sync flow:
1. Data is fetched from providers over HTTP (protected by Rate Limiter + Circuit Breaker + Retry)
2. Adapters convert it into the domain model
3. The scoring service computes scores
4. Data is persisted to PostgreSQL
5. The Redis cache is invalidated
6. A `ContentsSyncedEvent` is published via RabbitMQ
7. The Elasticsearch index is updated

## Dashboard

A minimal web UI available at `http://localhost:8080`:

- Login form (JWT auth)
- Keyword search (Elasticsearch full-text)
- Type filter (Video / Article / All)
- Sort (Popularity / Relevance / Newest)
- Pagination
- Content cards (score, type, metrics, tags)
- Manual sync button
- Toast notifications

## Clean Code Approach

- **SOLID principles** — single responsibility per class, loose coupling via dependency injection
- **DRY** — shared logic lives in services and extension methods
- **Async/await** — all I/O operations are asynchronous
- **DTO pattern** — domain entities are never exposed externally
- **Nullable reference types** — enabled across all projects
- **Structured logging** — Serilog with console and file sinks
- **FluentValidation** — request validation via pipeline behavior
- **Global exception handling** — centralized error handling via middleware

## Security

| Measure | Detail |
|-------|-------|
| JWT Bearer auth | HS256-signed tokens, 1-hour expiry |
| Rate limiting | Separate policies for global, search and auth endpoints |
| Security headers | X-Frame-Options, X-Content-Type-Options, Referrer-Policy, X-XSS-Protection |
| CORS restriction | Only localhost origins allowed |
| Input validation | All requests validated with FluentValidation |
| SQL injection protection | EF Core parameterized queries |
| Resilience patterns | Rate limiter, circuit breaker, retry and timeout on provider calls |
| Docker port binding | Infrastructure ports bound to `127.0.0.1` only |

### Recommendations for Production

| Area | Recommendation |
|------|-------|
| JWT Secret | Inject via environment variable, minimum 32 characters |
| Credentials | Use an identity provider (OAuth2/OIDC) or a secret manager |
| HTTPS | TLS termination behind a reverse proxy (nginx/traefik) |
| Logging | Serilog Seq/Elasticsearch sink + Kibana dashboards |
| Monitoring | Metric collection with Prometheus + Grafana |
| Rate Limiting | Redis-backed distributed rate limiter |
| CORS | Explicitly define production domains |
