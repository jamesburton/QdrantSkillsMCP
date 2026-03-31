# Project Research Summary

**Project:** QdrantSkillsMCP v1.1 — Shared Server Milestone
**Domain:** .NET 10 MCP server — HTTP transports, MCP OAuth 2.0, Azure IaC, GitHub Actions CI/CD
**Researched:** 2026-03-31
**Confidence:** HIGH (all four research files rated HIGH overall; two MEDIUM areas noted below)

## Executive Summary

QdrantSkillsMCP v1.1 converts the existing stdio-only MCP tool server into a shared, multi-tenant HTTP server deployable to Azure Container Apps. The recommended approach is a strict four-phase build — HTTP transport first (foundation), Entra authentication second (depends on HTTP), Bicep IaC third (depends on auth config shape), Docker and CI/CD last (depends on everything) — because each phase is a hard prerequisite for the next with no safe parallelism. The .NET MCP C# SDK v1.2.0 provides Streamable HTTP and legacy SSE endpoints through a single `MapMcp()` call, keeping the implementation surface small. The entire HTTP feature set is a conditional branch in `Program.cs`; the existing stdio path stays untouched.

The MCP OAuth 2.0 story is clean once the architecture is correct: the MCP server is a **resource server only**, Azure Entra is the authorization server. Three packages add the complete auth capability — `ModelContextProtocol.AspNetCore` 1.2.0, `Microsoft.Identity.Web` 4.6.0, and a `<FrameworkReference>` for `Microsoft.AspNetCore.App`. Do not implement token issuance, PKCE, or consent flows — Entra handles all of that. The one non-trivial auth requirement is serving `/.well-known/oauth-protected-resource` (RFC 9728 Protected Resource Metadata), a static JSON endpoint that is REQUIRED by the MCP November 2025 spec for client auto-discovery.

The highest-risk areas are the Bicep Graph extension for Entra app registration (GA July 2025 but newer, with documented limitations around secrets and group assignments that require post-deploy scripts) and the `PackAsTool` + `FrameworkReference` interaction (needs early validation via `dotnet pack`). Both risks are addressable with known mitigations. Everything else follows well-documented patterns with high-confidence official sources.

---

## Key Findings

### Recommended Stack

The v1.0 stack is unchanged. Three additions land in `Infrastructure.csproj`:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
<PackageReference Include="Microsoft.Identity.Web" Version="4.6.0" />
```

`ModelContextProtocol` also upgrades from 1.1.0 to 1.2.0 (required by `ModelContextProtocol.AspNetCore`). The upgrade has no compile-time breaks but changes the default for `EnableLegacySse` from `true` to `false` — explicitly set `EnableLegacySse = true` to keep backward compat with SSE-only clients such as Claude Desktop.

**Core new technologies:**
- `ModelContextProtocol.AspNetCore` 1.2.0 — HTTP transport; `MapMcp()` registers Streamable HTTP + legacy SSE automatically; no manual endpoint wiring needed
- `Microsoft.Identity.Web` 4.6.0 — Entra JWT validation via single `AddMicrosoftIdentityWebApi()` call; handles JWKS rotation, issuer validation, audience checks, and scope claims
- Bicep + Graph extension (GA July 2025) — declarative Entra app registration via `Microsoft.Graph/applications@v1.0`; supports OAuth2 permission scopes
- GitHub Actions OIDC federation — `azure/login@v2` with federated credentials eliminates client secret rotation entirely
- `azure/container-apps-deploy-action@v1` — deploys new revision to ACA by image reference

**What NOT to add:** `Azure.Identity`, MSAL.NET, `Duende.IdentityServer`, OIDC middleware, `Swashbuckle`, YARP, Dapr, Terraform, `DistributedCacheEventStreamStore` (v1.1 is single-instance). See STACK.md for full rationale on each.

### Expected Features

**Must have (table stakes):**
- Streamable HTTP transport with `POST /`, `GET /`, `DELETE /` endpoints via `MapMcp()`
- `EnableLegacySse = true` for backward compat (`GET /sse`, `POST /message`)
- `--sse` / `--streamable-http` / `--url {URL}` CLI flags (all three map to the same `WithHttpTransport()` code path)
- JWT Bearer validation on every HTTP request — 401 for missing or invalid token
- Audience (`aud`) validation against `api://{client-id}` — CRITICAL security requirement
- Scope enforcement: `skills:read` for read tools, `skills:write` for write tools
- Entra app registration with `skills:read` and `skills:write` OAuth2 scopes
- App roles (`SkillReader`, `SkillWriter`) assigned from 4 Q-Hub groups — cleaner than raw group GUIDs in JWT
- `/.well-known/oauth-protected-resource` endpoint (RFC 9728) — required by MCP November 2025 spec
- `/health` endpoint for Container Apps liveness and readiness probes
- Bicep: Container Apps environment + app + managed identity + ACR + Log Analytics workspace
- Bicep: Entra app registration + service principal + scopes (Graph extension)
- GitHub Actions: `ci.yml` (build + test), `deploy.yml` (build → ACR push → ACA deploy)
- Dockerfile updated: `EXPOSE`, `--streamable-http` entrypoint, auth env var placeholders

**Should have (differentiators):**
- `groupMembershipClaims: "ApplicationGroup"` on resource app manifest — prevents group claim overage
- `az account get-access-token --resource api://{client-id}` documented in setup wizard — developer convenience
- Kestrel `KeepAliveTimeout` tuned to 2 hours — prevents SSE connection drops
- CORS middleware in HTTP branch — future-proofs for browser-based MCP clients
- Configurable port via `--url` / `ASPNETCORE_URLS` — avoids local port conflicts (default port must not be 5000/5001 or macOS AirPlay port)
- Container Apps secrets (not plain env vars) for API keys and connection strings
- Image tagged with Git SHA, not `latest` — reliable rollback

**Defer (v2+):**
- Dynamic Client Registration (RFC 7591) — known clients; pre-register in Entra
- Stateless horizontal scaling / Redis session store — single-instance ACA is sufficient for team use
- SSE event ID resumability / `EventStreamStore` — not needed until multi-instance
- Key Vault references for secrets — Container Apps inline secrets are acceptable for v1.1
- Custom domain + managed TLS certificate — default `*.azurecontainerapps.io` is sufficient
- Multi-tenant Entra support — explicitly out of scope per PROJECT.md
- Blue/green via multiple-revision mode — over-engineering for current deployment frequency
- App Service alternative Bicep module — include only if ACA has a specific blocker

### Architecture Approach

The architecture is a **conditional branch in `Program.cs`**. The existing 4-way branch (`--config`, `--console`, `--setup`, default stdio) gains a fifth branch that fires on `--sse`, `--streamable-http`, or `--url`. The stdio branch and all other branches are untouched. The HTTP branch uses `WebApplication.CreateBuilder()` instead of `Host.CreateApplicationBuilder()`, calls `AddEntraAuthentication()` conditionally, adds `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`, and builds the pipeline as `UseAuthentication() → UseAuthorization() → MapMcp().RequireAuthorization()`. Auth registration is a new `Auth/AuthRegistration.cs` extension method called only in the HTTP branch — stdio mode has no auth pipeline.

**Major new components:**
1. **HTTP transport branch in Program.cs** — `WebApplication.CreateBuilder` + `WithHttpTransport()` + `MapMcp()`; `--sse` and `--streamable-http` converge to the same code path
2. **`Auth/AuthRegistration.cs`** — `AddEntraAuthentication()` extension; registers JWT bearer via `AddMicrosoftIdentityWebApi()`; defines `SkillsRead` and `SkillsWrite` authorization policies
3. **`/.well-known/oauth-protected-resource` endpoint** — small static `MapGet` serving RFC 9728 JSON pointing to Entra as authorization server
4. **`/health` endpoint** — `MapGet` or `MapHealthChecks`; checks Qdrant connectivity for readiness
5. **`infra/` Bicep modules** — `main.bicep` orchestrator calling `entra-app.bicep`, `container-apps.bicep` (environment + app + managed identity), `monitoring.bicep`
6. **`.github/workflows/ci.yml`** — build + test on every push/PR to main
7. **`.github/workflows/deploy.yml`** — OIDC Azure login → ACR push (SHA tag) → ACA revision deploy on `v*` tags or manual dispatch

**Session tracking note:** `InMemorySessionTracker` already supports keyed sessions. HTTP mode must pass the MCP `Mcp-Session-Id` (provided by the SDK) to the session tracker rather than `null`/`__default__`. The tools currently use `sessionId: null`; this must be updated for multi-client HTTP correctness.

### Critical Pitfalls

1. **stdout contamination breaks stdio** — `WebApplication.CreateBuilder()` starts Kestrel. If invoked in stdio mode, it writes to stdout and kills the MCP JSON-RPC stream. Fix: keep builder paths strictly mutually exclusive in `Program.cs`. Verify with a regression test after every change to the transport branch.

2. **Missing or wrong audience (`aud`) validation** — Without configuring "Expose an API" in Entra and setting an Application ID URI, Entra issues tokens with Graph's audience (`00000003-...`). JWT middleware rejects all tokens. Fix: configure `api://{client-id}` Application ID URI and custom scopes before writing any auth code.

3. **Group claims on the wrong app registration** — `groupMembershipClaims` must be set on the **resource** (server) app registration, not the client app. Access tokens are shaped by the resource manifest, not the client manifest. Fix: set `groupMembershipClaims: "ApplicationGroup"` on the server app reg; assign the 4 Q-Hub groups via Enterprise Applications.

4. **Bicep cannot create app secrets or group-to-role assignments** — The Graph Bicep extension cannot set `passwordCredentials` and cannot create `appRoleAssignedTo` assignments. Fix: plan explicit post-deploy steps (DeploymentScript or `az rest` CLI calls) for group assignments; use OIDC federated credentials for GitHub Actions (no secret needed); use managed identity for the container (no secret needed).

5. **Building against the March 2025 MCP auth spec** — The March spec had the MCP server acting as its own authorization server. The November 2025 spec (current) separates resource server from authorization server. Fix: implement `/.well-known/oauth-protected-resource` pointing to Entra; never implement `/authorize`, `/token`, or `/register` on the MCP server.

6. **`PackAsTool` + `FrameworkReference` interaction** — Untested combination. If `dotnet pack` fails or produces an oversized package, the NuGet distribution channel breaks. Fix: run `dotnet pack` as the very first action in Phase 1. If it fails, the mitigation is a separate server-only project that is not packed as a tool.

---

## Implications for Roadmap

The dependency chain is strict. There is no safe parallelism between phases — each phase is a hard prerequisite for the next.

### Phase 1: HTTP Transport (no auth)

**Rationale:** Foundation for everything. Auth, Docker, and CI/CD all require working HTTP endpoints. Can be validated entirely locally without Azure. The highest packaging risk (`PackAsTool` + `FrameworkReference`) is isolated here and must pass before proceeding.

**Delivers:**
- `--sse` / `--streamable-http` / `--url` CLI flags working
- `WebApplication`-based HTTP branch in `Program.cs`
- Streamable HTTP + legacy SSE endpoints via `MapMcp()` with `EnableLegacySse = true`
- Kestrel timeout configuration for long-lived SSE connections (2-hour `KeepAliveTimeout`)
- CORS middleware (permissive in v1.1, tighten later)
- `/health` endpoint (minimal liveness response)
- Dockerfile updated: `EXPOSE`, `--streamable-http` entrypoint, auth env var placeholders
- `dotnet pack` verified: NuGet tool still builds and installs correctly
- Regression test: stdio mode works identically before and after

**Addresses:** Streamable HTTP table stakes, legacy SSE backward compat, configurable URL and port

**Avoids:** stdout contamination (mutually exclusive builder paths), deprecated SSE-only transport, Kestrel timeout drops, port conflicts (non-standard default port)

**Research flag:** Standard patterns — `MapMcp()` and `WithHttpTransport()` are documented in official C# SDK docs with working samples. Skip per-phase research.

---

### Phase 2: Entra Authentication

**Rationale:** Depends on working HTTP transport from Phase 1. JWT validation middleware plugs into the ASP.NET Core pipeline established there. Can be tested with `az account get-access-token` tokens immediately after a manual Entra app registration is created — no Bicep required yet.

**Delivers:**
- Entra app registration (manual or `az cli` — Bicep is Phase 3)
- `Auth/AuthRegistration.cs`: `AddMicrosoftIdentityWebApi()` + `SkillsRead` / `SkillsWrite` policies
- `AzureAd` config section support (`TenantId`, `ClientId`, `Audience`)
- `MapMcp().RequireAuthorization()` — 401 and 403 responses with correct `WWW-Authenticate` headers
- `/.well-known/oauth-protected-resource` endpoint (RFC 9728)
- App roles (`SkillReader`, `SkillWriter`) with `groupMembershipClaims: "ApplicationGroup"`
- `InMemorySessionTracker` updated to use MCP session ID in HTTP mode
- Auth registration conditional on HTTP mode — stdio mode unchanged

**Addresses:** JWT validation, audience enforcement, scope enforcement, Protected Resource Metadata, app roles vs group GUIDs, az CLI developer convenience

**Avoids:** Audience mismatch, group claims on wrong manifest, March-spec auth server pattern, token passthrough confused deputy, groups overage

**Research flag:** Standard patterns — `Microsoft.Identity.Web` is extensively documented by Microsoft. The Protected Resource Metadata endpoint is a simple static `MapGet`. Skip per-phase research.

---

### Phase 3: Bicep IaC

**Rationale:** Depends on Phase 2 for the final auth config shape (ClientId, scopes, app role IDs) and Phase 1 for container port and entrypoint. Can be deployed manually first to validate infrastructure before wiring CI/CD. The Graph Bicep extension is the only MEDIUM-confidence area in the entire research — spike before committing to full implementation.

**Delivers:**
- `infra/main.bicep` orchestrator + `main.bicepparam`
- `infra/modules/entra-app.bicep`: app registration, scopes, service principal (Graph extension)
- `infra/modules/container-apps.bicep`: Log Analytics workspace, ACR, Container Apps environment + app, user-assigned managed identity
- Container App ingress (HTTPS, external), health probes, secrets (not plain env vars), managed identity for ACR pull, single-revision mode, min 1 replica
- Post-deploy script / DeploymentScript for group-to-role assignments (documented Bicep limitation)
- OIDC federated credential on deployment identity (prerequisite for Phase 4)
- Parameters for tenant ID, client ID, Qdrant URL, embedding API key (as secrets)

**Addresses:** Full Azure IaC, secrets not in env vars, health probes, single-revision zero-downtime mode, managed identity auth

**Avoids:** Bicep cannot create secrets (use managed identity + OIDC), Bicep cannot assign groups (post-deploy script), Entra replication delays (add `dependsOn` and retry in DeploymentScript), no `what-if` for Graph resources (document and accept)

**Research flag:** MEDIUM confidence for Graph Bicep. `Microsoft.Graph/appRoleAssignedTo` behavior and the group assignment workflow need hands-on verification. Run a minimal spike (app reg + SP + one scope) in a test subscription before writing the full module.

---

### Phase 4: Docker and CI/CD

**Rationale:** Depends on all prior phases. The workflow references the final Dockerfile (Phase 1), the Bicep templates (Phase 3), and ACR and ACA resource names from deployed infrastructure. Automates what was manually validated in Phases 1–3.

**Delivers:**
- `.github/workflows/ci.yml`: build + unit tests on every push and PR to main
- `.github/workflows/deploy.yml`: OIDC Azure login → Docker build → ACR push (SHA tag) → `container-apps-deploy-action` revision deploy
- Smoke test / health check curl after deploy
- Images tagged `${{ github.sha }}` — never `latest`
- GitHub Actions secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (no client secret)
- `vars`: `ACR_LOGIN_SERVER`, `RESOURCE_GROUP`, `CONTAINER_APP_NAME`
- Integration test strategy: unit tests in CI; Aspire integration tests deferred (require Docker-in-Docker)

**Addresses:** Automated deploy pipeline, image tagging, no secrets in workflows, gate deployment on passing tests

**Avoids:** `latest` tag (unreliable rollback), service principal JSON secret (use OIDC), separate build and test jobs using different source trees

**Research flag:** Standard patterns — `azure/login@v2` OIDC and `azure/container-apps-deploy-action@v1` have Microsoft-authored tutorials. Skip per-phase research.

---

### Phase Ordering Rationale

- HTTP before auth — there is no pipeline to protect without HTTP endpoints
- Auth before Bicep — Bicep's Entra module needs the final scope and app role definitions; premature Bicep risks schema drift
- Bicep before CI/CD — the deploy workflow references ACR URL, resource group, and app name that only exist after Bicep runs
- No parallelism — all phases have strict dependency edges

---

### Research Flags

**Needs per-phase research during planning:**
- **Phase 3 (Bicep IaC):** Graph Bicep extension is GA but newer; `appRoleAssignedTo` resource behavior and group assignment workflow need hands-on spike before committing to the full `entra-app.bicep` module. Budget a spike day at the start of Phase 3.

**Standard patterns (skip per-phase research):**
- **Phase 1 (HTTP Transport):** `MapMcp()` and `WithHttpTransport()` are documented in official C# SDK docs.
- **Phase 2 (Entra Auth):** `AddMicrosoftIdentityWebApi()` is extensively documented by Microsoft. Protected Resource Metadata is a static JSON endpoint.
- **Phase 4 (CI/CD):** `azure/login@v2` OIDC and `container-apps-deploy-action@v1` have Microsoft-authored complete examples.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All packages verified on NuGet as of 2026-03-31. `PackAsTool` + `FrameworkReference` interaction is the one unverified combination — validate with `dotnet pack` in Phase 1 task 1. |
| Features | HIGH | Derived from MCP 2025-03-26 spec (target) and 2025-11-25 spec (current). Feature boundaries are clear. Table stakes vs differentiators vs defer split is well-supported. |
| Architecture | HIGH | Verified against official C# SDK docs, official samples, and Microsoft.Identity.Web docs. `WebApplication` vs `Host` builder split is an established .NET pattern. |
| Pitfalls | HIGH | All critical pitfalls backed by official Microsoft docs or explicit MCP spec text. Bicep Graph limitations are documented by Microsoft. Auth spec version drift is confirmed against both spec versions. |

**Overall confidence:** HIGH

### Gaps to Address

- **`PackAsTool` + `FrameworkReference` interaction:** The research assumes this works based on the standard console-app-with-ASP.NET-Core pattern. Not validated against the specific `PackAsTool=true` flag. Validate in Phase 1 task 1 before writing any other HTTP code. Fallback: a separate `QdrantSkillsMCP.Server` project that is not packed as a tool.

- **MCP session ID exposure to tool classes in HTTP mode:** The SDK exposes `IMcpServer` per session, but the exact API for tool classes to retrieve the current session ID and pass it to `ISessionTracker` needs code-level verification during Phase 1. Research confirms the need but not the exact call site.

- **Graph Bicep `appRoleAssignedTo` resource:** Research confirms group-to-role assignment requires post-deploy scripting, but the exact Bicep `DeploymentScript` pattern for Graph API calls was not validated end-to-end. Spike required at the start of Phase 3.

- **Target client MCP spec version alignment:** Research targets MCP 2025-03-26 (C# SDK 1.2.0). Whether Claude Code, GitHub Copilot, and other target clients expect the March or November 2025 auth endpoints affects which discovery endpoints are strictly required. Verify client compatibility before finalizing the Protected Resource Metadata implementation in Phase 2.

---

## Sources

### Primary (HIGH confidence)
- [NuGet: ModelContextProtocol.AspNetCore 1.2.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/) — package existence and version
- [NuGet: Microsoft.Identity.Web 4.6.0](https://www.nuget.org/packages/Microsoft.Identity.Web) — package existence and version
- [MCP C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk) — `MapMcp()`, `WithHttpTransport()`, `HttpServerTransportOptions`
- [MCP 2025-03-26 Authorization Spec](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization) — auth flow, scope requirements
- [MCP 2025-11-25 Authorization Spec](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization) — Protected Resource Metadata (MUST), resource server pattern, token passthrough prohibition
- [Microsoft Learn: Protected web API app configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-app-configuration) — JWT validation, audience, scopes
- [Microsoft Learn: Configure group claims and app roles](https://learn.microsoft.com/en-us/security/zero-trust/develop/configure-tokens-group-claims-app-roles) — `groupMembershipClaims`, app roles
- [Microsoft Learn: Microsoft.App/containerApps Bicep reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/containerapps) — Bicep resource types and API versions
- [Microsoft Learn: Microsoft.Graph/applications Bicep reference](https://learn.microsoft.com/en-us/graph/templates/bicep/reference/applications) — Graph extension schema
- [Microsoft Graph Bicep Limitations](https://learn.microsoft.com/en-us/graph/templates/bicep/limitations) — secrets and group assignment limitations
- [Bicep Graph Extension GA Announcement](https://devblogs.microsoft.com/identity/bicep-templates-for-microsoft-entra-id-resources-is-ga/) — GA status confirmation (July 2025)
- [Microsoft Learn: Deploy to Container Apps with GitHub Actions](https://learn.microsoft.com/en-us/azure/container-apps/github-actions) — OIDC federation, deploy action
- [GitHub: Azure/container-apps-deploy-action](https://github.com/Azure/container-apps-deploy-action) — action parameters

### Secondary (MEDIUM confidence)
- [MCP C# SDK: Streamable HTTP Protocol (DeepWiki)](https://deepwiki.com/modelcontextprotocol/csharp-sdk/5.4-streamable-http-protocol) — transport internals
- [Building Remote MCP Servers with .NET and Azure Container Apps](https://dev.to/willvelida/building-remote-mcp-servers-with-net-and-azure-container-apps-cc2) — end-to-end pattern validation
- [Aaron Parecki on MCP OAuth Nov 2025](https://aaronparecki.com/2025/11/25/1/mcp-authorization-spec-update) — spec change analysis
- [MCP Auth with Entra in .NET](https://nikiforovall.blog/dotnet/2025/09/02/mcp-auth.html) — implementation pattern
- [Secure MCP server with Entra ID](https://damienbod.com/2025/09/23/implement-a-secure-mcp-server-using-oauth-and-entra-id/) — implementation reference
- [MCP OAuth Pitfalls (Obsidian Security)](https://www.obsidiansecurity.com/blog/when-mcp-meets-oauth-common-pitfalls-leading-to-one-click-account-takeover) — audience validation and confused deputy patterns

---
*Research completed: 2026-03-31*
*Ready for roadmap: yes*
