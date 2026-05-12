# Lionessta API

A full-stack web application for **Lionessta** — a handmade crochet brand. It provides a public product gallery for customers and a private admin dashboard for managing product images. Built with ASP.NET Core 8 and deployed on Microsoft Azure.

---

## What It Does

- **Customers** can browse product photos, filter by category, add items to a cart, and place orders via WhatsApp.
- **Admins** can log in to upload, edit, and delete product images through a browser-based dashboard.
- Uploaded images are automatically resized and stored in Azure Blob Storage. Metadata is persisted in Azure SQL Database.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 Web API |
| Database | Azure SQL (Entity Framework Core 8) |
| File Storage | Azure Blob Storage |
| Image Processing | SixLabors.ImageSharp 3 |
| Authentication | JWT Bearer (HMAC-SHA256) |
| Frontend | Vanilla HTML / CSS / JavaScript |
| API Docs | Swagger / Swashbuckle |

---

## Features

### Public Storefront (`/index.html`)
- Product grid loaded from the API
- Filter products by category (Bags, Accessories, Home)
- Lightbox view for product detail
- Client-side shopping cart
- WhatsApp checkout — cart contents are sent as a pre-formatted WhatsApp message

### Admin Dashboard (`/admin.html`)
- Secure login with JWT authentication
- Drag-and-drop image upload
- Image preview before upload
- Automatic resize to 800×1000 px on upload
- Edit label and category on any image
- Delete image (removes from storage and database)
- Category filter tabs

---

## Project Structure

```
LionessstaAPI/
├── Controllers/
│   ├── AuthController.cs       # Login endpoint, JWT issuance
│   └── ImagesController.cs     # Image CRUD endpoints
├── DTOs/
│   ├── ImageUploadDto.cs
│   ├── ImageUpdateDto.cs
│   └── ImageResponseDto.cs
├── Data/
│   └── AppDbContext.cs         # EF Core database context
├── Helpers/
│   ├── Program.cs              # App startup and DI configuration
│   └── JwtHelper.cs            # JWT token generation
├── Models/
│   └── ProductImage.cs         # Database entity
├── Services/
│   ├── IBlobService.cs         # Blob storage interface
│   └── BlobService.cs          # Azure Blob Storage + image resize logic
├── Migrations/                 # EF Core database migrations
└── wwwroot/
    ├── index.html              # Public storefront
    └── admin.html              # Admin dashboard
```

---

## API Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Login and receive a JWT token |
| `GET` | `/api/images` | Public | List all images (optional `?category=`) |
| `GET` | `/api/images/{id}` | Public | Get a single image by ID |
| `POST` | `/api/images/upload` | Admin | Upload a new product image |
| `PUT` | `/api/images/{id}` | Admin | Update image label or category |
| `DELETE` | `/api/images/{id}` | Admin | Delete image from storage and database |

Protected endpoints require the header: `Authorization: Bearer <token>`

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- SQL Server (local) **or** an Azure SQL Database
- Azure Storage account with a blob container
- Visual Studio 2022 or VS Code

---

## Local Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd LionessstaAPI
```

### 2. Configure your secrets

Create or update `LionessstaAPI/appsettings.Development.json` with your local values. **Never commit real credentials.**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=LionessstaDB;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "AzureBlob": {
    "ConnectionString": "YOUR_AZURE_STORAGE_CONNECTION_STRING",
    "ContainerName": "lionessta-images"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_MINIMUM_32_CHARACTERS",
    "Issuer": "LionessstaAPI",
    "Audience": "LionessstaClient"
  },
  "Admin": {
    "Username": "admin",
    "Password": "YOUR_ADMIN_PASSWORD"
  }
}
```

### 3. Apply database migrations

```bash
cd LionessstaAPI
dotnet ef database update
```

### 4. Run the application

```bash
dotnet run
```

The app will be available at:
- `http://localhost:5178` — storefront
- `http://localhost:5178/admin.html` — admin dashboard
- `http://localhost:5178/swagger` — API documentation

---

## Configuration Reference

All configuration keys that must be set in your environment or secrets manager:

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `AzureBlob:ConnectionString` | Azure Blob Storage connection string |
| `AzureBlob:ContainerName` | Name of the blob container for images |
| `Jwt:Key` | JWT signing secret — minimum 32 characters |
| `Jwt:Issuer` | JWT issuer identifier |
| `Jwt:Audience` | JWT audience identifier |
| `Admin:Username` | Admin dashboard login username |
| `Admin:Password` | Admin dashboard login password |

For production, set these as **Azure App Service Application Settings** (environment variables) instead of storing them in `appsettings.json`.

---

## Deployment (Azure)

The project includes a Web Deploy publish profile targeting Azure App Service.

1. Set all configuration values listed above as **Application Settings** in the Azure portal.
2. Publish using Visual Studio: **Build → Publish → LionessstaAPI20260509115108 - Web Deploy**
3. The app connects to Azure SQL and Azure Blob Storage automatically via the configured connection strings.

**Azure resources used:**
- Azure App Service — hosts the API and frontend
- Azure SQL Database — stores image metadata
- Azure Blob Storage — stores image files

---

## Database Schema

```sql
ProductImages
├── Id          INT           PRIMARY KEY IDENTITY
├── Label       NVARCHAR(MAX) NOT NULL
├── Category    NVARCHAR(MAX) NOT NULL
├── ImageUrl    NVARCHAR(MAX) NOT NULL   -- Azure Blob public URL
└── CreatedAt   DATETIME2     NOT NULL
```

---

## Known Limitations

- Cart state is stored in `sessionStorage` — it clears when the browser tab is closed.
- Only one admin account is supported (configured in appsettings).
- Orders are placed via WhatsApp — there is no integrated payment or order management system.
- Images are always resized to 800×1000 px (portrait crop) regardless of the original aspect ratio.

---

## License

This project is private. All rights reserved — Lionessta.
