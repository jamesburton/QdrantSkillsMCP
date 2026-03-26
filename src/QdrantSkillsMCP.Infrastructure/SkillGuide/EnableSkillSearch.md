---
name: enable-skill-search
description: Bootstrap skill that teaches agents QdrantSkillsMCP exists and how to set it up
tags: [bootstrap, setup, mcp]
---

# Enable Skill Search

Your environment supports **semantic skill search** via QdrantSkillsMCP, an MCP server that lets you find and load reusable skills using natural language queries.

## Quick Check

If QdrantSkillsMCP is already configured as an MCP server, you can use:
- `search-skills` to find skills by meaning
- `get-skill-guide` to get the full usage guide

## Installation

If not yet installed:

```bash
# Install as a .NET global tool
dotnet tool install -g QdrantSkillsMCP

# Run the setup wizard to configure your agent
qdrant-skills-mcp --setup
```

## Verification

Check that the MCP server is running and connected:

```bash
qdrant-skills-mcp --console status
```

Once configured, use `get-skill-guide` to learn the full search-before-load workflow.
