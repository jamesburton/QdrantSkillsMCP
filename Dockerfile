# Multi-stage build for QdrantSkillsMCP MCP server
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy solution and project files first for layer caching
COPY QdrantSkillsMCP.slnx .
COPY Directory.Build.props .
COPY global.json .
COPY src/QdrantSkillsMCP.Core/QdrantSkillsMCP.Core.csproj src/QdrantSkillsMCP.Core/
COPY src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj src/QdrantSkillsMCP.Infrastructure/

# Restore dependencies
RUN dotnet restore src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj

# Copy source
COPY src/ src/

# Publish self-contained single binary
RUN dotnet publish src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Default environment — override at runtime via -e or docker-compose
ENV ASPNETCORE_ENVIRONMENT=Production \
    QdrantSkills__QdrantHost=qdrant \
    QdrantSkills__QdrantGrpcPort=6334 \
    QdrantSkills__CollectionName=skills \
    QdrantSkills__EmbeddingProvider=openai

# MCP server uses stdio transport — no ports needed
ENTRYPOINT ["dotnet", "QdrantSkillsMCP.Infrastructure.dll"]
