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

No install needed with .NET 10's `dnx` (always uses the latest version):

```bash
# Initialize config with local Qdrant defaults
dnx QdrantSkillsMCP -- --config init

# Auto-configure your AI agent
dnx QdrantSkillsMCP -- --setup

# Verify connection
dnx QdrantSkillsMCP -- --config validate
```

Or install globally: `dotnet tool install -g QdrantSkillsMCP`

## Verification

Check that the MCP server is running and connected:

```bash
dnx QdrantSkillsMCP -- --console status
```

Once configured, use `get-skill-guide` to learn the full search-before-load workflow.
