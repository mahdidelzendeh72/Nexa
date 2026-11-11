# MAF

.NET 10 solution for an authenticated Agent backend, a standalone Blazor
WebAssembly client, and a PostgreSQL-backed knowledge/RAG pipeline.

## Projects

- `src/Maf.Agent.Api`: ASP.NET Core API with Identity, ingestion, and hybrid retrieval.
- `src/Maf.Client`: Blazor WebAssembly login, knowledge management, and search UI.
- `tests/Maf.Agent.Api.Tests`: normalization, chunking, RRF, authorization, and parser tests.

Agent Framework and AG-UI behavior remain deferred to the next phase.

## Local configuration

The API reads sensitive development values from .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=55432;Database=maf;Username=maf;Password=maf_dev" --project src/Maf.Agent.Api
dotnet user-secrets set "SeedUser:Email" "<email>" --project src/Maf.Agent.Api
dotnet user-secrets set "SeedUser:Password" "<password>" --project src/Maf.Agent.Api
```

The development user is created only when the API runs in the Development
environment. Never add these values to source-controlled configuration.

## PostgreSQL and pgvector

The knowledge schema requires both `pg_trgm` and `vector`. A normal PostgreSQL
Windows installation usually includes `pg_trgm` but not pgvector. The included
Compose service runs a pgvector-enabled PostgreSQL on port `55432`, leaving a
native PostgreSQL on `5432` unchanged:

```powershell
docker compose up -d postgres
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Maf.Agent.Api
```

The migration creates both extensions, knowledge tables, generated full-text
vectors, GIN indexes, and the HNSW cosine index.

## Knowledge ingestion

`KnowledgeManager` can upload PDF, DOCX, XLSX, Markdown, and TXT files up to
100 MB at `/knowledge/documents`. Original bytes are streamed into PostgreSQL
`bytea`. SHA-256 duplicates return the existing document instead of creating a
copy. A single-reader channel and hosted worker extract, normalize, chunk, and
optionally embed each document. Pending or interrupted jobs are recovered when
the API starts.

Scanned PDFs use local Tesseract OCR. Install the native Tesseract runtime for
the host OS and place `fas.traineddata` and `eng.traineddata` in the configured
`Knowledge:Ocr:TessDataPath` directory. Text PDFs do not require Tesseract.
OCR is supported on Windows and Linux.

### Optional Ollama embeddings

Lexical full-text and trigram retrieval always remain available. To enable
1024-dimensional BGE-M3 embeddings:

```powershell
ollama pull bge-m3
ollama serve
```

Set `Knowledge:Embedding:Provider` to `Ollama` through User Secrets or an
environment variable:

```powershell
dotnet user-secrets set "Knowledge:Embedding:Provider" "Ollama" --project src/Maf.Agent.Api
```

If Ollama is disabled or unavailable, ingestion finishes as
`ReadyWithoutEmbedding` and search reports `LexicalFallback`. When available,
search fuses full-text, trigram, and vector rankings with reciprocal-rank
fusion (RRF).

### Roles and API

The development seed user receives `KnowledgeReader` and `KnowledgeManager`.
Readers can list/download documents, search, and check health. Managers can
also upload, retry/reindex, and delete.

Knowledge routes are under `/api/knowledge`:

- `POST/GET /documents`, `GET/DELETE /documents/{id}`
- `GET /documents/{id}/download`
- `POST /documents/{id}/retry` and `/reindex`
- `POST /search`
- `GET /health`

## Run

Start the API and client in separate terminals:

```powershell
dotnet run --project src/Maf.Agent.Api --launch-profile https
dotnet run --project src/Maf.Client --launch-profile https
```

- API: `https://localhost:7126`
- Client: `https://localhost:7178`

## Authentication notes

The `/identity/login` endpoint returns ASP.NET Core Identity proprietary bearer
tokens. They are not JWT or OAuth/OIDC tokens. The client stores the access and
refresh tokens in `localStorage`, as selected for this initial phase.

Values in `localStorage` are accessible to JavaScript and can be exposed by an
XSS vulnerability. For production, prefer an HttpOnly cookie/BFF design or a
standards-based OIDC provider, apply a strict Content Security Policy, and avoid
injecting untrusted HTML.

## Security and resource warning

This phase intentionally has no malware scanner and no parser page-count,
worksheet-row, extracted-text-size, CPU-time, or processing-time limits. The
only ingestion guard is the 100 MB upload cap plus extension/signature checks.
Malicious or highly compressed Office/PDF files can therefore consume excessive
CPU, memory, disk, or OCR time. Add malware scanning, sandboxed parsing, and
strict per-format resource/time limits before exposing uploads to untrusted
users.

## Tests

```powershell
dotnet test Maf.sln
```
