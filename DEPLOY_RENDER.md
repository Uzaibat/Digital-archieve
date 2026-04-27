# Deploy IDADRS on Render

This repo includes a Render Blueprint file: `render.yaml`.

## 1) Push project to GitHub

Render deploys from Git repositories.

## 2) Create services with Blueprint

1. Open Render dashboard.
2. Click **New +** -> **Blueprint**.
3. Select this repository/branch.
4. Render will detect `render.yaml` and create:
   - `idadrs-api` (web service, .NET 8 API)
   - `idadrs-client` (static frontend)
   - `idadrs-db` (PostgreSQL)

## 3) Set API URL for frontend

After first deploy, open the static site URL and run this once in browser devtools console:

```js
localStorage.setItem('idadrs_api_url', 'https://idadrs-api.onrender.com/api');
location.reload();
```

If you rename services/domains, use your actual API URL.

## 4) Update production env vars

In `idadrs-api` service -> **Environment**, verify these values:

- `Storage__BaseUrl` = your API public URL (for example `https://idadrs-api.onrender.com`)
- `ALLOWED_ORIGINS` includes your static site domain

If your static domain differs from `https://idadrs-client.onrender.com`, update `ALLOWED_ORIGINS`.

## 5) Run DB migration on Render

From `idadrs-api` service shell:

```bash
dotnet ef database update --project src/IDADRS.Infrastructure --startup-project src/IDADRS.API
```

If `dotnet-ef` isn't available in shell, run migration locally against Render DB connection string, or add a one-off migration step in CI.

## 6) Verify

- API health: `https://<api-domain>/health`
- Swagger: `https://<api-domain>/swagger`
- Frontend: `https://<client-domain>`

Login with seeded admin after DB is migrated and seeded.
