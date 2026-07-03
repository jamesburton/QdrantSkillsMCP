# Multi-stage build for the QdrantSkillsMCP MCP server (framework-dependent).
#
# Native AOT was evaluated but abandoned: the app relies on reflection-based
# IOptions config binding, Semantic Kernel's BertOnnx connector, protobuf/gRPC
# (Qdrant.Client) and reflection JSON on the tool hot path -- all RequiresDynamicCode
# (IL3050), which throw under native AOT at runtime. Shipping framework-dependent.

# Stage 1: Build + framework-dependent publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy solution/props/project files first for restore-layer caching
COPY QdrantSkillsMCP.slnx .
COPY Directory.Build.props .
COPY global.json .
COPY src/QdrantSkillsMCP.Core/QdrantSkillsMCP.Core.csproj src/QdrantSkillsMCP.Core/
COPY src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj src/QdrantSkillsMCP.Infrastructure/

RUN dotnet restore src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj

# Copy sources and publish (framework-dependent -- runtime supplied by the aspnet base image)
COPY src/ src/
RUN dotnet publish src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Stage 2: Runtime -- chiseled ASP.NET (minimal, non-root UID 1654)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

# LocalONNX (default embedding provider) downloads its model + native runtime on
# demand into LocalApplicationData. On Linux .NET resolves that via XDG_DATA_HOME,
# so pin it under a mountable path the non-root user can write to.
ENV XDG_DATA_HOME=/data \
    ASPNETCORE_ENVIRONMENT=Production

# Persist the ONNX model / native-runtime cache across container restarts.
# Bind-mount this at runtime (named volumes mount root-owned and the UID-1654
# app user cannot write to them).
VOLUME /data

COPY --from=build /app/publish .

EXPOSE 8934

# Bind to 0.0.0.0 so the mapped port is reachable from the host.
ENTRYPOINT ["dotnet", "QdrantSkillsMCP.Infrastructure.dll", "--http", "--url", "http://0.0.0.0:8934"]
