# YourApp — .NET 10 Clean Architecture API template

Template Web API cho dự án mới: Clean Architecture, CQRS với MediatR, EF Core (ghi) + Dapper (đọc) trên PostgreSQL, Redis, JWT, OpenTelemetry, test đầy đủ với Testcontainers.

## Dùng template

```bash
dotnet new install .                       # cài từ thư mục này (chạy lại sau khi sửa template)
dotnet new ca-api -n Acme.Shop             # tạo solution mới, đổi mọi "YourApp" thành "Acme.Shop"
dotnet new ca-api -n Acme.Shop --IncludeSample false   # bỏ feature mẫu Products
dotnet new uninstall .                     # gỡ template
```

| Tuỳ chọn | Mặc định | Ý nghĩa |
|---|---|---|
| `--IncludeSample` | `true` | Kèm feature mẫu `Products` (entity, command/query, controller, migration, test) |
| `--SkipRestore` | `false` | Không tự `dotnet restore` sau khi tạo |

Cổng `launchSettings.json` được sinh ngẫu nhiên cho mỗi project để không đụng nhau.

## Chạy local

Yêu cầu: .NET SDK 10, Docker Desktop.

```bash
cp .env.example .env                                   # điền mật khẩu DB, JWT secret
docker compose up -d postgres redis aspire-dashboard   # hạ tầng (Postgres ở cổng host 5433)
dotnet tool restore                                    # dotnet-ef
dotnet run --project src/YourApp.Api                   # API (Development: tự migrate)
```

Hoặc chạy cả API trong container: `docker compose up --build`.

> Postgres trong Docker mở ra máy host ở cổng **5433** (đổi bằng `POSTGRES_PORT` trong `.env`) để không đụng PostgreSQL cài sẵn trên máy, thường chiếm 5432. Nếu đổi cổng thì sửa luôn `ConnectionStrings:DefaultConnection` trong `appsettings.Development.json`.

| Địa chỉ | Nội dung |
|---|---|
| `http://localhost:<port>/scalar` | Tài liệu API + gọi thử (chỉ Development) |
| `http://localhost:<port>/openapi/v1.json` | Tài liệu OpenAPI |
| `http://localhost:<port>/health/live`, `/health/ready` | Probe cho k8s |
| `http://localhost:18888` | Aspire Dashboard: trace, metrics |

**Lấy token để gọi thử** (chỉ có ở Development):

```bash
curl -X POST http://localhost:<port>/dev/token -H "Content-Type: application/json" \
     -d '{"subject":"dev-user","roles":["Admin"]}'
```

Template chưa có đăng nhập thật. Khi thêm (ASP.NET Identity, Keycloak…), endpoint login dùng `JwtTokenIssuer` hoặc thay hẳn bằng IdP bên ngoài.

## Cấu trúc

```
src/
  YourApp.Domain          Entity, Result/Error, interface repository. Không phụ thuộc gì.
  YourApp.Application     Command/Query + Handler + Validator (MediatR), abstraction (IUnitOfWork, IReadDbContext, ICurrentUser…)
  YourApp.Infrastructure  EF Core, Dapper, Redis, repository, interceptor audit/soft-delete, migration
  YourApp.Api             Controller, auth, rate limit, OpenAPI, health, logging/tracing
tests/
  YourApp.UnitTests          Domain + handler + validator (NSubstitute)
  YourApp.ArchitectureTests  Khoá chiều phụ thuộc giữa các tầng (NetArchTest)
  YourApp.IntegrationTests   API thật + Postgres/Redis trong container (Testcontainers)
```

Chiều phụ thuộc: `Api → Infrastructure → Application → Domain`. Application được dùng EF Core (chỉ trong query handler) nhưng không được tham chiếu provider Npgsql hay Infrastructure. Architecture test sẽ đỏ nếu vi phạm.

## Quy ước

**Thêm một feature** — làm theo `Products`:

1. `Domain/<Feature>/`: entity (setter private, factory trả `Result`), `<Feature>Errors`, `I<Feature>Repository`.
2. `Application/<Feature>/`: mỗi use case một file gồm command/query + validator + handler (handler `sealed`).
3. `Infrastructure/Persistence/`: `<Feature>Configuration` (EF) và `<Feature>Repository`. Repository tự đăng ký DI theo quy ước tên `XxxRepository` ↔ `IXxxRepository`.
4. `Api/Controllers/<Feature>Controller` kế thừa `BaseApiController`, chỉ gọi `ISender`. DTO HTTP để ở `Api/Contracts`.
5. Tạo migration (xem dưới), viết test.

**Xử lý lỗi — hai đường, không trộn:**

| Loại lỗi | Cách làm | HTTP |
|---|---|---|
| Dữ liệu đầu vào sai định dạng | FluentValidation → `ValidationBehavior` ném `CustomValidationException` | 400 + `errors` theo field |
| Lỗi nghiệp vụ dự kiến được | Handler trả `Result.Failure(Error.NotFound/Conflict/…)` | 404 / 409 / 403 … + `code` |
| Lỗi bất ngờ | Cứ để exception bay lên | 500, không lộ chi tiết |

Mọi lỗi trả về dạng `application/problem+json` có `traceId` để tra log/trace.

**Đọc/ghi tách đôi:**

- **Ghi (command):** repository + EF Core có tracking, interceptor audit/soft-delete, `IUnitOfWork`. Command handler **không** được dùng EF trực tiếp (architecture test kiểm).
- **Đọc (query) — mặc định LINQ:** `IReadDbContext.Query<T>()` trả `IQueryable` không tracking, đã lọc bản ghi xoá mềm. Luôn kết thúc bằng `Select` sang DTO để chỉ lấy cột cần (xem `GetProductById`, `ListProducts`, phép chiếu dùng chung `ProductResponse.Projection`).
- **Đọc — SQL tay (Dapper) khi LINQ không hợp:** qua `ISqlConnectionFactory` (xem `GetProductPriceStats`). Dapper **không** đi qua query filter của EF → SQL phải tự lọc `is_deleted = false`.

| Truy vấn | Viết bằng |
|---|---|
| Chi tiết theo id, danh sách lọc/sắp xếp/phân trang, join 2–3 bảng | LINQ + `Select` |
| Báo cáo: `GROUP BY` nhiều tầng, window function, CTE, `percentile_cont`… | SQL tay (Dapper) |
| Tính năng riêng Postgres: JSONB, full-text, `DISTINCT ON`, `LATERAL` | SQL tay (Dapper) |
| Hot path đã đo thấy chậm và không chỉnh được qua LINQ | SQL tay (Dapper) |

Dấu hiệu chuyển sang SQL: phải "vật lộn" để LINQ sinh đúng câu SQL mình muốn. Xem SQL EF sinh ra bằng cách hạ `Microsoft.EntityFrameworkCore.Database.Command` xuống `Information` trong `Serilog:MinimumLevel:Override`.

**Audit & xoá mềm:** entity cài `IAuditableEntity` được tự điền `created_at/by`, `updated_at/by`. Entity cài `ISoftDeletable` thì `Remove()` chỉ bật `is_deleted`; query EF tự lọc (muốn thấy hết dùng `IgnoreQueryFilters()`).

**Transaction:**

- Mặc định chỉ cần **một** `unitOfWork.SaveChangesAsync()` cuối handler — EF tự gói mọi INSERT/UPDATE/DELETE đang track vào một transaction.
- Cần transaction tường minh (gọi `SaveChanges` nhiều lần, trộn `ExecuteUpdate`/SQL thô, đổi isolation level) thì dùng `IUnitOfWork.ExecuteInTransactionAsync`:

```csharp
return await unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    var order = Order.Create(...);              // tạo/nạp entity BÊN TRONG delegate
    orders.Add(order);
    await unitOfWork.SaveChangesAsync(ct);
    var reserved = await inventory.ReserveAsync(order.Id, ct);
    if (reserved.IsFailure) return reserved;    // Result thất bại → tự rollback, không cần ném exception
    await unitOfWork.SaveChangesAsync(ct);
    return Result.Success();
}, cancellationToken: cancellationToken);
```

| Tình huống | Hành vi |
|---|---|
| Delegate chạy xong | Commit |
| Ném exception | Rollback, exception đi lên nguyên kiểu |
| Trả `Result` thất bại | Rollback, trả nguyên `Result` |
| Gọi lồng nhau | Lời gọi trong nhập chung transaction ngoài; chỉ lớp ngoài cùng commit/rollback |
| Lỗi kết nối tạm thời | Chạy lại cả delegate với Change Tracker đã xoá (vì vậy entity phải tạo/nạp trong delegate) |

Không tự gọi `Database.BeginTransaction` trong code: EF đang bật `EnableRetryOnFailure`, transaction tự mở ngoài execution strategy sẽ bị EF từ chối. Delegate có thể bị chạy lại khi retry nên đừng gọi dịch vụ ngoài (gửi mail, gọi API…) bên trong — làm sau khi commit.

## Migration

```bash
dotnet ef migrations add <Ten> -p src/YourApp.Infrastructure -s src/YourApp.Infrastructure -o Persistence/Migrations
dotnet ef migrations script --idempotent -p src/YourApp.Infrastructure -s src/YourApp.Infrastructure -o migrate.sql
dotnet ef migrations bundle -p src/YourApp.Infrastructure -s src/YourApp.Infrastructure -o efbundle
```

- Development: `Database:MigrateOnStartup=true` tự migrate khi chạy.
- Production: **không** migrate lúc khởi động (nhiều pod cùng chạy sẽ tranh nhau). Chạy `efbundle` hoặc `migrate.sql` thành một bước riêng trong CI/CD trước khi deploy.

## Cấu hình

Giá trị bí mật **không** nằm trong `appsettings.json`; đặt qua biến môi trường (`__` thay cho `:`).

| Khoá | Bắt buộc | Ghi chú |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✔ | PostgreSQL |
| `ConnectionStrings__Redis` | ✔ | |
| `JwtSettings__Secret` | ✔ | ≥ 32 ký tự; thiếu/ngắn → app dừng ngay khi khởi động |
| `JwtSettings__Issuer`, `__Audience` | | Mặc định `YourApp` |
| `Cors__AllowedOrigins__0..n` | | Rỗng = chặn cross-origin |
| `RateLimiting__PermitLimit`, `__WindowSeconds` | | Mặc định 100 request / 60 s mỗi user hoặc IP |
| `Security__KnownNetworks__0..n` | | CIDR của reverse proxy tin cậy |
| `Security__UseHttpsRedirection` | | Mặc định tắt (TLS kết thúc ở ingress) |
| `OpenApi__Enabled` | | Bật tài liệu API ngoài Development |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | | Có thì mới bật OpenTelemetry |

## Test

```bash
dotnet test --solution YourApp.slnx                         # tất cả (integration test cần Docker đang chạy)
dotnet test --project tests/YourApp.UnitTests               # chỉ unit test
dotnet test --solution YourApp.slnx --filter-class "*ProductsEndpointsTests"
```

## Build & quản lý gói

- Phiên bản gói khai báo **một chỗ** ở `Directory.Packages.props` (Central Package Management); `.csproj` chỉ ghi tên gói.
- `Directory.Build.props`: `net10.0`, nullable, analyzer mức Recommended, **warning = lỗi**. Tinh chỉnh luật trong `.editorconfig`.
- MediatR khoá ở 12.5.0 — bản MIT cuối; từ 13.x cần license thương mại.

## CI

Có sẵn `.github/workflows/ci.yml` và `.gitlab-ci.yml` (restore → build → quét gói lỗ hổng → test → build image). Giữ file của nền tảng bạn dùng, xoá file còn lại. GitLab cần runner bật `privileged` cho Docker-in-Docker (Testcontainers).
