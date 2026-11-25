# MinIOStorage

A small R&D solution that demonstrates uploading, listing, downloading and
deleting files against a [MinIO](https://min.io) object store, split into a
Blazor UI, a REST API and the MinIO server itself — all wired together with
Docker Compose.

---

## 1. Overview

The solution is made up of three cooperating services:

| # | Service              | Role                                                              |
|---|----------------------|-------------------------------------------------------------------|
| 1 | `file-management-web`| Blazor Server UI that users interact with                         |
| 2 | `file-storage-api`   | ASP.NET Core Web API that owns all MinIO interactions             |
| 3 | `minio`              | Object-storage server (S3-compatible) holding the actual files    |

The UI never talks to MinIO directly — it always goes through the API, which
is the only component that holds MinIO credentials and knows the bucket name.

---

## 2. Architecture

```
          ┌────────────────────────┐        HTTP (JSON / multipart)
  Browser │  file-management-web   │  ─────────────────────────────┐
  ──────▶ │  Blazor Server (UI)    │                                │
          │  :5100  → :8080        │                                ▼
          └────────────────────────┘                   ┌──────────────────────────┐
                                                       │   file-storage-api       │
                                                       │   ASP.NET Core Web API   │
                                                       │   :5000 → :8080          │
                                                       │                          │
                                                       │   /api/files             │
                                                       │     GET     list         │
                                                       │     POST    upload       │
                                                       │     GET /{name} url     │
                                                       │     DELETE /{name}       │
                                                       └───────────┬──────────────┘
                                                                   │ MinIO .NET SDK
                                                                   ▼
                                                       ┌──────────────────────────┐
                                                       │          minio           │
                                                       │  S3-compatible storage   │
                                                       │  API    :9000            │
                                                       │  Console:9001            │
                                                       │  Bucket: employee-       │
                                                       │          documents       │
                                                       └──────────────────────────┘
```

### 2.1 `FileManagement.Web` (Blazor Server UI)

- Interactive Server render mode (`AddInteractiveServerComponents`).
- Main page [Files.razor](FileManagement.Web/Components/Pages/Files.razor) lists
  files in a table and supports multi-file upload (up to 20 files, 100 MB each)
  and per-row delete.
- All I/O goes through [IFileStorageService](FileManagement.Web/Services/IFileStorageService.cs),
  implemented by [ApiFileStorageService](FileManagement.Web/Services/ApiFileStorageService.cs),
  which is a typed `HttpClient` pointing at the API (`FileStorageApi:BaseUrl`).

### 2.2 `MinIO` project — `file-storage-api` (ASP.NET Core Web API)

- Target framework: **net10.0**, project SDK `Microsoft.NET.Sdk.Web`.
- Settings bound from the `Minio` configuration section into
  [MinioSettings](MinIO/Common/MinioSettings.cs) and used to register an
  `IMinioClient` in [Program.cs](MinIO/Program.cs).
- [FilesController](MinIO/Controllers/FilesController.cs) exposes:
  - `GET    /api/files`           — list objects in the bucket
  - `POST   /api/files/upload`    — multipart upload (`IFormFile`)
  - `GET    /api/files/{name}`    — returns a 10-minute pre-signed URL
  - `DELETE /api/files/{name}`    — remove an object
- [MinioFileStorageService](MinIO/Service/MinioFileStorageService.cs) is the
  single implementation of [IFileStorageService](MinIO/Common/IFileStorageService.cs);
  it auto-creates the bucket on first upload and renames each object to a GUID
  + original extension.
- OpenAPI is enabled in Development; `GET /` redirects to `/openapi/v1.json`.

### 2.3 `minio` service

- Image: `bitnamilegacy/minio:2025.4.22-debian-12-r1`.
- Credentials (dev only): `minioadmin` / `minioadmin123`.
- Data volume: `minio-data` mounted at `/bitnami/minio/data`.
- Healthcheck hits `/minio/health/live` so the API only starts once MinIO is
  ready (`depends_on … service_healthy`).

---

## 3. Orchestrator

**Docker Compose** is the orchestrator for this solution — no Kubernetes, no
Aspire, no service mesh.

- [docker-compose.yml](docker-compose.yml) — the authoritative definition of
  the three services, their ports, environment, dependencies, the shared
  `file-storage-network` bridge network and the `minio-data` named volume.
- [docker-compose.override.yml](docker-compose.override.yml) — Visual Studio's
  development overrides: dev env vars, user secrets and dev HTTPS certs
  mounted from `%APPDATA%`.
- [docker-compose.dcproj](docker-compose.dcproj) — the Visual Studio Docker
  Compose project that makes F5 start the whole stack.

Startup order is enforced by Compose:

```
minio (healthy)  →  file-storage-api  →  file-management-web
```

---

## 4. How to run

### 4.1 With Docker Compose (recommended)

Prerequisites: Docker Desktop.

```powershell
# from the repo root
docker compose up --build
```

Once the containers are up:

| URL                               | What it is                          |
|-----------------------------------|-------------------------------------|
| http://localhost:5100             | Blazor UI — go to **Files** page    |
| http://localhost:5000             | API root, redirects to OpenAPI JSON |
| http://localhost:5000/openapi/v1.json | OpenAPI spec                    |
| http://localhost:9001             | MinIO web console (`minioadmin` / `minioadmin123`) |
| http://localhost:9000             | MinIO S3 API                        |

Stop and clean up:

```powershell
docker compose down          # keep the MinIO volume
docker compose down -v       # also wipe uploaded files
```

### 4.2 From Visual Studio

Open `MinIOStorage.slnx`, keep the **Docker Compose** profile selected (see
[launchSettings.json](launchSettings.json)) and press F5 — VS uses the same
compose files plus the dev overrides.

### 4.3 Running the projects directly (without containers)

You need the .NET 10 SDK and a local MinIO (or point the settings at a remote
one). In two terminals:

```powershell
# terminal 1 — API (defaults to http://localhost:5161 in dev)
dotnet run --project MinIO/MinIOStorage.csproj

# terminal 2 — Web UI
dotnet run --project FileManagement.Web/FileManagement.Web.csproj
```

The API reads MinIO settings from
[MinIO/appsettings.Development.json](MinIO/appsettings.Development.json)
(endpoint `localhost:9000`, bucket `employee-documents`). The UI reads
`FileStorageApi:BaseUrl` from
[FileManagement.Web/appsettings.json](FileManagement.Web/appsettings.json) —
override it with an env var or user secret to match the API's actual port.

---

## 5. Technology, tools and libraries

### 5.1 Runtime & frameworks

- **.NET 10** (`TargetFramework: net10.0`) — both projects
- **ASP.NET Core Web API** — `file-storage-api`
- **Blazor Server** with Interactive Server render mode — `file-management-web`
- **Bootstrap 5** — shipped under `wwwroot/lib/bootstrap`, loaded via Blazor's
  static asset pipeline (`MapStaticAssets` + `@Assets[...]`)

### 5.2 NuGet packages

MinIO API ([MinIO/MinIOStorage.csproj](MinIO/MinIOStorage.csproj)):

- `Minio` 7.0.0 — official MinIO .NET SDK
- `Microsoft.AspNetCore.OpenApi` 10.0.14 — built-in OpenAPI generation
- `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` — VS container tooling

Web UI ([FileManagement.Web/FileManagement.Web.csproj](FileManagement.Web/FileManagement.Web.csproj))
relies only on the ASP.NET Core / Blazor framework references, plus
`System.Net.Http.Json` from the BCL for typed JSON calls.

### 5.3 Infrastructure & tooling

- **MinIO** (`bitnamilegacy/minio:2025.4.22-debian-12-r1`) — S3-compatible object store
- **Docker** + **Docker Compose** — orchestration and local dev environment
- **Visual Studio 2022+ / `dotnet` CLI** — build and debug
- **OpenAPI** — API discovery (`/openapi/v1.json`) in Development

### 5.4 Key configuration keys

| Setting                     | Where                                           | Purpose                       |
|-----------------------------|-------------------------------------------------|-------------------------------|
| `Minio:Endpoint`            | API — `appsettings*.json` / compose env         | Host:port of MinIO            |
| `Minio:AccessKey` / `SecretKey` | API — `appsettings*.json` / compose env      | MinIO credentials             |
| `Minio:BucketName`          | API — `appsettings*.json` / compose env         | Target bucket (auto-created)  |
| `Minio:UseSSL`              | API — `appsettings*.json` / compose env         | TLS to MinIO                  |
| `FileStorageApi:BaseUrl`    | Web — `appsettings.json` / compose env          | API base URL used by UI       |

---

## 6. Project layout

```
MinIOStorage/
├─ MinIOStorage.slnx                 ← solution (new slnx format)
├─ docker-compose.yml                ← services: web, api, minio
├─ docker-compose.override.yml       ← VS dev overrides
├─ docker-compose.dcproj             ← VS compose project
├─ MinIO/                            ← file-storage-api (ASP.NET Core Web API)
│  ├─ Program.cs
│  ├─ Controllers/FilesController.cs
│  ├─ Service/MinioFileStorageService.cs
│  ├─ Common/{IFileStorageService.cs, MinioSettings.cs}
│  └─ Dockerfile
└─ FileManagement.Web/               ← Blazor Server UI
   ├─ Program.cs
   ├─ Components/
   │  ├─ App.razor, Routes.razor
   │  ├─ Layout/{MainLayout,NavMenu,ReconnectModal}.razor
   │  └─ Pages/{Home,Files,Weather,Counter,Error,NotFound}.razor
   ├─ Services/{IFileStorageService.cs, ApiFileStorageService.cs, FileItem.cs}
   └─ Dockerfile
```
