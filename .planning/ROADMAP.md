# Roadmap: QdrantSkillsMCP

## Milestones

- ✅ **v1.0 MVP** — Phases 1-4 (shipped 2026-03-30)
- 🚧 **v1.1 Shared Server** — Phases 5-8 (in progress)

## Phases

<details>
<summary>v1.0 MVP (Phases 1-4) — SHIPPED 2026-03-30</summary>

- [x] Phase 1: Core MCP Server (5/5 plans) — completed 2026-03-25
- [x] Phase 2: Search Intelligence and Embedding Providers (3/3 plans) — completed 2026-03-25
- [x] Phase 3: CLI, Distribution, and Bundled Skill (4/4 plans) — completed 2026-03-26
- [x] Phase 4: Configuration Management (2/2 plans) — completed 2026-03-27

Full phase details: [.planning/milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)

</details>

### v1.1 Shared Server

**Milestone Goal:** Enable QdrantSkillsMCP to run as a shared network server with Azure Entra authentication and full cloud deployment.

**Phase Numbering:**
- Integer phases (5, 6, 7, 8): Planned milestone work
- Decimal phases (5.1, 6.1): Urgent insertions if needed (marked with INSERTED)

- [ ] **Phase 5: HTTP Transport** - Multi-transport support with Streamable HTTP, legacy SSE, and stdio regression safety
- [ ] **Phase 6: Entra Authentication** - MCP OAuth 2.0 via Azure Entra with JWT validation and scope-based authorization
- [ ] **Phase 7: Bicep IaC** - Azure infrastructure as code for Entra app, Container Apps, and Container Registry
- [ ] **Phase 8: CI/CD Pipeline** - GitHub Actions build, test, and deploy automation with OIDC federation

## Phase Details

### Phase 5: HTTP Transport
**Goal**: Server runs over HTTP with Streamable HTTP and legacy SSE transports while preserving identical stdio behavior
**Depends on**: Phase 4 (v1.0 complete)
**Requirements**: TRANS-01, TRANS-02, TRANS-03, TRANS-04, TRANS-05, TRANS-06, TRANS-07, TRANS-08, TRANS-09, TRANS-10
**Success Criteria** (what must be TRUE):
  1. Running with `--http` starts an HTTP server serving both Streamable HTTP and legacy SSE via single `MapMcp()` call
  2. Running with no transport flag (or `--stdio`) works identically to v1.0 — no regressions in existing MCP tool behavior
  3. GET /health returns a liveness response suitable for container health probes (degraded, not unhealthy, when Qdrant is down)
  4. `dotnet pack` produces a valid NuGet tool package that installs and runs correctly with the ASP.NET Core FrameworkReference included
  5. Dockerfile defaults to `--http` mode on port 8080
**Plans:** 3 plans
Plans:
- [x] 05-01-PLAN.md — Package upgrades and PackAsTool + FrameworkReference validation
- [ ] 05-02-PLAN.md — HTTP transport branch, health endpoints, URL config, and unit tests
- [ ] 05-03-PLAN.md — Dockerfile update and stdio regression verification

### Phase 6: Entra Authentication
**Goal**: HTTP endpoints are protected by Azure Entra JWT validation with read/write scope enforcement and MCP-compliant discovery
**Depends on**: Phase 5
**Requirements**: AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTH-07, AUTH-08, AUTH-09
**Success Criteria** (what must be TRUE):
  1. HTTP requests without a valid JWT Bearer token receive 401 Unauthorized — requests with valid tokens are accepted
  2. Tokens with wrong audience (not api://{client-id}) are rejected with 401
  3. Read tools (search-skills, load-skill) require skills:read scope; write tools (add-skill, update-skill, archive-skill, delete-skill) require skills:write scope — missing scope returns 403
  4. GET /.well-known/oauth-protected-resource returns RFC 9728 metadata pointing to Entra as the authorization server
  5. stdio mode has no authentication pipeline — it works exactly as before with no token requirements
**Plans**: TBD

### Phase 7: Bicep IaC
**Goal**: Complete Azure infrastructure is declaratively defined and deployable from Bicep templates
**Depends on**: Phase 6
**Requirements**: IAC-01, IAC-02, IAC-03, IAC-04, IAC-05, IAC-06, IAC-07, IAC-08, IAC-09, IAC-10
**Success Criteria** (what must be TRUE):
  1. `az deployment group create` with the Bicep templates provisions an Entra app registration with skills:read and skills:write scopes, a service principal, and SkillReader/SkillWriter app roles
  2. A Container Apps environment with Log Analytics, ACR, and a container app (managed identity, health probes, secrets) is created by the templates
  3. Post-deploy script assigns app roles to the four Q-Hub groups (dev, devops, qa, ba)
  4. OIDC federated credential exists on the deployment identity for GitHub Actions authentication
  5. All sensitive configuration (API keys, connection strings) is stored as Container App secrets, not plain environment variables
**Plans**: TBD

### Phase 8: CI/CD Pipeline
**Goal**: Code changes are automatically built, tested, and deployed to Azure Container Apps via GitHub Actions
**Depends on**: Phase 7
**Requirements**: CICD-01, CICD-02, CICD-03, CICD-04, CICD-05
**Success Criteria** (what must be TRUE):
  1. Every push and PR to main triggers a CI workflow that runs dotnet build and dotnet test — failures block merge
  2. Deploy workflow authenticates to Azure via OIDC federation with no stored client secrets
  3. Deploy workflow builds a Docker image, pushes to ACR with the git SHA as the tag, and deploys a new Container Apps revision
  4. After deployment, a health check confirms the /health endpoint responds on the new revision
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 5 -> 6 -> 7 -> 8

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core MCP Server | v1.0 | 5/5 | Complete | 2026-03-25 |
| 2. Search and Embeddings | v1.0 | 3/3 | Complete | 2026-03-25 |
| 3. CLI and Distribution | v1.0 | 4/4 | Complete | 2026-03-26 |
| 4. Configuration | v1.0 | 2/2 | Complete | 2026-03-27 |
| 5. HTTP Transport | v1.1 | 0/3 | Planned | - |
| 6. Entra Authentication | v1.1 | 0/0 | Not started | - |
| 7. Bicep IaC | v1.1 | 0/0 | Not started | - |
| 8. CI/CD Pipeline | v1.1 | 0/0 | Not started | - |
