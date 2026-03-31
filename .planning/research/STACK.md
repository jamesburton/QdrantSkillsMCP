# Stack Research -- v1.1 Shared Server

**Researched:** 2026-03-31
**Overall confidence:** HIGH (all packages verified against NuGet and official SDK docs)

> This file covers ONLY the new stack additions for v1.1. The v1.0 stack (ModelContextProtocol, Qdrant.Client, Aspire, XUnit v3, etc.) is validated and unchanged unless noted.

---

## HTTP Transports

### Package Changes

| Package | Current | Target | Action | Why |
|---------|---------|--------|--------|-----|
| `ModelContextProtocol` | 1.1.0 | 1.2.0 | **Upgrade** | Latest stable (published 2026-03-27). v1.2.0 disables legacy SSE by default -- correct for new projects, but we need to opt back in for backward compat. No breaking compile-time changes. |
| `ModelContextProtocol.AspNetCore` | -- | 1.2.0 | **Add** | Required for HTTP transports. Provides `WithHttpTransport()` and `MapMcp()`. Depends on `ModelContextProtocol >= 1.2.0`. Targets .NET 8+ (compatible with .NET 10). |

### How It Works

`ModelContextProtocol.AspNetCore` 1.2.0 provides a single `MapMcp()` call that registers both transport endpoints:

**Streamable HTTP (modern, MCP 2025-03-26 spec):**
- `POST /` -- send messages, receive JSON or SSE stream response
- `GET /` -- open long-lived SSE stream for unsolicited server messages
- `DELETE /` -- terminate session
- Stateful via `Mcp-Session-Id` header

**Legacy SSE (backward compat, MCP 2024-11-05 spec):**
- `GET /sse` -- open SSE connection
- `POST /message` -- send messages
- Must opt in: `EnableLegacySse = true` (defaults to `false` in v1.2.0, marked `[Obsolete]` but functional)

### Setup Pattern

```csharp
// DI registration
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.EnableLegacySse = true;  // Claude Desktop and older clients use SSE
        // options.IdleTimeout = TimeSpan.FromHours(2);  // default
        // options.Stateless = false;  // default, use stateful sessions
    })
    .WithToolsFromAssembly();

// Endpoint routing
app.MapMcp();
```

### Critical: ASP.NET Core Shared Framework

The Infrastructure project uses `Microsoft.NET.Sdk` (not `.Web`) and ships as a NuGet tool (`PackAsTool`). To use `ModelContextProtocol.AspNetCore` without changing to `Sdk.Web`:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

This brings in ASP.NET Core types (Kestrel, routing, etc.) without changing the project SDK. The tool packaging (`PackAsTool`) is preserved. At startup, select the hosting model based on CLI flags:

- `--stdio` (default): Use `Host.CreateDefaultBuilder()` + `WithStdioServerTransport()` (existing code)
- `--sse` or `--streamable-http` or `--url {url}`: Use `WebApplication.CreateBuilder()` + `WithHttpTransport()` + `MapMcp()`

### Breaking Change Alert

**ModelContextProtocol 1.1.0 -> 1.2.0**: `EnableLegacySse` defaults to `false`. If Claude Desktop or other SSE-only clients connect to the HTTP endpoint, they will get 404 on `/sse` unless `EnableLegacySse = true` is set. This is a behavioral change, not a compile-time break.

### Key Transport Options (HttpServerTransportOptions)

| Property | Default | Use Case |
|----------|---------|----------|
| `EnableLegacySse` | `false` | Set `true` for backward compat with SSE-only clients |
| `Stateless` | `false` | Keep `false` -- stateful sessions match MCP session model |
| `IdleTimeout` | 2 hours | Reasonable default, no change needed |
| `MaxIdleSessionCount` | 10,000 | Reasonable for shared server, no change needed |
| `EventStreamStore` | null (in-memory) | Only set for multi-instance resumability. Skip for v1.1. |

---

## Auth / Identity

### Packages to Add

| Package | Version | Purpose | Why This One |
|---------|---------|---------|-------------|
| `Microsoft.Identity.Web` | 4.6.0 | JWT validation + Entra integration | Official Microsoft package. Single call: `AddMicrosoftIdentityWebApi()`. Handles JWKS rotation, issuer validation, audience checks, multi-tenant support. Published 2026-03-20. |

`Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.5) is included in the ASP.NET Core shared framework -- no explicit package reference needed when using `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

### What NOT to Add for Auth

| Package | Why NOT |
|---------|---------|
| `Microsoft.Identity.Client` (MSAL.NET 4.83.1) | Client-side token acquisition. The MCP server is the resource server -- it validates tokens, does not acquire them. |
| `Azure.Identity` | For calling Azure APIs (Key Vault, Storage, etc.). Not needed for JWT validation. Add later only if the server itself calls Azure services. |
| `Duende.IdentityServer` or any custom OAuth server | Entra IS the authorization server. Do not build or host your own. |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | For interactive web app login (cookie-based). MCP server uses JWT bearer auth only. |

### JWT Validation Setup

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// appsettings.json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "{tenant-id}",
    "ClientId": "api://{client-id}",
    "Audience": "api://{client-id}"
  }
}
```

### MCP OAuth 2.0 Protocol Compliance

The MCP 2025-03-26 spec requires two well-known endpoints:

1. **Protected Resource Metadata** (RFC 9728): Server MUST serve `/.well-known/oauth-protected-resource`:
   ```json
   {
     "resource": "https://your-server-url",
     "authorization_servers": ["https://login.microsoftonline.com/{tenantId}/v2.0"],
     "scopes_supported": ["skills.read", "skills.write"]
   }
   ```

2. **Authorization Server Metadata** (RFC 8414): Entra already publishes this at `https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration`. No server-side work needed.

The server needs a simple middleware or endpoint to serve the protected resource metadata document. This is a static JSON response -- no package needed.

### Scopes Design

| Scope | Entra Value | Grants |
|-------|------------|--------|
| `skills.read` | `api://{client-id}/skills.read` | `search-skills`, `load-skill` |
| `skills.write` | `api://{client-id}/skills.write` | `add-skill`, `update-skill`, `archive-skill`, `delete-skill` |

Implement via `[RequiredScope("skills.read")]` attribute or policy-based authorization checking `scp` or `roles` claims.

### az CLI Token Reuse (Developer Convenience)

For developers who have run `az login`:

```bash
# Get a token for the MCP server's Entra app
az account get-access-token --resource api://{client-id} --query accessToken -o tsv
```

This produces a standard Entra JWT. The MCP client (or a wrapper script) passes it as `Authorization: Bearer {token}`. The server validates it identically to any other Entra-issued token. **No special server-side code needed** -- this is purely a client-side convenience pattern.

---

## IaC (Bicep)

### Resource Types

| Resource | Bicep Type | API Version | Purpose |
|----------|-----------|-------------|---------|
| Entra App Registration | `Microsoft.Graph/applications@v1.0` | v1.0 (Graph Bicep) | App reg with OAuth2 permission scopes |
| Entra Service Principal | `Microsoft.Graph/servicePrincipals@v1.0` | v1.0 (Graph Bicep) | Enterprise app for the registration |
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces@2023-09-01` | 2023-09-01 | Required by Container Apps Environment |
| Container Registry | `Microsoft.ContainerRegistry/registries@2023-07-01` | 2023-07-01 | ACR for Docker images |
| Container Apps Environment | `Microsoft.App/managedEnvironments@2024-03-01` | 2024-03-01 | Hosting environment |
| Container App | `Microsoft.App/containerApps@2024-03-01` | 2024-03-01 | QdrantSkillsMCP server instance |
| App Service Plan | `Microsoft.Web/serverfarms@2023-12-01` | 2023-12-01 | Alternative hosting |
| App Service | `Microsoft.Web/sites@2023-12-01` | 2023-12-01 | Alternative hosting |

### Bicep Graph Extension (Entra App Registration)

The Microsoft Graph Bicep extension went GA July 2025. Required for declarative Entra app registrations.

```bicep
extension microsoftGraph

resource app 'Microsoft.Graph/applications@v1.0' = {
  displayName: 'Q-Hub MCPs'
  uniqueName: 'q-hub-mcps'
  api: {
    oauth2PermissionScopes: [
      {
        id: guid('q-hub-mcps-skills-read')
        value: 'skills.read'
        adminConsentDisplayName: 'Read skills'
        adminConsentDescription: 'Search and load skills'
        type: 'User'
        isEnabled: true
      }
      {
        id: guid('q-hub-mcps-skills-write')
        value: 'skills.write'
        adminConsentDisplayName: 'Write skills'
        adminConsentDescription: 'Add, update, archive, and delete skills'
        type: 'Admin'
        isEnabled: true
      }
    ]
  }
}

resource sp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: app.appId
}
```

**Confidence note:** MEDIUM for Graph Bicep. It is GA but newer than the Azure resource types. Group assignment (`appRoleAssignments` for qhub-people-dev/devops/qa/ba) may require additional `Microsoft.Graph/appRoleAssignedTo` resources or post-deployment scripts. Test this early.

### Recommended File Structure

```
infra/
  main.bicep              # Orchestrator module
  main.bicepparam         # Environment parameters
  modules/
    entra-app.bicep       # App registration + service principal + scopes
    container-registry.bicep  # ACR
    container-apps.bicep  # Environment + container app
    app-service.bicep     # Alternative deployment target
    monitoring.bicep      # Log Analytics workspace
```

### Container App Configuration

Key settings for the Container App resource:

```bicep
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  properties: {
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'  // Container Apps handles TLS termination
      }
      registries: [{ server: acr.properties.loginServer, identity: managedIdentity.id }]
    }
    template: {
      containers: [{
        name: 'qdrant-skills-mcp'
        image: '${acr.properties.loginServer}/qdrant-skills-mcp:latest'
        resources: { cpu: json('0.5'), memory: '1Gi' }
        env: [
          { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
          { name: 'MCP_TRANSPORT', value: 'streamable-http' }
        ]
      }]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}
```

---

## CI/CD (GitHub Actions)

### Actions to Use

| Action | Version | Purpose |
|--------|---------|---------|
| `actions/checkout@v4` | v4 | Clone repository |
| `actions/setup-dotnet@v4` | v4 | Install .NET 10 SDK |
| `docker/login-action@v3` | v3 | Authenticate to ACR |
| `docker/build-push-action@v6` | v6 | Build and push Docker image |
| `azure/login@v2` | v2 | Authenticate to Azure via OIDC |
| `azure/container-apps-deploy-action@v1` | v1 | Deploy revision to Container Apps |

### Authentication: OIDC Federated Credentials (NOT Secrets)

Do NOT use service principal client secrets stored as GitHub Secrets. Use OIDC federation:

1. Create a User-Assigned Managed Identity in Azure
2. Add federated credential pointing to `repo:jamesburton/QdrantSkillsMCP:ref:refs/heads/main` (and tag pattern)
3. `azure/login@v2` authenticates with `client-id`, `tenant-id`, `subscription-id` -- no secret rotation

```yaml
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

### Recommended Workflow Structure

```
.github/workflows/
  ci.yml        # Build + test on every PR and push to main
  deploy.yml    # Build image + push to ACR + deploy to Container Apps
```

**ci.yml** triggers: `push` to `main`, `pull_request` to `main`

```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --logger trx
```

**deploy.yml** triggers: `push` tags `v*`, or `workflow_dispatch`

```yaml
jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - uses: docker/login-action@v3
        with:
          registry: ${{ vars.ACR_LOGIN_SERVER }}
          username: ${{ secrets.AZURE_CLIENT_ID }}
          password: ''  # OIDC token from azure/login
      - uses: docker/build-push-action@v6
        with:
          push: true
          tags: ${{ vars.ACR_LOGIN_SERVER }}/qdrant-skills-mcp:${{ github.ref_name }}
      - uses: azure/container-apps-deploy-action@v1
        with:
          containerAppName: qdrant-skills-mcp
          resourceGroup: ${{ vars.RESOURCE_GROUP }}
          imageToDeploy: ${{ vars.ACR_LOGIN_SERVER }}/qdrant-skills-mcp:${{ github.ref_name }}
```

### Integration Tests in CI

The existing XUnit v3 + Aspire integration tests require a running Qdrant instance. Options for CI:

1. **Aspire test infrastructure** -- `DistributedApplicationTestingBuilder` starts containers. Requires Docker-in-Docker or a service container on the runner. This already works locally.
2. **Skip integration tests in CI** initially, run only unit tests. Add integration test job later with Docker support.

Recommend option 2 for v1.1 -- focus on the deploy pipeline first.

---

## What NOT to Add

| Package / Tool | Why NOT |
|---------------|---------|
| `Azure.Identity` | Server validates incoming tokens, does not acquire tokens to call Azure APIs. Add only if the server later needs to call Key Vault, Storage, etc. |
| `Microsoft.Identity.Client` (MSAL.NET) | Client-side token acquisition library. The server is the resource, not the client. |
| `Duende.IdentityServer` | Entra IS the authorization server. Do not build or host a custom OAuth server. |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | For interactive web app login with cookies. MCP uses JWT bearer only. |
| `Yarp.ReverseProxy` | Container Apps has built-in ingress with TLS termination, custom domains, and traffic splitting. No reverse proxy needed. |
| `Swashbuckle` / `NSwag` (OpenAPI) | MCP endpoints are not REST APIs. OpenAPI docs would be misleading and incorrect. |
| `DistributedCacheEventStreamStore` | Only needed for multi-instance Streamable HTTP resumability. Start with in-memory (default). Add when scaling beyond 1 replica. |
| `Dapr` | Unnecessary abstraction. Direct Qdrant.Client + ASP.NET Core middleware is sufficient. |
| `Terraform` | Bicep is native Azure IaC. No benefit from adding a Terraform provider for a pure-Azure deployment. |
| `Azure DevOps Pipelines` | GitHub Actions is the CI/CD platform. No second system. |
| Separate `ModelContextProtocol.Core` package reference | Already pulled in transitively by `ModelContextProtocol`. Do not add a direct reference. |

---

## Version Compatibility Matrix

| Component | Version | Compatible With | Notes |
|-----------|---------|-----------------|-------|
| `ModelContextProtocol` | 1.2.0 | .NET 8+ | Upgrade from 1.1.0. No compile-time breaks. |
| `ModelContextProtocol.AspNetCore` | 1.2.0 | .NET 8+ | New. Requires ASP.NET Core shared framework. |
| `Microsoft.Identity.Web` | 4.6.0 | .NET 8+ | Latest stable. |
| Bicep CLI | 0.30+ | Azure CLI 2.65+ | Ships with `az bicep`. Graph extension requires Bicep v0.26+. |
| `actions/checkout` | v4 | ubuntu-latest | Standard. |
| `actions/setup-dotnet` | v4 | .NET 10.0.x | Supports .NET 10. |
| `azure/login` | v2 | OIDC federation | Supports federated credentials. |
| `azure/container-apps-deploy-action` | v1 | Container Apps 2024-03-01+ | Supports `imageToDeploy`. |

---

## Summary of Changes to Infrastructure.csproj

```xml
<!-- Upgrade -->
<PackageReference Include="ModelContextProtocol" Version="1.2.0" />

<!-- Add -->
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
<PackageReference Include="Microsoft.Identity.Web" Version="4.6.0" />

<!-- Add framework reference for ASP.NET Core (preserves PackAsTool) -->
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

That is the complete set of NuGet changes. Three lines.

---

## Sources

- [NuGet: ModelContextProtocol 1.2.0](https://www.nuget.org/packages/ModelContextProtocol/)
- [NuGet: ModelContextProtocol.AspNetCore 1.2.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/)
- [NuGet: Microsoft.Identity.Web 4.6.0](https://www.nuget.org/packages/Microsoft.Identity.Web)
- [MCP C# SDK Releases](https://github.com/modelcontextprotocol/csharp-sdk/releases)
- [MCP C# SDK: HttpServerTransportOptions](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html)
- [MCP C# SDK: Streamable HTTP Protocol (DeepWiki)](https://deepwiki.com/modelcontextprotocol/csharp-sdk/5.4-streamable-http-protocol)
- [MCP Specification: Authorization](https://modelcontextprotocol.io/specification/draft/basic/authorization)
- [MCP Specification: Transports (2025-03-26)](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
- [Microsoft Learn: JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Microsoft Learn: Protected web API app configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-app-configuration)
- [Microsoft Learn: Microsoft.App/containerApps Bicep reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/containerapps)
- [Microsoft Learn: Microsoft.App/managedEnvironments Bicep reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/managedenvironments)
- [Microsoft Learn: Microsoft.Graph/applications Bicep reference](https://learn.microsoft.com/en-us/graph/templates/bicep/reference/applications)
- [Microsoft Learn: Deploy to Container Apps with GitHub Actions](https://learn.microsoft.com/en-us/azure/container-apps/github-actions)
- [GitHub: Azure/container-apps-deploy-action](https://github.com/Azure/container-apps-deploy-action)
- [Bicep for Entra ID resources GA announcement](https://devblogs.microsoft.com/identity/bicep-templates-for-microsoft-entra-id-resources-is-ga/)
- [Building Remote MCP Servers with .NET and Azure Container Apps](https://dev.to/willvelida/building-remote-mcp-servers-with-net-and-azure-container-apps-cc2)
- [MCP Auth Specification Deep Dive](https://www.descope.com/blog/post/mcp-auth-spec)

---
*Stack research for: v1.1 Shared Server milestone*
*Researched: 2026-03-31*
