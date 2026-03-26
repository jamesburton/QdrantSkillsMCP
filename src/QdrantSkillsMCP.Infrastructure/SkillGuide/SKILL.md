---
name: qdrant-skills-mcp
description: Agent skill guide for QdrantSkillsMCP - semantic skill search and retrieval
version: "1.0"
tags: [mcp, skills, search, qdrant, embedding]
---

# QdrantSkillsMCP - Agent Skill Guide

## What is QdrantSkillsMCP

QdrantSkillsMCP is an MCP server that provides **semantic skill search and retrieval** backed by Qdrant vector database. It lets you store reusable skills (instructions, patterns, runbooks) and find them using natural language queries. Skills are embedded as vectors and matched by meaning, not just keywords.

## Available Tools

| Tool | Description |
|------|-------------|
| `search-skills` | Semantic search for skills matching a natural language query |
| `load-skill` | Load a specific skill by exact name, returning its full content |
| `list-skills` | List all stored skills with optional output mode filtering |
| `add-skill` | Add a new skill to the repository |
| `update-skill` | Update an existing skill's content or metadata |
| `delete-skill` | Permanently remove a skill from the repository |
| `archive-skill` | Archive a skill (soft delete, preserves content) |
| `reset-session` | Clear the current session's loaded skill tracking |
| `get-skill-guide` | Returns this guide (you are reading it now) |

## Search-Before-Load Pattern

**Always search first, then load specific skills by name.**

The recommended workflow:

1. **Search** with a natural language description of what you need:
   `search-skills query="error handling patterns for REST APIs"`

2. **Review** the search results -- they include skill names and summaries, not full content.

3. **Load** only the specific skill(s) you need by exact name:
   `load-skill name="rest-api-error-handling"`

**Why this matters:** Loading full skill content is expensive in context. Searching first lets you find the right skill without loading dozens of irrelevant ones. The search results give you enough information (name + summary) to decide which skills to load.

## Output Modes

When listing or searching skills, you can control how much detail is returned:

| Mode | Returns | Use When |
|------|---------|----------|
| `full` (default) | Complete skill content | You need the actual skill instructions |
| `names` | Skill names only | Quick inventory check, minimal context cost |
| `summaries` | Names + summary excerpts | Deciding which skills to load |

For large skill libraries (50+ skills), prefer `names` or `summaries` mode to avoid overwhelming your context window.

## Session Tracking

QdrantSkillsMCP tracks which skills you have loaded in the current session.

- **Search results include "ALREADY LOADED SKILLS"** -- a listing of skills you have already loaded in this session. Check this before loading a skill to avoid redundant loads.
- **Only `full` output mode marks skills as loaded** -- using `names` or `summaries` mode does not count as loading a skill.
- **Use `reset-session` for fresh context** -- if you want to clear the loaded skills tracking and start fresh, call `reset-session`.

## Frequent Skills

Your project may have **FrequentSkills** files that list commonly needed skills:

- `FrequentSkills.md` -- shared, team-curated list (committed to version control)
- `FrequentSkills.local.md` -- personal preferences (gitignored)

These files exist at two levels:
1. **User-level:** `~/.qdrant-skills/FrequentSkills.md` and `FrequentSkills.local.md`
2. **Project-level:** In the project root directory

Check these files before searching -- they indicate which skills are considered important for the current context. You may want to pre-load frequently used skills at the start of a session.

## Best Practices

1. **Search with natural language context, not just keywords.** Instead of `search-skills query="error"`, try `search-skills query="how to handle validation errors in ASP.NET controllers"`. The semantic search understands meaning.

2. **Use the temperature parameter to control match strictness.** Lower temperature (e.g., 0.3) returns only highly relevant matches. Higher temperature (e.g., 0.8) returns broader, more exploratory results.

3. **Use max-results to limit response size.** Default results may be more than you need. Set `max-results=3` for focused queries.

4. **Load skills by exact name after searching.** The search results give you names -- use those exact names with `load-skill`.

5. **Check session tracking to avoid redundant loads.** The "ALREADY LOADED SKILLS" section in search results tells you what is already in your context.

6. **Prefer summaries mode for discovery.** When exploring what skills are available, use `list-skills mode="summaries"` instead of loading everything.

7. **Archive instead of delete when unsure.** Archived skills can be recovered; deleted skills cannot.
