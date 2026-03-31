# Features Research -- v1.1 Shared Server

**Domain:** MCP server shared deployment with authentication, HTTP transports, and IaC
**Researched:** 2026-03-31
**MCP Spec Target:** 2025-03-26 (with awareness of 2025-06-18 changes)
**Overall Confidence:** HIGH

---

## MCP OAuth 2.0 Flow

The MCP 2025-03-26 spec defines OAuth 2.1-based authorization for HTTP transports. The MCP server can either act as its own authorization server or delegate to a third-party one (Azure Entra in our case). Key: the spec distinguishes between the MCP server as a "protected resource" and the authorization server as a separate concern.

**Important spec evolution note:** The 2025-06-18 spec added RFC 9728 (Protected Resource Metadata) as a MUST and removed fallback endpoint defaults. The 2025-03-26 spec we target does NOT require RFC 9728 -- it uses RFC 8414 (Authorization Server Metadata) with fallback defaults. However, implementing RFC 9728 now is forward-compatible and recommended.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| JWT Bearer token validation on every HTTP request | MCP spec MUST: "authorization MUST be included in every HTTP request" | Low | Standard ASP.NET Core `AddAuthentication().AddJwtBearer()` -- well-trodden path |
| 401 Unauthorized when no/invalid token | MCP spec MUST: servers respond with HTTP 401 when auth required | Low | Default ASP.NET Core behavior with JWT middleware |
| 403 Forbidden for insufficient scopes | MCP spec MUST: return 403 for invalid scopes | Low | ASP.NET Core authorization policies |
| Read/write scope enforcement | Project requirement: read vs write permissions | Med | Define custom scopes in Entra app registration (`Skills.Read`, `Skills.Write`), enforce via `[Authorize]` or policy on tool handlers |
| HTTPS-only for all auth endpoints | MCP spec MUST: all authorization endpoints served over HTTPS | Low | Container Apps provides TLS termination by default |
| PKCE support (server-side validation) | MCP spec REQUIRED for all clients; server must accept code_challenge | Low | Handled by Azure Entra -- we delegate auth server role |
| Authorization Server Metadata (RFC 8414) | MCP spec: servers SHOULD implement, clients MUST consume | Med | When delegating to Azure Entra, the `/.well-known/openid-configuration` endpoint on `login.microsoftonline.com` already serves this. MCP server needs to expose `/.well-known/oauth-authorization-server` pointing to Entra's metadata, OR rely on the client doing discovery from the Entra issuer directly |
| Token expiration enforcement | MCP spec MUST: validate tokens per OAuth 2.1 Section 5.2 | Low | Built into `JwtBearerHandler` -- validates `exp` claim automatically |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Protected Resource Metadata (RFC 9728) | Forward-compatible with MCP 2025-06-18 spec; clients auto-discover auth server | Med | Serve `/.well-known/oauth-protected-resource` returning `{ resource, authorization_servers, scopes_supported }`. Not required by 2025-03-26 spec but becomes MUST in 2025-06-18. Recommended to implement now. The C# SDK's `.AddMcp()` auth handler supports this. |
| Dynamic Client Registration (RFC 7591) | MCP spec SHOULD: allows unknown clients to register automatically | High | Azure Entra does NOT support RFC 7591 natively. Would require a custom registration proxy. Defer unless needed -- our clients are known (Claude Code, Copilot, etc.) and can use pre-registered client IDs. |
| Refresh token support | Seamless token renewal without re-auth | Low | Azure Entra provides refresh tokens by default with authorization code flow. Server just validates access tokens. |
| `MCP-Protocol-Version` header on metadata discovery | MCP spec SHOULD: allows version-aware responses | Low | Nice for future-proofing. Read the header and log it; respond with same metadata regardless for now. |
| Third-party authorization flow (Entra as auth server) | MCP spec MAY: delegates auth to external provider | Med | This IS our architecture. MCP server is the protected resource; Entra is the authorization server. The MCP spec explicitly supports this pattern. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| MCP server as its own OAuth authorization server | Massive complexity (token issuance, user database, PKCE validation, consent UI). Azure Entra already does this. | Delegate to Azure Entra. MCP server is the protected resource only -- it validates tokens, not issues them. |
| Dynamic Client Registration via custom proxy | Over-engineering for known client set. RFC 7591 support in front of Entra would be a custom service. | Pre-register known clients in Entra app registration. Provide client ID in setup wizard output. |
| Custom token format (non-JWT) | Loses interop with standard tooling, Entra issues JWTs. | Use standard Entra-issued JWTs. Validate with `Microsoft.Identity.Web`. |
| Per-request Graph API calls for authorization | Latency disaster. Each MCP tool call would need a Graph roundtrip. | Use JWT claims (groups/roles) for authorization. Cache if Graph needed for overage. |
| Storing tokens server-side per session | Unnecessary state. Bearer token model is stateless. | Validate tokens on every request. Stateless is correct for resource servers. |

---

## HTTP Transports (SSE + Streamable HTTP)

The MCP 2025-03-26 spec defines Streamable HTTP as THE transport for remote servers, replacing the older HTTP+SSE from 2024-11-05. The C# SDK (`ModelContextProtocol.AspNetCore`) provides `WithHttpTransport()` and `MapMcp()` which implement Streamable HTTP natively.

### SSE (Legacy HTTP+SSE from 2024-11-05)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `--sse` CLI flag to start in SSE mode | Backward compat for older MCP clients | Med | The C# SDK previously supported SSE via separate endpoints (GET for SSE stream, POST for messages). The current SDK version uses Streamable HTTP which subsumes SSE. The `--sse` flag should map to the same Streamable HTTP server, since Streamable HTTP already uses SSE for streaming responses. |
| Dual-endpoint backward compatibility | MCP spec recommends servers support older clients | Med | Spec says: "Continue to host both the SSE and POST endpoints of the old transport, alongside the new MCP endpoint." Consider whether any target clients still use old SSE-only. If not, skip. |

**Recommendation:** Do NOT implement the old SSE transport separately. Streamable HTTP already uses SSE for streaming. The `--sse` flag can be an alias for `--streamable-http` or be dropped entirely. Check if any target MCP clients (Claude Code, Copilot) still require the old SSE transport. If so, implement backward compat shim per spec guidance.

### Streamable HTTP (MCP 2025-03-26)

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Single MCP endpoint supporting POST and GET | MCP spec MUST | Low | `app.MapMcp("/mcp")` in the C# SDK handles this. One endpoint, two methods. |
| POST accepts JSON-RPC requests, returns JSON or SSE stream | MCP spec MUST: server returns `application/json` for simple responses, `text/event-stream` for streaming | Low | Handled by `StreamableHttpServerTransport` in the C# SDK |
| GET opens SSE stream for server-initiated messages | MCP spec MUST: return SSE stream or 405 | Low | SDK handles this. Server can push notifications/requests to client via SSE. |
| `Mcp-Session-Id` header for session management | MCP spec MAY assign, clients MUST include if assigned | Med | SDK's `HttpServerTransportOptions` supports session management. Enable for stateful mode. |
| 202 Accepted for notifications/responses (no body) | MCP spec MUST | Low | SDK handles this automatically |
| `Accept` header with `application/json` and `text/event-stream` | MCP spec MUST on client side; server should respect | Low | SDK handles content negotiation |
| Origin header validation | MCP spec MUST: prevent DNS rebinding attacks | Low | Add CORS middleware or manual Origin check. Critical for security. |
| `--streamable-http` and `--url {URL}` CLI flags | Project requirement: configure transport mode and bind URL | Med | Parse CLI args, configure Kestrel to listen on specified URL. Default to `http://localhost:3001/mcp` or similar. |
| DELETE for session termination | MCP spec: clients SHOULD send DELETE, servers MAY accept or return 405 | Low | SDK supports this. Implement to clean up session state. |
| Binding to configurable host/port | Operational necessity for deployment | Low | Use `--url` parameter to set Kestrel's listen address |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| SSE event IDs for resumability | MCP spec MAY: enables reconnection without message loss | Med | SDK supports event store (`HttpServerTransportOptions.EventStore`). Useful for unreliable networks. Defer to later if not needed for initial deployment. |
| Stateless mode (no session ID) | MCP spec allows: enables horizontal scaling behind load balancer | Med | Don't assign `Mcp-Session-Id` -- each request is independent. Requires session tracking to use request-scoped or external state (Redis/Qdrant). Conflicts with existing in-memory session tracking. |
| Batch JSON-RPC support | MCP spec allows: multiple requests in one POST | Low | SDK handles this. Useful for client optimization. |
| Graceful shutdown with session cleanup | Production readiness | Med | On SIGTERM, close active SSE streams, return 404 for session IDs. |

#### Stateless vs Stateful Decision

**Recommend stateful mode** (with `Mcp-Session-Id`) for v1.1 because:
1. Existing session tracking (skills already loaded per session) is in-memory and assumes statefulness.
2. Single-instance Container Apps deployment (1-2 replicas in single revision mode) does not need stateless horizontal scaling.
3. Stateful is simpler -- the SDK assigns a session ID and routes requests to the correct session state.
4. If horizontal scaling is needed later, move session state to Redis or Qdrant and switch to stateless.

---

## Azure Entra Authorization

The project uses an Entra app registration ("Q-Hub MCPs") with group-based access control. Four groups: `qhub-people-dev`, `qhub-people-devops`, `qhub-people-qa`, `qhub-people-ba`.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Entra app registration with API scopes | Foundation for OAuth flow | Low | Register app in Entra portal. Expose API with `Skills.Read` and `Skills.Write` scopes. Set Application ID URI. |
| JWT validation with `Microsoft.Identity.Web` | Standard .NET Entra integration | Low | `AddMicrosoftIdentityWebApiAuthentication(Configuration)` validates issuer, audience, signing keys automatically. |
| Group-based authorization from JWT `groups` claim | Project requirement: 4 groups control access | Med | Enable "groups" optional claim in token configuration. With only 4 groups, well under the 200 overage limit. Map group object IDs to authorization policies. |
| Scope-based tool access (read vs write) | Project requirement: separate read/write permissions | Med | `search-skills`, `load-skill`, `list-skills` require `Skills.Read`. `add-skill`, `update-skill`, `archive-skill`, `delete-skill` require `Skills.Write`. Enforce via `[Authorize(Policy = "ReadSkills")]` and `[Authorize(Policy = "WriteSkills")]` on tool handlers, or via the C# SDK's authorization filter support. |
| Token audience validation | Security: tokens must be intended for this API | Low | `Microsoft.Identity.Web` validates `aud` claim matches the app's Application ID URI. |
| Issuer validation | Security: tokens must come from expected Entra tenant | Low | Built into `JwtBearerHandler`. Validate against `https://login.microsoftonline.com/{tenant-id}/v2.0`. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| App roles instead of groups claim | Cleaner authorization model, avoids group ID mapping, no overage risk | Med | Define app roles (`SkillReader`, `SkillWriter`) in app manifest. Assign groups to roles. Token contains `roles` claim with friendly names instead of GUIDs. Slightly more setup but much cleaner code. **Recommended over raw groups claim.** |
| Group-to-scope mapping documentation | Developer clarity | Low | Document which groups get which scopes. Include in setup wizard output. |
| Conditional access policies | Enterprise security: require MFA, compliant devices, etc. | Low (config) | Configured entirely in Entra portal. No code changes. Nice for enterprise deployments. |
| Multi-tenant support | Allow other Entra tenants to use the server | High | Requires multi-tenant app registration, consent flow, tenant validation. Out of scope per PROJECT.md but note for future. |

### Groups vs App Roles Decision

**Recommend app roles** because:
1. Only 4 groups -- mapping them to app roles is trivial one-time setup.
2. `roles` claim contains friendly names (`SkillReader`) instead of GUIDs (`a1b2c3d4-...`).
3. No risk of the groups overage problem (200 group JWT limit). App roles have no such limit for the app's own roles.
4. Authorization code reads `User.IsInRole("SkillReader")` instead of checking GUID strings.
5. If groups are also wanted (e.g., for logging which team a user belongs to), they can still be included as a secondary claim.

---

## az CLI Token Reuse

Developers who have run `az login` should be able to use the MCP server without a separate browser-based OAuth dance. This is a developer convenience feature for local use.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `AzureCliCredential` for local dev token acquisition | Developer convenience: reuse existing `az login` session | Med | `Azure.Identity` package provides `AzureCliCredential`. It spawns `az account get-access-token --resource {app-id-uri}` under the hood. The resulting token is a valid Entra JWT for the app's audience. |
| Token passed as Bearer header to MCP server | Standard OAuth flow | Low | Client (or a wrapper script) gets token via `az account get-access-token`, passes as `Authorization: Bearer {token}` on MCP HTTP requests. |
| `--az-login` or similar CLI flag | Signal to use az CLI credential flow instead of browser OAuth | Low | When specified, the server's built-in client (for `--console` mode) or setup wizard acquires token via `AzureCliCredential`. |
| Fallback chain: AzureCliCredential -> InteractiveBrowser | Developer ergonomics | Med | Use `DefaultAzureCredential` or `ChainedTokenCredential` with `AzureCliCredential` first, falling back to interactive browser if `az login` not available. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Server-side `az` CLI invocation for token validation | `az` CLI is a client-side tool. Server validates JWTs, not CLI tokens. | Server always validates JWTs from `Authorization` header. `az` CLI is client-side only. |
| Custom token exchange (az token -> MCP token) | Unnecessary complexity. Entra tokens work directly. | `az account get-access-token` returns a standard Entra JWT. Pass it directly to the MCP server. No exchange needed. |
| Storing az CLI credentials on the server | Security risk. Credentials belong on developer machines only. | az CLI token reuse is a client-side concern. Server validates whatever JWT arrives. |

### How It Works in Practice

1. Developer runs `az login` (one-time, persisted).
2. MCP client (or wrapper) runs `az account get-access-token --resource api://{app-id-uri} --query accessToken -o tsv`.
3. Token is passed as `Authorization: Bearer {token}` on every HTTP request to the MCP server.
4. MCP server validates the JWT normally -- it doesn't know or care that it came from `az` CLI vs browser flow.
5. For `--console` mode, the server itself can use `AzureCliCredential` to get a token for self-testing.

**Key insight:** This is not a server-side feature. It's a client-side convenience. The server's auth validation is identical regardless of how the token was obtained. The work is in documenting the flow and optionally providing helper scripts or setup wizard integration.

---

## Container Apps Deployment

Azure Container Apps provides a managed container hosting environment with built-in HTTPS, scaling, and revision management.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Bicep template for Container Apps environment + app | IaC requirement | Med | Define `Microsoft.App/managedEnvironments` and `Microsoft.App/containerApps` resources. Single revision mode (`activeRevisionsMode: 'Single'`). |
| HTTPS ingress with TLS termination | Security: all auth endpoints must be HTTPS | Low | Container Apps provides automatic TLS. Set `ingress.external: true`, `ingress.allowInsecure: false`. |
| Health probes (liveness + readiness) | Production readiness: Container Apps uses probes for zero-downtime | Med | Add `/health` or `/healthz` endpoint to the MCP server. Configure in Bicep: `probes: [{ type: 'Readiness', httpGet: { path: '/health', port: 8080 } }]`. |
| Secrets via Container Apps secrets (not env vars) | Security: API keys, connection strings must not be in Bicep files | Med | Define secrets in Container Apps resource. Reference as `secretRef` in env vars. Pass actual values from CI/CD pipeline (GitHub Secrets). |
| Environment variables for non-sensitive config | Operational: Qdrant URL, embedding provider, collection name | Low | Set in Bicep `env` array on container. |
| Container image from GitHub Container Registry (ghcr.io) | CI/CD: GitHub Actions builds and pushes image | Med | Bicep references `ghcr.io/{org}/{repo}:{tag}`. Registry credentials as Container Apps secret. |
| Single revision mode for zero-downtime | Default mode: new revision takes traffic when ready | Low | `activeRevisionsMode: 'Single'`. Previous revision deprovisions after new one is healthy. |
| Managed Identity (user-assigned) | Avoid storing credentials for Azure services | Med | Create identity in Bicep, assign to Container App. Use for Key Vault access, Entra token acquisition. |
| GitHub Actions workflow: build, push, deploy | CI/CD pipeline | Med | Standard workflow: checkout -> docker build -> push to ghcr.io -> `az containerapp update --image`. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Key Vault references for secrets | Centralized secret management with audit logging | Med | Instead of inline secrets, reference Key Vault secrets: `secretRef` with `keyVaultUrl`. Requires managed identity with Key Vault access policy. Better for production but adds a resource. |
| Bicep modules (modular IaC) | Maintainability: separate modules for environment, app, identity, Entra | Med | Split Bicep into `main.bicep` calling modules. Cleaner than one massive file. |
| Auto-scaling rules (HTTP-based) | Handle variable load | Low | `scale: { minReplicas: 1, maxReplicas: 3, rules: [{ name: 'http', http: { metadata: { concurrentRequests: '50' } } }] }`. |
| Staging slot via multiple revision mode | Blue/green deployment with traffic splitting | High | Requires `activeRevisionsMode: 'Multiple'`, revision labels, traffic weight management. Over-engineering for v1.1. Stick with single revision mode. |
| App Service as alternative deployment target | Bicep flexibility per PROJECT.md requirement | Med | Define an alternative `Microsoft.Web/sites` Bicep template. Same container image, different hosting. Useful if Container Apps has limitations. |
| Log Analytics workspace integration | Observability: centralized logging | Low | Container Apps environment requires a Log Analytics workspace. Include in Bicep. Query logs via Azure portal or CLI. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Secrets in Bicep files | Security: secrets committed to git | Pass via `--parameters` from GitHub Actions using `${{ secrets.X }}` |
| Multiple revision mode for v1.1 | Complexity: traffic splitting, revision management | Single revision mode. Zero-downtime is built-in. |
| Custom domain + certificate for v1.1 | Scope creep. Default `*.azurecontainerapps.io` domain works fine initially. | Use default Container Apps domain. Add custom domain later if needed. |
| Azure Front Door / API Management | Over-engineering for a team-internal MCP server | Direct Container Apps ingress. Add gateway layer only when needed. |

---

## Feature Dependencies

```
Streamable HTTP transport  -->  MCP OAuth 2.0 (auth sits on top of HTTP transport)
                           -->  Container Apps deployment (needs HTTP to be useful remotely)

Entra app registration     -->  MCP OAuth 2.0 (provides the authorization server)
                           -->  Azure Entra authorization (groups/roles defined in registration)

Container Apps Bicep       -->  GitHub Actions CI/CD (deploys the Bicep template)
                           -->  Entra app registration (Bicep can create the registration)

az CLI token reuse         -->  Entra app registration (needs app ID URI for --resource)
                           -->  Streamable HTTP transport (tokens sent via HTTP Bearer)

Existing stdio transport   -->  NO auth (MCP spec: stdio SHOULD NOT use this auth spec)
```

## MVP Recommendation

**Prioritize (must ship):**
1. Streamable HTTP transport (`--streamable-http` / `--url`) -- foundation for everything else
2. JWT Bearer validation with `Microsoft.Identity.Web` -- table stakes for shared server
3. Entra app registration with app roles + scopes -- authorization backbone
4. Scope enforcement on MCP tool handlers (read vs write) -- project requirement
5. Container Apps Bicep + GitHub Actions CI/CD -- deployment pipeline
6. Health endpoint for Container Apps probes

**Include but lower priority:**
7. Protected Resource Metadata endpoint (`/.well-known/oauth-protected-resource`) -- forward-compat
8. az CLI token reuse documentation + helper in setup wizard
9. App Service alternative Bicep template

**Defer:**
- Dynamic Client Registration (RFC 7591) -- known clients, pre-register in Entra
- SSE backward compat shim -- verify if any target clients need it first
- Stateless horizontal scaling -- single instance sufficient for team use
- SSE event ID resumability -- network is reliable in Azure-to-Azure calls
- Multi-tenant support -- out of scope per PROJECT.md
- Key Vault references -- inline Container Apps secrets sufficient for v1.1
- Custom domain/TLS -- default Container Apps domain works

## Sources

### MCP Specification (PRIMARY -- HIGH confidence)
- [MCP 2025-03-26 Authorization Spec](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)
- [MCP 2025-03-26 Transports Spec](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
- [MCP Draft Authorization (2025-06-18+)](https://modelcontextprotocol.io/specification/draft/basic/authorization)

### C# SDK (HIGH confidence)
- [MCP C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [StreamableHttpServerTransport API](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.StreamableHttpServerTransport.html)
- [HttpServerTransportOptions API](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html)
- [SSE to Streamable HTTP migration discussion](https://github.com/modelcontextprotocol/csharp-sdk/discussions/790)

### Azure Entra (HIGH confidence)
- [Configure group claims and app roles](https://learn.microsoft.com/en-us/security/zero-trust/develop/configure-tokens-group-claims-app-roles)
- [Groups claim overage handling](https://learn.microsoft.com/en-us/troubleshoot/entra/entra-id/app-integration/get-signed-in-users-groups-in-access-token)
- [Access token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference)

### Azure Container Apps (HIGH confidence)
- [Container Apps secrets management](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets)
- [Container Apps revisions and zero-downtime](https://learn.microsoft.com/en-us/azure/container-apps/revisions)
- [Container Apps health probes](https://learn.microsoft.com/en-us/azure/container-apps/health-probes)
- [Building Remote MCP Servers with .NET and Azure Container Apps](https://dev.to/willvelida/building-remote-mcp-servers-with-net-and-azure-container-apps-cc2)

### Community / Analysis (MEDIUM confidence)
- [MCP Auth spec critique](https://blog.christianposta.com/the-updated-mcp-oauth-spec-is-a-mess/)
- [Auth0 MCP spec analysis](https://auth0.com/blog/mcp-specs-update-all-about-auth/)
- [Aaron Parecki on MCP OAuth](https://aaronparecki.com/2025/04/03/15/oauth-for-model-context-protocol)
- [Secure MCP server with Entra ID](https://damienbod.com/2025/09/23/implement-a-secure-mcp-server-using-oauth-and-entra-id/)
- [MCP auth with Entra in .NET](https://nikiforovall.blog/dotnet/2025/09/02/mcp-auth.html)
