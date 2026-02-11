# Search Engine Service

Birden fazla content provider'dan (JSON & XML) veri çeken, normalize eden, weighted scoring algoritması ile puanlayan ve REST API üzerinden sunan bir search engine servisi.

## Teknoloji Stack'i

| Katman | Teknoloji |
|--------|-----------|
| Runtime | .NET 10 (LTS) |
| Mimari | Clean Architecture (4 katman) |
| Veritabanı | PostgreSQL + EF Core (Code-First) |
| Cache | Redis (L2) + IMemoryCache (L1) |
| Full-Text Search | Elasticsearch |
| Message Broker | RabbitMQ + MassTransit |
| Auth | JWT Bearer Token |
| Test | xUnit + Moq + FluentAssertions |
| API Docs | OpenAPI + Scalar UI |
| Container | Docker + docker-compose |

## Mimari

Clean Architecture prensiplerine uygun 4 katmanlı yapı:

```
┌─────────────────────────────────┐
│         WebAPI Layer            │  ← Controller, Middleware, Dashboard
├─────────────────────────────────┤
│       Application Layer         │  ← Use Case, DTO, Validator, Scoring
├─────────────────────────────────┤
│      Infrastructure Layer       │  ← EF Core, Redis, ES, RabbitMQ, HTTP Client
├─────────────────────────────────┤
│         Domain Layer            │  ← Entity, Enum, Interface, Value Object
└─────────────────────────────────┘
```

Dependency kuralı: iç katmanlar dış katmanlara bağımlı değil. Infrastructure ve WebAPI, Domain ve Application'a depend eder.

## Proje Yapısı

```
src/
├── SearchEngine.Domain/            # Entity, Enum, Interface, Value Object
├── SearchEngine.Application/       # Use Case, DTO, Validator, Scoring Service
├── SearchEngine.Infrastructure/    # EF Core, Provider Client, Redis, ES, RabbitMQ
├── SearchEngine.WebAPI/            # Controller, Middleware, Dashboard (wwwroot)
tests/
├── SearchEngine.UnitTests/         # Domain + Application layer test'leri
├── SearchEngine.IntegrationTests/  # API integration test'leri
```

## Design Pattern'ler

| Pattern | Kullanım |
|---------|----------|
| **Strategy** | `IContentProvider` interface — her provider kendi implementasyonuna sahip |
| **Factory** | `ContentProviderFactory` — provider type'a göre doğru instance'ı oluşturur |
| **Adapter** | Her provider adapter'ı external format'ı internal domain model'e dönüştürür |
| **Repository** | `IContentRepository` — data access abstraction'ı |
| **Decorator** | Cache layer, repository'yi wrap eder (`CachedContentRepository`) |
| **Mediator (CQRS)** | MediatR ile command/query separation |
| **Circuit Breaker** | `Microsoft.Extensions.Http.Resilience` ile provider HTTP call'larında resilience |
| **Pipeline Behavior** | MediatR pipeline'ında validation ve logging |

## Scoring Algoritması

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
| Süre | Puan |
|------|------|
| 1 hafta içinde | +5 |
| 1 ay içinde | +3 |
| 3 ay içinde | +1 |
| Daha eski | +0 |

### Engagement Score
- **Video:** `(likes / views) × 10`
- **Text/Article:** `(reactions / reading_time) × 5`

## API Endpoint'leri

### Search
```http
GET /api/v1/search?keyword={keyword}&type={video|text}&sortBy={score|date}&page=1&pageSize=10
```

Query parameter'lar:
- `keyword` — arama terimi (title ve tag'lerde arar)
- `type` — içerik tipi filtresi: `video` veya `text`
- `sortBy` — sıralama: `score` (varsayılan) veya `date`
- `page` / `pageSize` — pagination

### Content Detail
```http
GET /api/v1/contents/{id}
```

### Provider Sync (Manuel)
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

Response'taki JWT token'ı `Authorization: Bearer {token}` header'ında kullanın.

### Health Check
```http
GET /health
```

## Kurulum ve Çalıştırma

### Gereksinimler
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/) ve Docker Compose

### Docker ile Çalıştırma (Önerilen)

```bash
# Repo'yu klonla
git clone https://github.com/<kullanici-adi>/search-engine-service.git
cd search-engine-service

# Tüm servisleri ayağa kaldır
docker-compose up --build
```

Servisler:
- **API:** http://localhost:8080
- **Scalar API Docs:** http://localhost:8080/scalar/v1
- **Dashboard:** http://localhost:8080/dashboard.html
- **RabbitMQ Management:** http://localhost:15672 (guest/guest)

> **Not:** Port çakışması yaşarsanız (örn. Redis 6379 zaten kullanılıyorsa), local'de çalışan servisleri durdurun:
> ```bash
> # Windows
> net stop Redis
> # Linux/Mac
> sudo systemctl stop redis
> ```

### Local Geliştirme

```bash
# Infrastructure servislerini ayağa kaldır (PostgreSQL, Redis, ES, RabbitMQ)
docker-compose up postgres redis elasticsearch rabbitmq -d

# API'yi çalıştır
dotnet run --project src/SearchEngine.WebAPI
```

API varsayılan olarak `http://localhost:5000` adresinde çalışır.

## Test

### Test'leri Çalıştırma

```bash
# Tüm test'leri çalıştır
dotnet test

# Sadece unit test'ler
dotnet test tests/SearchEngine.UnitTests

# Sadece integration test'ler
dotnet test tests/SearchEngine.IntegrationTests
```

### Test Coverage

| Kategori | Test Sayısı | Kapsam |
|----------|-------------|--------|
| Unit Test'ler | 62 | Scoring, Adapter, Handler, Validator, Factory |
| Integration Test'ler | 19 | API endpoint, Auth, Search, Sync, Health |
| **Toplam** | **81** | |

#### Unit Test Detayı
- `ScoringServiceTests` — Video ve article scoring hesaplamaları, freshness, engagement
- `ContentProviderAdapterTests` — JSON ve XML provider adapter'ları
- `SearchContentsHandlerTests` — Search query handler logic'i
- `SyncProvidersHandlerTests` — Provider sync handler'ı
- `GetContentByIdHandlerTests` — Content detail handler'ı
- `ContentProviderFactoryTests` — Provider factory pattern
- `ValidationTests` — FluentValidation kuralları

#### Integration Test Detayı
- `SearchEndpointTests` — Keyword search, type filter, pagination, sorting
- `ContentEndpointTests` — Content detail, 404 handling
- `SyncEndpointTests` — Provider sync trigger
- `AuthEndpointTests` — Login, token validation, unauthorized access
- `HealthCheckTests` — Health endpoint

## Authentication (JWT)

Dashboard ve API endpoint'leri JWT Bearer token ile korunur.

**Demo credentials:**
- Username: `admin`
- Password: `admin123`

Token alma:
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

Token kullanma:
```bash
curl http://localhost:8080/api/v1/search?keyword=docker \
  -H "Authorization: Bearer {token}"
```

> **Not:** Demo credential'lar `appsettings.json`'da tanımlıdır. Production ortamda environment variable ile override edilmelidir.

## Rate Limiting

| Policy | Limit | Window | Uygulanan Endpoint |
|--------|-------|--------|--------------------|
| Global | 100 request | 1 dakika (fixed window) | Tüm endpoint'ler |
| Search | 30 request | 1 dakika (sliding window) | `/api/v1/search` |
| Auth | 5 request | 1 dakika (fixed window) | `/api/v1/auth/*` |

## Background Sync

Provider data sync'i iki yolla tetiklenir:

1. **Manuel:** `GET /api/v1/providers/sync` endpoint'i
2. **Otomatik:** `BackgroundSyncService` — uygulama başladığında ve her 30 dakikada bir çalışır

Sync süreci:
1. Provider'lardan HTTP ile veri çekilir (Circuit Breaker korumalı)
2. Adapter'lar ile domain model'e dönüştürülür
3. Scoring service ile puanlanır
4. PostgreSQL'e persist edilir
5. Redis cache invalidate edilir
6. RabbitMQ üzerinden `ContentsSyncedEvent` publish edilir
7. Elasticsearch index'i güncellenir

## Dashboard

`http://localhost:8080/dashboard.html` adresinden erişilen minimal bir web UI:

- Login form (JWT auth)
- Keyword search
- Type filter (Video / Text / Tümü)
- Sort (Score / Date)
- Pagination
- Content card'ları (score, type, metrics)
- Manuel sync butonu

## Clean Code Yaklaşımı

- **SOLID prensipleri** — Her class tek sorumluluk, dependency injection ile loose coupling
- **DRY** — Ortak logic service ve extension method'larda
- **Async/await** — Tüm I/O operasyonları asenkron
- **DTO pattern** — Domain entity'ler hiçbir zaman dışarıya expose edilmez
- **Nullable reference types** — Tüm projelerde enable
- **Structured logging** — Serilog ile console ve file sink
- **FluentValidation** — Request validation pipeline behavior ile
- **Global exception handling** — Middleware ile merkezi hata yönetimi

## Güvenlik

| Önlem | Detay |
|-------|-------|
| JWT Bearer auth | HS256 signed token, 1 saat expiry |
| Rate limiting | Global, search ve auth endpoint'lerinde ayrı policy'ler |
| Security header'lar | X-Frame-Options, X-Content-Type-Options, Referrer-Policy, X-XSS-Protection |
| CORS restriction | Sadece localhost origin'lere izin |
| Input validation | FluentValidation ile tüm request'ler validate edilir |
| SQL injection protection | EF Core parameterized query'ler |
| Resilience pattern | Circuit breaker, retry, timeout provider call'larında |
| Docker port binding | Infrastructure port'lar sadece `127.0.0.1`'e bind |

### Production Ortam İçin Öneriler

| Alan | Öneri |
|------|-------|
| JWT Secret | Environment variable ile inject edin, minimum 32 karakter |
| Credential'lar | Identity provider (OAuth2/OIDC) veya secret manager kullanın |
| HTTPS | Reverse proxy (nginx/traefik) arkasında TLS termination |
| Logging | Serilog Seq/Elasticsearch sink + Kibana dashboard |
| Monitoring | Prometheus + Grafana ile metric toplama |
| Rate Limiting | Redis-backed distributed rate limiter |
| CORS | Production domain'leri explicit olarak tanımlayın |
