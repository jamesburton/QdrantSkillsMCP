# Requirements: QdrantSkillsMCP v1.1 Shared Server

**Defined:** 2026-03-31
**Core Value:** Agents can semantically search and retrieve the right skills at the right time

## v1.1 Requirements

Requirements for shared server milestone. Each maps to roadmap phases.

### HTTP Transport

- [ ] **TRANS-01**: Server supports Streamable HTTP transport (POST/GET/DELETE /) via --streamable-http or --url {URL} flags
- [ ] **TRANS-02**: Server supports legacy HTTP/SSE transport (GET /sse, POST /message) via --sse flag with EnableLegacySse=true
- [ ] **TRANS-03**: Server supports explicit --stdio flag (remains default when no transport flag specified)
- [ ] **TRANS-04**: Server exposes /health endpoint returning liveness status for container probes
- [ ] **TRANS-05**: Server configures CORS middleware in HTTP mode for browser-based MCP clients
- [ ] **TRANS-06**: Server configures Kestrel KeepAliveTimeout (2 hours) for long-lived SSE connections
- [ ] **TRANS-07**: Configurable listen URL/port via --url, ASPNETCORE_URLS, or config settings
- [x] **TRANS-08**: dotnet pack with PackAsTool=true + FrameworkReference to ASP.NET Core produces valid NuGet package
- [ ] **TRANS-09**: Existing stdio mode works identically after HTTP transport additions (regression verified)
- [ ] **TRANS-10**: Dockerfile updated with EXPOSE, --streamable-http entrypoint, and auth env var placeholders

### Authentication

- [ ] **AUTH-01**: HTTP requests validated with JWT Bearer tokens from Azure Entra — 401 for missing/invalid tokens
- [ ] **AUTH-02**: JWT audience (aud) validated against api://{client-id} — rejects tokens intended for other APIs
- [ ] **AUTH-03**: skills:read and skills:write OAuth2 permission scopes defined on Entra app registration
- [ ] **AUTH-04**: App roles (SkillReader, SkillWriter) enforce scope-based authorization — read tools require skills:read, write tools require skills:write
- [ ] **AUTH-05**: /.well-known/oauth-protected-resource endpoint serves RFC 9728 Protected Resource Metadata pointing to Entra
- [ ] **AUTH-06**: Auth pipeline active only in HTTP transport mode — stdio mode has no auth
- [ ] **AUTH-07**: AzureAd config section (TenantId, ClientId, Audience) integrated into layered config system
- [ ] **AUTH-08**: InMemorySessionTracker uses MCP session ID in HTTP mode for multi-client correctness
- [ ] **AUTH-09**: az CLI token reuse (az account get-access-token --resource) documented and optionally integrated into setup wizard

### Infrastructure as Code

- [ ] **IAC-01**: Bicep creates Entra app registration "Q-Hub MCPs" with skills:read and skills:write scopes via Graph extension
- [ ] **IAC-02**: Bicep creates service principal and app roles (SkillReader, SkillWriter) for the Entra app
- [ ] **IAC-03**: Post-deploy script assigns app roles to qhub-people-dev, qhub-people-devops, qhub-people-qa, qhub-people-ba groups
- [ ] **IAC-04**: Bicep creates Azure Container Apps environment with Log Analytics workspace
- [ ] **IAC-05**: Bicep creates container app with managed identity, ACR pull, health probes, single-revision mode
- [ ] **IAC-06**: Bicep creates Azure Container Registry for image storage
- [ ] **IAC-07**: Container App secrets used for sensitive config (not plain environment variables)
- [ ] **IAC-08**: Bicep creates App Service plan and web app as alternative deployment target
- [ ] **IAC-09**: OIDC federated credential created on deployment identity for GitHub Actions (no client secret)
- [ ] **IAC-10**: Bicep parameterized for tenant ID, subscription, resource group, Qdrant URL, and embedding config

### CI/CD

- [ ] **CICD-01**: GitHub Actions CI workflow runs dotnet build and dotnet test on every push/PR to main
- [ ] **CICD-02**: GitHub Actions deploy workflow authenticates to Azure via OIDC federation (no stored secrets)
- [ ] **CICD-03**: Deploy workflow builds Docker image, pushes to ACR with git SHA tag
- [ ] **CICD-04**: Deploy workflow deploys new Container Apps revision from ACR image
- [ ] **CICD-05**: Post-deploy health check verifies /health endpoint responds on new revision

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Integration

- **INTG-01**: skills-guru integration as first-class backend — push/sync TO and query/search FROM QdrantSkillsMCP

### Auth (Advanced)

- **AUTH-10**: Dynamic Client Registration (RFC 7591) for unknown MCP clients
- **AUTH-11**: Multi-tenant Entra support for cross-organization use

### Scalability

- **SCAL-01**: Stateless horizontal scaling with Redis session store
- **SCAL-02**: SSE event ID resumability for reliable reconnection
- **SCAL-03**: Key Vault references for secret management (replace inline Container Apps secrets)

### Deployment (Advanced)

- **DEPL-01**: Custom domain with managed TLS certificate on Container Apps
- **DEPL-02**: Blue/green deployment via multiple-revision mode

## Out of Scope

| Feature | Reason |
|---------|--------|
| Token issuance / authorization server endpoints | MCP server is resource server only — Entra handles token issuance |
| PKCE / consent flow implementation | Handled entirely by Entra — not server-side work |
| Multi-tenant SaaS hosting | Single-tenant, Q-Hub only |
| GUI / web dashboard | CLI and MCP tools only |
| Non-.NET client SDKs | Agents interact via MCP protocol |
| Terraform / Pulumi | Bicep chosen as IaC tool |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TRANS-01 | Phase 5 | Pending |
| TRANS-02 | Phase 5 | Pending |
| TRANS-03 | Phase 5 | Pending |
| TRANS-04 | Phase 5 | Pending |
| TRANS-05 | Phase 5 | Pending |
| TRANS-06 | Phase 5 | Pending |
| TRANS-07 | Phase 5 | Pending |
| TRANS-08 | Phase 5 | Complete |
| TRANS-09 | Phase 5 | Pending |
| TRANS-10 | Phase 5 | Pending |
| AUTH-01 | Phase 6 | Pending |
| AUTH-02 | Phase 6 | Pending |
| AUTH-03 | Phase 6 | Pending |
| AUTH-04 | Phase 6 | Pending |
| AUTH-05 | Phase 6 | Pending |
| AUTH-06 | Phase 6 | Pending |
| AUTH-07 | Phase 6 | Pending |
| AUTH-08 | Phase 6 | Pending |
| AUTH-09 | Phase 6 | Pending |
| IAC-01 | Phase 7 | Pending |
| IAC-02 | Phase 7 | Pending |
| IAC-03 | Phase 7 | Pending |
| IAC-04 | Phase 7 | Pending |
| IAC-05 | Phase 7 | Pending |
| IAC-06 | Phase 7 | Pending |
| IAC-07 | Phase 7 | Pending |
| IAC-08 | Phase 7 | Pending |
| IAC-09 | Phase 7 | Pending |
| IAC-10 | Phase 7 | Pending |
| CICD-01 | Phase 8 | Pending |
| CICD-02 | Phase 8 | Pending |
| CICD-03 | Phase 8 | Pending |
| CICD-04 | Phase 8 | Pending |
| CICD-05 | Phase 8 | Pending |

**Coverage:**
- v1.1 requirements: 34 total
- Mapped to phases: 34
- Unmapped: 0

---
*Requirements defined: 2026-03-31*
*Last updated: 2026-03-31 — traceability updated after roadmap creation*
