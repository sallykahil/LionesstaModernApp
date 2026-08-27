# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build
dotnet run          # http://localhost:5178 / https://localhost:7239
```

Swagger UI is available at `/swagger` in Development only.

## Database Migrations (EF Core)

```powershell
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture

ASP.NET Core 8.0 Web API for a product image gallery. Two environments:
- **Development** — `appsettings.Development.json`, local SQL Server, local Azure emulator credentials
- **Production** — `appsettings.json`, Azure SQL Database, Azure Blob Storage

### Request Flow

```
HTTP Request
  → Controllers (AuthController / ImagesController)
  → Services (BlobService for Azure Blob; direct DbContext for DB)
  → AppDbContext (EF Core, single table: ProductImages)
```

`Helpers/Program.cs` owns service registration and the middleware pipeline:
static files → CORS → HTTPS redirect → authentication → authorization → controllers.

### Authentication

JWT Bearer (HMAC-SHA256, 8-hour expiry).  
`POST /api/auth/login` validates credentials from config (`Admin` section) and returns a token.  
`JwtHelper` generates tokens; admin credentials and the JWT secret (`Jwt` section) live in appsettings.  
Protected endpoints use `[Authorize(Roles = "Admin")]`.

### Image Handling

`BlobService` receives a raw upload, uses **SixLabors.ImageSharp** to crop/resize to **800×1000 px** and re-encode as JPEG, then uploads to Azure Blob Storage. The blob URL is stored in `ProductImage.ImageUrl`. Delete removes from both Blob and the database.

### Key Paths

| Path | Purpose |
|---|---|
| `Helpers/Program.cs` | DI registration + middleware pipeline |
| `Data/AppDbContext.cs` | EF Core context (`ProductImages` DbSet) |
| `Models/ProductImage.cs` | Domain entity |
| `Controllers/ImagesController.cs` | Image CRUD + file upload |
| `Controllers/AuthController.cs` | JWT login endpoint |
| `Services/BlobService.cs` | Azure Blob upload/delete + ImageSharp resize |
| `Helpers/JwtHelper.cs` | Token generation |
| `DTOs/` | Request/response shapes |
| `Migrations/` | EF Core schema history |

### API Surface

| Method | Path | Auth |
|---|---|---|
| POST | `/api/auth/login` | — |
| GET | `/api/images` | — (optional `?category=`) |
| GET | `/api/images/{id}` | — |
| POST | `/api/images/upload` | Admin |
| PUT | `/api/images/{id}` | Admin |
| DELETE | `/api/images/{id}` | Admin |
