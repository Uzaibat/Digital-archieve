# IDADRS Project — Requirements & Dependencies
# ================================================
# NOTE: This project is .NET-based. .NET does NOT use requirements.txt (that is Python).
# NuGet packages are managed via .csproj files. This file documents ALL dependencies,
# environment variables, and system requirements for the full project.


# ──────────────────────────────────────────────
# 1. SYSTEM / BUILD REQUIREMENTS
# ──────────────────────────────────────────────

[Runtime]
dotnet-sdk          = 8.0
dotnet-aspnet       = 8.0
cmake               = >= 3.15          # required to compile native C search library
gcc / build-essential                  # required to compile libsearch_engine.so
libstdc++6                             # required at runtime to load libsearch_engine.so
libgomp1                               # required at runtime for C library threading
curl                                   # required for Docker health check

[Frontend]
node                = >= 18.x
npm                 = >= 9.x

[Database]
postgresql          = >= 14.x          # must be PostgreSQL — uses tsvector GIN index
                                       # (NOT compatible with SQLite or SQL Server)

[Containerisation]
docker              = >= 24.x
docker-compose      = >= 2.x


# ──────────────────────────────────────────────
# 2. BACKEND NUGET PACKAGES
# ──────────────────────────────────────────────

# --- IDADRS.NativeSearch ---
Microsoft.Extensions.DependencyInjection.Abstractions = 8.0.0
Microsoft.Extensions.Logging.Abstractions             = 8.0.0

# --- IDADRS.Infrastructure ---
Microsoft.EntityFrameworkCore                          = 8.0.4
Microsoft.EntityFrameworkCore.Design                   = 8.0.4
Npgsql.EntityFrameworkCore.PostgreSQL                  = 8.0.4
Microsoft.Extensions.Configuration                     = 8.0.0
Microsoft.Extensions.Configuration.Json               = 8.0.0
Microsoft.Extensions.Configuration.EnvironmentVariables = 8.0.0

# --- IDADRS.API ---
Microsoft.AspNetCore.Authentication.JwtBearer          = 8.0.0
Swashbuckle.AspNetCore                                 = 6.x        # Swagger / OpenAPI
Microsoft.Extensions.DependencyInjection               = 8.0.0
Microsoft.Extensions.Logging                           = 8.0.0

# --- IDADRS.Tests ---
Microsoft.NET.Test.Sdk                                 = 17.x
xunit (or NUnit)                                       = latest
Moq                                                    = 4.x


# ──────────────────────────────────────────────
# 3. FRONTEND NPM PACKAGES (React + Vite)
# ──────────────────────────────────────────────

react                = ^18.x
react-dom            = ^18.x
vite                 = ^5.x
@vitejs/plugin-react  = ^4.x

# HTTP client
axios                = ^1.x            # OR use native fetch()

# Routing
react-router-dom     = ^6.x

# (Add any other frontend packages your project uses)


# ──────────────────────────────────────────────
# 4. ENVIRONMENT VARIABLES
# ──────────────────────────────────────────────
# Create a .env file in your project root (NEVER commit this to GitHub)

# --- Backend (required) ---
ConnectionStrings__Default   = Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<pass>
ASPNETCORE_URLS              = http://+:8080
ASPNETCORE_ENVIRONMENT       = Production

# --- JWT Authentication (required) ---
Jwt__Key                     = <your-256-bit-secret-key-min-32-chars>
Jwt__Issuer                  = https://your-api.onrender.com
Jwt__Audience                = https://your-frontend.onrender.com
Jwt__ExpiryMinutes           = 60
Jwt__RefreshExpiryDays       = 7

# --- File Uploads (required if using /uploads) ---
Storage__UploadPath          = /uploads

# --- Frontend (.env in /frontend folder) ---
VITE_API_URL                 = https://your-backend-name.onrender.com


# ──────────────────────────────────────────────
# 5. RENDER DEPLOYMENT CHECKLIST
# ──────────────────────────────────────────────

[Backend — Web Service]
Language             = Docker
Root Directory       = backend
Dockerfile path      = backend/Dockerfile
Port                 = 8080
Health Check Path    = /health

[Frontend — Static Site]
Root Directory       = frontend
Build Command        = npm run build
Publish Directory    = dist
Rewrite Rule         = /* -> /index.html  (Action: Rewrite)

[Database — PostgreSQL]
Type                 = Render Managed PostgreSQL
Free Tier            = Yes (available)
Connect via          = Internal Database URL → set as ConnectionStrings__Default

[Persistent Storage — for /uploads]
Type                 = Render Disk  (paid, $1/GB/month)
Mount Path           = /uploads
OR use               = AWS S3 / Cloudflare R2 (recommended for production)


# ──────────────────────────────────────────────
# 6. DATABASE SCHEMA SUMMARY
# ──────────────────────────────────────────────

Tables (created by EF Core migration 20260426223331_InitialCreate):
  - users         (id, username, email, password_hash, role, created_date)
  - documents     (id, title, description, file_path, upload_date, uploaded_by, category_id, search_vector[tsvector])
  - categories    (id, category_name, description)
  - refresh_tokens(id, user_id, token, issued_at, expires_at, revoked, replaced_by)
  - access_logs   (id, user_id, document_id, access_date, action_type)

Special Indexes:
  - documents.search_vector  → GIN index (PostgreSQL full-text search)
  - users.email              → UNIQUE
  - users.username           → UNIQUE
  - refresh_tokens.token     → UNIQUE

Run migrations:
  dotnet ef database update --project src/IDADRS.Infrastructure --startup-project src/IDADRS.API
  OR automatically via: db.Database.Migrate() in Program.cs on startup


# ──────────────────────────────────────────────
# 7. NATIVE C LIBRARY BUILD COMMANDS
# ──────────────────────────────────────────────

# Compile libsearch_engine.so (Linux/Mac):
  cd src/IDADRS.NativeSearch
  cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
  cmake --build build
  cp build/libsearch_engine.so .

# This is handled automatically by the Dockerfile build stage.
# For local development on Windows, the managed C# fallback is used automatically
# (NativeSearchService gracefully degrades when the .so is unavailable).

