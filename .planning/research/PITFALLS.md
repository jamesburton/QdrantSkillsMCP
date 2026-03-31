# Pitfalls Research -- v1.1 Shared Server

**Domain:** Adding HTTP transports, MCP OAuth 2.0 (Azure Entra), Bicep IaC, and GitHub Actions CI/CD to an existing .NET 10 MCP stdio server
**Researched:** 2026-03-31
**Overall confidence:** HIGH (verified against MCP spec 2025-11-25, Microsoft official docs, C# SDK docs)

---

## MCP OAuth 2.0 Pitfalls

### CRITICAL: Spec version drift -- March vs November 2025

**What goes wrong:** The March 2025 MCP auth spec assumed the MCP server IS the authorization server. The November 2025 spec formally separates the MCP server (resource server) from the authorization server. If you implement against the March spec, your server will be non-compliant with current clients.

**Warning signs:** You are implementing `/.well-known/oauth-authorization-server`, `/authorize`, `/token`, `/register` endpoints on the MCP server itself. That was the March 2025 pattern.

**Prevention:** Implement against the 2025-11-25 spec. The MCP server is a resource server only. Azure Entra is the authorization server. The MCP server MUST:
1. Implement Protected Resource Metadata (RFC 9728) -- serve `/.well-known/oauth-protected-resource` pointing to Entra as the AS
2. Return `WWW-Authenticate: Bearer resource_metadata="..."` on 401 responses
3. Validate JWT tokens issued by Entra -- do NOT implement token endpoints

**Phase:** Auth phase (first auth work). Get this right before writing any token validation code.

**Confidence:** HIGH -- verified against https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization

### CRITICAL: Missing audience validation on JWT tokens

**What goes wrong:** MCP server accepts any valid Entra token, including tokens issued for Microsoft Graph or other APIs. An attacker with a Graph token could call your MCP tools.

**Warning signs:** Token validation only checks `iss` (issuer) and `exp` (expiry) but not `aud` (audience). Or `aud` check accepts the wrong value.

**Prevention:** The MCP server MUST validate the `aud` claim matches your app registration's Application ID URI (`api://{client-id}`) or client ID. You MUST configure custom scopes in Entra's "Expose an API" blade -- without this, Entra issues Graph-audience tokens, not your-API-audience tokens. The November 2025 spec explicitly states: "MCP servers MUST validate that access tokens were issued specifically for them as the intended audience."

**Phase:** Auth phase. Part of the JWT validation middleware setup.

**Confidence:** HIGH -- verified against MCP spec and Microsoft identity platform docs

### CRITICAL: Token passthrough / confused deputy

**What goes wrong:** If the MCP server calls upstream APIs (Qdrant, embedding providers) using the MCP client's token, this creates a confused deputy vulnerability. The November 2025 spec explicitly prohibits this.

**Warning signs:** Code that reads `Authorization` header from incoming request and passes it to Qdrant or OpenAI calls.

**Prevention:** The MCP server authenticates incoming requests with Entra tokens. Outbound calls to Qdrant/embedding providers use their own credentials (API keys, managed identity). Never forward the MCP client's token. The spec says: "The MCP server MUST NOT pass through the token it received from the MCP client."

**Phase:** Auth phase. Enforce this in architecture review before implementation.

**Confidence:** HIGH -- explicitly stated in MCP 2025-11-25 spec

### HIGH: Scope enforcement gaps -- read vs write

**What goes wrong:** Token is validated but scope claims are not checked per-tool. A user with `skills:read` scope can call `add-skill` or `delete-skill`.

**Warning signs:** JWT validation middleware only checks "is token valid?" without per-endpoint scope checks. All MCP tools accessible with any valid token.

**Prevention:** Define two scopes in Entra: `skills:read` and `skills:write`. Map them to MCP tools:
- `skills:read` -> `search-skills`, `load-skill`, `list-skills`, `get-skill-guide`
- `skills:write` -> `add-skill`, `update-skill`, `delete-skill`, `archive-skill`
Return HTTP 403 with `WWW-Authenticate: Bearer error="insufficient_scope", scope="skills:write"` when scope is missing. The spec defines a step-up authorization flow for this.

**Phase:** Auth phase. Must be designed into the tool invocation pipeline, not bolted on after.

**Confidence:** HIGH -- MCP spec 2025-11-25 defines scope challenge handling

### MODERATE: PKCE not enforced by authorization server

**What goes wrong:** If Entra's app registration doesn't require PKCE, public clients can complete the flow without it, enabling authorization code interception.

**Warning signs:** App registration allows implicit flow or doesn't enforce PKCE.

**Prevention:** In the Entra app registration, set `allowPublicClient: false` for the MCP server app (it's a resource server, not a public client). For any client app registrations that interact with the MCP server, ensure PKCE is required. The MCP spec states: "PKCE is REQUIRED for all clients."

**Phase:** Auth phase / Entra app registration.

**Confidence:** HIGH

### MODERATE: Missing Protected Resource Metadata endpoint

**What goes wrong:** MCP clients can't discover where to authenticate. They fail silently or fall back to non-functional defaults. The November 2025 spec REQUIRES this -- it's not optional.

**Warning signs:** Client connects, gets 401, but has no way to find the authorization server.

**Prevention:** Implement `/.well-known/oauth-protected-resource` on the MCP server returning:
```json
{
  "resource": "https://your-server.azurecontainerapps.io",
  "authorization_servers": ["https://login.microsoftonline.com/{tenant-id}/v2.0"],
  "scopes_supported": ["skills:read", "skills:write"],
  "bearer_methods_supported": ["header"]
}
```
Also include `resource_metadata` in all 401 `WWW-Authenticate` headers.

**Phase:** Auth phase. Implement alongside the first 401 response handling.

**Confidence:** HIGH -- MUST requirement in 2025-11-25 spec

### MODERATE: Token expiry during long MCP sessions

**What goes wrong:** Entra access tokens typically have a 60-90 minute lifetime. MCP sessions can last hours (agent coding sessions). After token expiry, every tool call returns 401 and the client must re-authenticate.

**Warning signs:** Tools work for an hour then all start failing. Users re-authenticate repeatedly.

**Prevention:** This is primarily a client-side concern (refresh tokens), but the server should:
1. Return clear 401 with `WWW-Authenticate` header so clients know to refresh
2. Keep token validation stateless -- don't cache "this session is authenticated"
3. Document expected token lifetime in server metadata
Real-world MCP deployments (Atlassian, Notion) all hit this -- it's a known ecosystem pain point.

**Phase:** Auth phase (server-side). Document for users in a later phase.

**Confidence:** HIGH -- multiple real-world reports of this issue

---

## HTTP Transport Pitfalls

### CRITICAL: stdout contamination breaks stdio when adding HTTP

**What goes wrong:** Adding ASP.NET Core / Kestrel pulls in middleware that writes to stdout. The existing stdio transport reserves stdout exclusively for MCP JSON-RPC. Any stdout pollution kills the stdio transport.

**Warning signs:** Existing stdio mode stops working after adding HTTP transport code. The current `Program.cs` already has the comment: "CRITICAL: ALL logging to stderr. Stdout is reserved for MCP JSON-RPC transport."

**Prevention:** The transport modes must be mutually exclusive branches in `Program.cs` -- they already are (the code uses `if/else if/else` branching). When adding `--sse` and `--streamable-http` modes, add them as new branches that use `WebApplication.CreateBuilder()` instead of `Host.CreateApplicationBuilder()`. Never let Kestrel start in stdio mode. Never let stdio code run in HTTP mode.

**Phase:** Transport phase. First thing to implement and test -- verify stdio still works after adding HTTP code paths.

**Confidence:** HIGH -- verified from current Program.cs code

### CRITICAL: Using deprecated SSE transport instead of Streamable HTTP

**What goes wrong:** SSE transport was deprecated in MCP spec 2025-03-26. Building on it means building on a dead end. Some older clients still expect it, but new clients target Streamable HTTP.

**Warning signs:** Implementing separate `/sse` (GET) and `/message` (POST) endpoints as the primary transport.

**Prevention:** Use `ModelContextProtocol.AspNetCore` package. Call `.WithHttpTransport()` and `app.MapMcp()`. This gives you Streamable HTTP as primary AND legacy SSE endpoints for backward compatibility. The C# SDK's `MapMcp()` automatically maps both:
- Streamable HTTP: POST/GET/DELETE on `/mcp` (or configured path)
- Legacy SSE: GET `/sse` + POST `/message`

**Phase:** Transport phase. Use the SDK's built-in support, don't hand-roll either transport.

**Confidence:** HIGH -- verified from C# SDK docs and DeepWiki

### HIGH: Missing CORS configuration for browser-based MCP clients

**What goes wrong:** Browser-based MCP clients (web agents, VS Code web) get CORS errors. Preflight OPTIONS requests fail. SSE connections blocked.

**Warning signs:** Works from Claude Code (non-browser) but fails from browser-based clients.

**Prevention:** Add CORS middleware in the HTTP transport branch:
```csharp
builder.Services.AddCors(options => options.AddPolicy("MCP", policy =>
    policy.AllowAnyOrigin()  // or restrict to known clients
          .AllowAnyMethod()
          .AllowAnyHeader()
          .WithExposedHeaders("Content-Type")));
app.UseCors("MCP");
```
Place CORS middleware before auth middleware. For SSE specifically, ensure `Cache-Control: no-cache` and `Connection: keep-alive` headers pass through.

**Phase:** Transport phase. Configure alongside Kestrel setup.

**Confidence:** MEDIUM -- depends on which MCP clients will connect; browser clients need this

### HIGH: Kestrel request timeout kills long SSE connections

**What goes wrong:** Kestrel's default `KeepAliveTimeout` (2 minutes) and `RequestHeadersTimeout` (30 seconds) close SSE connections prematurely. The Streamable HTTP GET endpoint streams SSE and needs to stay open indefinitely.

**Warning signs:** SSE connections drop after ~2 minutes. Clients reconnect repeatedly.

**Prevention:** Configure Kestrel limits for the HTTP transport branch:
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});
```
Also set reasonable `MaxConcurrentConnections` since each SSE client holds a connection open.

**Phase:** Transport phase. Part of Kestrel configuration.

**Confidence:** MEDIUM -- default timeouts documented, but actual behavior depends on load balancer/proxy in front

### MODERATE: Session identity changes between stdio and HTTP

**What goes wrong:** The v1.0 session tracking uses MCP connection lifecycle as session boundary with `__default__` sentinel. In HTTP mode, "connection" is different -- Streamable HTTP is stateless per-request (with optional session via `Mcp-Session-Id` header). The `ConcurrentDictionary`-based session tracker may not work correctly.

**Warning signs:** Session tracking (already-loaded skills) doesn't persist across HTTP requests. Or sessions leak memory because they're never cleaned up.

**Prevention:** For Streamable HTTP, use the `Mcp-Session-Id` header as the session key (the SDK handles this). Implement session expiry/cleanup for HTTP mode -- stdio sessions end when the process exits, but HTTP sessions need TTL-based cleanup. Consider a `ISessionManager` interface to abstract the difference.

**Phase:** Transport phase. Must be addressed when wiring up tool DI in HTTP mode.

**Confidence:** MEDIUM -- the exact C# SDK session handling needs verification during implementation

### MODERATE: Port conflicts when running locally

**What goes wrong:** Developer runs the MCP server in HTTP mode on port 5000/5001, which conflicts with other ASP.NET apps or macOS AirPlay (port 5000).

**Warning signs:** "Address already in use" errors on startup.

**Prevention:** Use a non-standard default port (e.g., 5288) and make it configurable via `--port` argument and `ASPNETCORE_URLS` environment variable. Document this. In Container Apps it won't matter (port 80/443 mapped), but local dev needs a sane default.

**Phase:** Transport phase. Minor but affects developer experience.

**Confidence:** HIGH -- common .NET developer experience issue

---

## Azure Entra Pitfalls

### CRITICAL: Group claims not appearing in access tokens

**What goes wrong:** You configure group claims on the client app registration, but groups don't appear in the ACCESS token. Access tokens are generated using the RESOURCE app's manifest, not the client app's manifest. This is the single most common Entra group claims mistake.

**Warning signs:** `groups` claim present in ID tokens but missing from access tokens. Checking jwt.ms shows no groups in the access token.

**Prevention:** Set `groupMembershipClaims: "ApplicationGroup"` on the RESOURCE (MCP server) app registration manifest, not the client app. This restricts group claims to groups explicitly assigned to the app. Then assign the 4 Q-Hub groups (dev/devops/qa/ba) to the app via Enterprise Applications > Users and Groups.

**Phase:** Entra app registration phase. Must be configured before testing any group-based authorization.

**Confidence:** HIGH -- verified against Microsoft Learn docs on group claims

### CRITICAL: Audience (`aud`) claim mismatch

**What goes wrong:** Without configuring "Expose an API" with custom scopes, Entra issues tokens with `aud: "00000003-0000-0000-c000-000000000000"` (Microsoft Graph API audience). Your server validates `aud` against your app ID and rejects every token.

**Warning signs:** JWT validation fails with "audience validation failed". Token's `aud` is a GUID that doesn't match your app registration.

**Prevention:** In the MCP server app registration:
1. Go to "Expose an API"
2. Set Application ID URI (usually `api://{client-id}`)
3. Add scopes: `skills:read` and `skills:write`
4. Client apps request these scopes (e.g., `api://{client-id}/skills:read`)
Then validate `aud` matches `api://{client-id}` in JWT middleware.

**Phase:** Entra app registration phase. Must be done before any auth testing.

**Confidence:** HIGH -- verified against Microsoft identity platform docs

### HIGH: Multi-tenant vs single-tenant misconfiguration

**What goes wrong:** App registration created as multi-tenant when it should be single-tenant (or vice versa). Multi-tenant means ANY Azure AD tenant's users can get tokens. For a shared internal server, this is a security gap.

**Warning signs:** External users can authenticate to your MCP server. Or: internal users from partner tenants can't authenticate.

**Prevention:** For "Q-Hub MCPs" with internal group assignments, use single-tenant (`signInAudience: "AzureADMyOrg"`). This restricts to your organization's tenant only. Document this decision. If you later need B2B access, use Entra External ID or guest accounts, not multi-tenant.

**Phase:** Entra app registration phase. Set correctly at creation time.

**Confidence:** HIGH

### HIGH: Group overage claim -- too many groups

**What goes wrong:** If a user belongs to more than 150 groups (or 200 in SAML tokens), Entra replaces the `groups` claim with a `_claim_names`/`_claim_sources` pair pointing to a Graph API URL. Your middleware that reads `groups` directly from the token finds nothing.

**Warning signs:** Group-based auth works for most users but fails for admins/senior staff who are in many groups. Token contains `_claim_names` instead of `groups`.

**Prevention:** Use `groupMembershipClaims: "ApplicationGroup"` to limit claims to groups assigned to the app (you have only 4). This avoids the overage scenario entirely because you won't exceed the limit with 4 groups. Alternatively, use App Roles instead of group claims -- they never trigger overage. For this project with 4 groups, `ApplicationGroup` filtering is sufficient.

**Phase:** Entra app registration phase. Set `ApplicationGroup` from the start.

**Confidence:** HIGH -- well-documented Microsoft limitation

### MODERATE: `az` CLI login tokens have different audience

**What goes wrong:** The v1.1 requirement mentions "az CLI persisted login support." Tokens from `az login` have audience `https://management.azure.com` (Azure Management), not your API. They can't be used directly as MCP bearer tokens.

**Warning signs:** Developer logs in with `az login`, passes token to MCP server, gets 401.

**Prevention:** For developer convenience, use `az account get-access-token --resource api://{client-id}` to get a token scoped to your API. Or implement a thin wrapper that does this. The `az` CLI can target any resource, but developers must specify the right audience. Document this flow clearly.

**Phase:** Auth convenience phase (after core auth works). This is a DX feature, not core security.

**Confidence:** HIGH -- standard Azure CLI behavior

---

## Bicep / IaC Pitfalls

### CRITICAL: Bicep cannot create application secrets (passwordCredentials)

**What goes wrong:** You define the Entra app registration in Bicep using the Microsoft Graph Bicep extension, but can't set a client secret. Deployment succeeds but the app has no credentials.

**Warning signs:** Bicep deploys without error but the app registration has no secrets in the portal.

**Prevention:** This is a known limitation (GA as of July 2025). Use one of:
1. **Federated identity credentials** (FIC) for GitHub Actions -- no secret needed, Bicep supports this via `federatedIdentityCredentials`
2. **DeploymentScript** resource to call `az ad app credential reset` or Microsoft Graph API post-deploy
3. **Post-deploy az CLI step** in your CI/CD pipeline: `az ad app credential reset --id {appId}`
For the MCP server itself, prefer managed identity in Container Apps (no secret needed for the server). Client secrets are only needed if other apps authenticate as clients.

**Phase:** IaC phase. Design the credential strategy before writing Bicep.

**Confidence:** HIGH -- verified against Microsoft Graph Bicep limitations docs

### CRITICAL: Bicep cannot deploy role-assignable groups

**What goes wrong:** You define groups with `isAssignableToRole: true` in Bicep. Deployment fails even with correct permissions.

**Warning signs:** `DeploymentFailed` error when deploying groups with role-assignable flag.

**Prevention:** This is a documented Bicep limitation. The 4 Q-Hub groups (qhub-people-dev/devops/qa/ba) must be created via:
1. **DeploymentScript** calling Microsoft Graph: `POST /groups` with `isAssignableToRole: true`
2. **Post-deploy az CLI**: `az ad group create --display-name "..." --mail-nickname "..." --is-assignable-to-role true`
3. **Manual creation** (if groups already exist) -- just reference their object IDs in Bicep

**Phase:** IaC phase. These groups likely already exist in the organization -- verify before trying to create them.

**Confidence:** HIGH -- explicitly documented limitation

### HIGH: Bicep cannot assign groups to enterprise applications

**What goes wrong:** Even if groups exist, Bicep can't create app role assignments (assigning groups to the enterprise app). This requires Microsoft Graph `appRoleAssignedTo` API.

**Warning signs:** App registration deploys, groups exist, but no assignments link them.

**Prevention:** Post-deploy step required:
```bash
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/{sp-id}/appRoleAssignedTo" \
  --body '{"principalId":"{group-id}","resourceId":"{sp-id}","appRoleId":"{role-id}"}'
```
Or use a DeploymentScript in Bicep that calls this Graph endpoint. Accept that Entra group assignments are a "Bicep + post-deploy" two-step process.

**Phase:** IaC phase. Document which parts are Bicep and which are post-deploy scripts.

**Confidence:** HIGH

### HIGH: Replication delay after Entra resource creation

**What goes wrong:** Bicep creates an app registration and immediately tries to create a service principal or assign roles. The app hasn't replicated across Entra yet. Deployment fails with "resource not found."

**Warning signs:** Intermittent deployment failures. Works on retry.

**Prevention:** Add `dependsOn` between resources and use retry logic in DeploymentScripts. For Graph API calls, implement a retry with 30-second backoff. In Bicep, you can use `@batchSize(1)` on module deployments to force sequential execution.

**Phase:** IaC phase. Build retry logic into any DeploymentScript.

**Confidence:** MEDIUM -- well-known issue but timing varies

### MODERATE: No `what-if` support for Graph Bicep resources

**What goes wrong:** `az deployment group what-if` doesn't show changes to Entra resources. You can't preview what Bicep will do to app registrations before deploying.

**Warning signs:** Developers skip the what-if step because it shows no Graph changes, then get surprised.

**Prevention:** Accept this limitation. Use `az ad app show` and `az ad sp show` before and after deployments to manually diff. Consider keeping a separate deployment for Entra resources vs Azure resources so you can deploy/verify them independently.

**Phase:** IaC phase. Document as a known limitation in deployment runbook.

**Confidence:** HIGH -- documented Bicep limitation

### MODERATE: Bicep deployment identity needs high Entra permissions

**What goes wrong:** The service principal running Bicep deployments needs `Application.ReadWrite.All` or `AppRoleAssignment.ReadWrite.All` Graph permissions. These are admin-consent-required permissions.

**Warning signs:** Deployment fails with 403/Forbidden on Graph operations.

**Prevention:** Pre-grant the deployment identity (GitHub Actions service principal) the needed Graph permissions with admin consent. Use the principle of least privilege -- only grant `Application.ReadWrite.OwnedBy` if the deployment identity owns the app registration.

**Phase:** IaC phase. Pre-requisite before first Bicep deployment.

**Confidence:** HIGH

---

## CI/CD Pitfalls

### CRITICAL: Using `latest` tag for container images

**What goes wrong:** Every push tags the image as `latest`. Container Apps may not pull a new image if the tag hasn't changed (cached). Or worse, a rollback deploys an unknown version because `latest` was overwritten.

**Warning signs:** Deploy succeeds but Container Apps serves the old code. Rollback deploys wrong version.

**Prevention:** Tag images with the Git commit SHA: `ghcr.io/{repo}:${GITHUB_SHA}`. Also tag with semver for releases. Never rely on `latest` as the deployment tag. The Container Apps deploy action will create a new revision only if the image reference changes.

**Phase:** CI/CD phase. Set up in the very first workflow file.

**Confidence:** HIGH -- standard container best practice confirmed by Azure docs

### HIGH: Secret sprawl -- storing Azure credentials as GitHub secrets

**What goes wrong:** Using `AZURE_CREDENTIALS` secret with a service principal JSON blob. The secret contains client_id + client_secret + tenant_id + subscription_id. Secrets rotate, expire, or leak.

**Warning signs:** `AZURE_CREDENTIALS` secret in GitHub with a full JSON service principal. No secret rotation policy.

**Prevention:** Use OpenID Connect (OIDC) federated credentials instead:
1. Create federated identity credential on the Entra app registration pointing to your GitHub repo
2. Use `azure/login@v2` with OIDC: `client-id`, `tenant-id`, `subscription-id` as separate secrets (no client_secret)
3. Bicep supports `federatedIdentityCredentials` natively
This eliminates secret rotation entirely. The GitHub Actions OIDC token is short-lived and auto-rotated.

**Phase:** CI/CD phase. Set up OIDC from the start, never create a client secret for GitHub Actions.

**Confidence:** HIGH -- Microsoft-recommended approach

### HIGH: Environment variable injection exposing secrets in container

**What goes wrong:** Secrets (API keys for embedding providers, Qdrant connection strings) are passed as plain environment variables in the Container Apps definition. They appear in `az containerapp show` output and deployment logs.

**Warning signs:** `az containerapp show` reveals API keys in `environmentVariables` section.

**Prevention:** Use Container Apps secrets or Azure Key Vault references:
```bicep
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  properties: {
    configuration: {
      secrets: [
        { name: 'openai-api-key', value: openAiKey }  // stored as secret
      ]
    }
    template: {
      containers: [{
        env: [
          { name: 'OPENAI_API_KEY', secretRef: 'openai-api-key' }
        ]
      }]
    }
  }
}
```
Even better: use Key Vault references so secrets aren't in Bicep parameters at all.

**Phase:** CI/CD + IaC phase. Design secret injection pattern before first deployment.

**Confidence:** HIGH

### MODERATE: GitHub Actions workflow doesn't build/test before deploying

**What goes wrong:** Workflow pushes a broken image because tests weren't run, or tests ran against a different build than what was deployed.

**Warning signs:** Separate "test" and "deploy" jobs build independently. Tests pass on one build, deploy uses another.

**Prevention:** Single pipeline: build -> test -> push -> deploy. Use Docker multi-stage build so the test stage uses the same image as the deploy stage. The `dotnet test` step should run inside the container build, not separately. Gate deployment on test success.

**Phase:** CI/CD phase. Design the workflow structure before implementation.

**Confidence:** HIGH

### MODERATE: Container Apps revision management -- old revisions accumulate

**What goes wrong:** Every deployment creates a new revision. Old revisions consume resources and clutter the portal. Default traffic splitting may route to old revisions.

**Warning signs:** Dozens of inactive revisions. Unexpected traffic to old versions.

**Prevention:** Set `activeRevisionsMode: 'Single'` in the Container App template to automatically deactivate old revisions. Or implement a cleanup step in CI/CD that deactivates revisions older than N deployments.

**Phase:** CI/CD + IaC phase. Set in the initial Bicep template.

**Confidence:** MEDIUM -- depends on deployment frequency

---

## Integration Pitfalls

### CRITICAL: Breaking stdio when adding HTTP dependencies

**What goes wrong:** Adding `ModelContextProtocol.AspNetCore` package introduces ASP.NET Core dependencies. If these are unconditionally loaded or configured, they may alter the hosting behavior even in stdio mode. For example, `WebApplication.CreateBuilder()` automatically starts Kestrel.

**Warning signs:** `dotnet tool run QdrantSkillsMCP` (stdio mode) suddenly opens a port or logs Kestrel startup messages.

**Prevention:** Keep the `Program.cs` branching clean:
- stdio mode: `Host.CreateApplicationBuilder()` (no web host, no Kestrel) -- as it is today
- HTTP mode: `WebApplication.CreateBuilder()` (web host with Kestrel)
The package reference to `ModelContextProtocol.AspNetCore` is fine -- it's just a library. Only the builder choice determines whether Kestrel starts. Verify with integration tests that stdio mode works identically before and after adding HTTP support.

**Phase:** Transport phase. First integration test should be: "stdio mode still works."

**Confidence:** HIGH -- verified from current Program.cs architecture

### HIGH: NuGet tool packaging breaks with ASP.NET Core

**What goes wrong:** The project currently ships as a `dotnet tool` (NuGet). Adding ASP.NET Core dependencies may break tool packaging if `<PackAsTool>` conflicts with the web SDK, or the tool package becomes too large.

**Warning signs:** `dotnet pack` fails. Or package size balloons from ~5MB to ~50MB.

**Prevention:** The Infrastructure project already uses `<OutputType>Exe</OutputType>`. ASP.NET Core libraries are framework-included (not bundled in the package) when targeting `net10.0`. Verify the tool package size stays reasonable after adding the `ModelContextProtocol.AspNetCore` package. If it bloats, consider conditional compilation or a separate server package.

**Phase:** Transport phase. Check package size after adding the ASP.NET Core package.

**Confidence:** MEDIUM -- needs verification during implementation

### HIGH: Configuration schema conflicts between modes

**What goes wrong:** The v1.0 configuration system (layered config, `ConfigManager`) doesn't account for HTTP-specific settings (port, CORS origins, TLS cert). Adding them may break existing config validation or create confusing error messages when HTTP settings are provided in stdio mode.

**Warning signs:** Config validation errors about unknown keys. Or HTTP settings silently ignored in stdio mode.

**Prevention:** Extend the config schema cleanly:
- HTTP settings only validated when `--sse` or `--streamable-http` is active
- Add transport-specific config sections (e.g., `[http]` in config file)
- Existing stdio config keys remain unchanged
- `--config list` shows transport-appropriate settings based on current mode

**Phase:** Transport phase, after core HTTP transport works.

**Confidence:** MEDIUM -- depends on v1.0 config system flexibility

### MODERATE: Aspire AppHost doesn't support HTTP transport testing

**What goes wrong:** The v1.0 Aspire AppHost runs Qdrant for integration tests. For v1.1, you also need to test the HTTP transport, which means starting the MCP server as a web host inside the test. The Aspire testing pattern may not accommodate this cleanly.

**Warning signs:** Integration tests for HTTP transport require manual HTTP client setup outside the Aspire fixture.

**Prevention:** Create a separate test fixture for HTTP transport tests that starts a `WebApplication` in-process using `WebApplicationFactory<T>`. This is the standard ASP.NET Core integration testing pattern. Keep Aspire for Qdrant lifecycle management. The two can coexist.

**Phase:** Transport phase testing. Design the test architecture before writing HTTP tests.

**Confidence:** MEDIUM -- standard .NET testing pattern but needs verification with Aspire

### MODERATE: Health checks and readiness probes missing

**What goes wrong:** Container Apps has built-in health probes. Without them, the container is marked unhealthy and restarted, or traffic routes to an unready instance (Qdrant not connected yet).

**Warning signs:** Container restarts in a loop. Or requests fail during startup because Qdrant connection isn't established.

**Prevention:** Add health check endpoints in HTTP mode:
- `/health/live` -- process is running (liveness)
- `/health/ready` -- Qdrant is connected, embedding provider is available (readiness)
Configure these in the Container Apps Bicep template. In stdio mode, health checks don't apply.

**Phase:** Transport + IaC phase. Add health endpoints when setting up HTTP transport, configure probes in Bicep.

**Confidence:** HIGH -- Container Apps best practice

---

## Phase-Specific Warning Summary

| Phase Topic | Most Likely Pitfall | Severity | Mitigation |
|---|---|---|---|
| Entra App Registration | `aud` claim mismatch + group claims on wrong manifest | CRITICAL | Configure "Expose an API" first, set `groupMembershipClaims` on resource app |
| HTTP Transport | stdout contamination breaking stdio | CRITICAL | Mutually exclusive builder paths in Program.cs |
| MCP OAuth / Auth | Implementing against March 2025 spec instead of November 2025 | CRITICAL | Server is resource server only, implement Protected Resource Metadata |
| Bicep IaC | Cannot create secrets or group assignments | CRITICAL | Plan DeploymentScript or az CLI post-deploy steps from the start |
| CI/CD | Secret-based auth instead of OIDC | HIGH | Use federated identity credentials, never create client secrets for CI/CD |
| Integration | Existing stdio mode breaks | CRITICAL | Integration test: stdio still works after every transport change |

---

## Sources

- [MCP Authorization Spec 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization) -- HIGH confidence
- [MCP Authorization Spec 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization) -- HIGH confidence (older spec for comparison)
- [MCP C# SDK StreamableHttpServerTransport](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.StreamableHttpServerTransport.html) -- HIGH confidence
- [MCP C# SDK HttpServerTransportOptions](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html) -- HIGH confidence
- [Microsoft Graph Bicep Limitations](https://learn.microsoft.com/en-us/graph/templates/bicep/limitations) -- HIGH confidence
- [Azure Entra Group Claims Configuration](https://learn.microsoft.com/en-us/entra/identity/hybrid/connect/how-to-connect-fed-group-claims) -- HIGH confidence
- [Azure Entra Access Token Claims Reference](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference) -- HIGH confidence
- [Azure Container Apps GitHub Actions Deployment](https://learn.microsoft.com/en-us/azure/container-apps/github-actions) -- HIGH confidence
- [MCP OAuth Pitfalls Leading to Account Takeover](https://www.obsidiansecurity.com/blog/when-mcp-meets-oauth-common-pitfalls-leading-to-one-click-account-takeover) -- HIGH confidence
- [November 2025 MCP Auth Spec Changes](https://aaronparecki.com/2025/11/25/1/mcp-authorization-spec-update) -- MEDIUM confidence
- [Building Remote MCP Servers with .NET and Azure Container Apps](https://dev.to/willvelida/building-remote-mcp-servers-with-net-and-azure-container-apps-cc2) -- MEDIUM confidence
- [MCP Token Expiry Community Reports](https://community.atlassian.com/forums/Atlassian-Remote-MCP-Server/constant-authorization-timeouts/td-p/3177132) -- MEDIUM confidence
- [Bicep Graph Extension GA Announcement](https://techcommunity.microsoft.com/blog/azuregovernanceandmanagementblog/announcing-ga-of-bicep-templates-support-for-microsoft-entra-id-resources/4437163) -- HIGH confidence
- [Why MCP Deprecated SSE for Streamable HTTP](https://blog.fka.dev/blog/2025-06-06-why-mcp-deprecated-sse-and-go-with-streamable-http/) -- MEDIUM confidence
- [MCP C# SDK Streamable HTTP Discussion](https://github.com/modelcontextprotocol/csharp-sdk/discussions/549) -- MEDIUM confidence
