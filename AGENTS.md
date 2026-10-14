# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

A standalone **MCP server** for [GroupDocs.Search for .NET](https://products.groupdocs.com/search) — exposes full-text indexing and search over local documents as AI-callable tools via the Model Context Protocol.

Published as a container image to `ghcr.io/groupdocs-search/search-net-mcp` (Docker Hub mirror `docker.io/groupdocs/search-net-mcp` when the org secrets exist) and registered in the MCP Registry as an `oci` package. **Docker-first:** the packed .NET tool is ~281 MiB, over NuGet.org's 250 MB limit, so there is no NuGet / `dnx` channel (see the revert notes in `.github/workflows/publish_prod.yml`).

## The one thing that differs from every other GroupDocs MCP server

**It is stateful.** An index persists on disk between tool calls and restarts. Three rules follow:

1. **Paths are relative.** Document paths are relative to `GROUPDOCS_MCP_STORAGE_PATH`; indexes are addressed by **name** under `GROUPDOCS_MCP_INDEX_PATH`. `Tools/IndexPaths.cs` is the only place that turns caller input into filesystem paths, and it rejects rooted paths and `..` escapes. Do not bypass it.
2. **Every engine call goes through `Tools/IndexStore.cs`**, which applies one `IndexSettings` (text storage on, the index folder filtered out of indexing), converts 22.7–26.8 indexes to the 26.10 format, and serialises access per index folder.
3. **Engine semantics are not intuitive — verified on 26.10.0:** `Index.Delete` only accepts paths passed to `Add` (not files inside an indexed folder); unquoted multi-word queries match nothing (phrases need double quotes); `*` is a whole-word wildcard, `?` the character wildcard.

## MCP tools exposed

| Tool | Description |
|---|---|
| `create_index` | Creates or opens a named index; `recreate=true` empties it. |
| `add_to_index` | Indexes a file, folder (recursive) or the whole storage folder; creates the index on demand. |
| `update_index` | Re-reads changed files, adds new files under indexed folders, drops deleted ones. |
| `remove_from_index` | Removes an indexed path from the index (files on disk untouched). |
| `search` | Ranked matching documents as JSON (`documentsFound`, `documents[]`, `truncated`, `hint`, `evaluation`). |
| `get_text_fragments` | Matching passages with context, as plain text. |
| `get_index_info` | Index existence, counts, indexed paths, version, available indexes, optional supported formats — as JSON. |
| `get_license_status` | Shipped by `GroupDocs.Mcp.Core`; licensing mode and metered consumption. |

**Error contract.** Tools return `CallToolResult`, not `string`. Failures carry `isError: true` **and** text starting `<Operation> failed for '<target>': …` (`Index creation`, `Indexing`, `Index update`, `Removal`, `Search`, `Fragment extraction`, `Index-info lookup`). Input validation throws `ArgumentException` / `FileNotFoundException`, which Core's `ToolErrorFilter` turns into the same `isError: true` shape.

## Folder layout

```
src/
  GroupDocs.Search.Mcp/
    Program.cs                       ← host bootstrap, stdio transport, SearchConfig registration
    SearchConfig.cs                  ← storage + index roots, resolved like Local.Storage resolves its folders
    SearchLicenseManager.cs          ← License / Metered / consumption overrides for Core
    Tools/
      CreateIndexTool.cs             ← [McpServerTool] create_index
      AddToIndexTool.cs              ← [McpServerTool] add_to_index
      UpdateIndexTool.cs             ← [McpServerTool] update_index
      RemoveFromIndexTool.cs         ← [McpServerTool] remove_from_index
      SearchTool.cs                  ← [McpServerTool] search (+ QueryHint)
      GetTextFragmentsTool.cs        ← [McpServerTool] get_text_fragments
      GetIndexInfoTool.cs            ← [McpServerTool] get_index_info
      IndexPaths.cs                  ← the path contract (relative paths, names, listings)
      IndexStore.cs                  ← settings, open/convert/create, per-folder locks, report lookup
      Evaluation.cs                  ← evaluation limits and notice text
      MissingIndex.cs                ← "index does not exist" recovery message
      ToolError.cs                   ← errors-as-text + isError helpers
    .mcp/server.json                 ← MCP Registry manifest (OCI)
    GroupDocs.Search.Mcp.csproj
  GroupDocs.Search.Mcp.Tests/        ← xUnit; tool tests drive the real engine in temp folders
  GroupDocs.Search.Mcp.sln
  Directory.Build.props
build/dependencies.props             ← single source of truth for versions
changelog/                           ← one MD file per change
docker/                              ← Dockerfile (aspnet:10.0 runtime), docker-compose.yml
install/                             ← config.json + generator for README buttons and client snippets
.github/workflows/                   ← build_packages, run_tests, publish_prod (OCI branch), publish_docker
```

## Commands you can run

```bash
dotnet build src/GroupDocs.Search.Mcp.sln -c Release
dotnet test src/GroupDocs.Search.Mcp.sln -c Release

# Run the server locally (stdio); indexes go to ./Files/.groupdocs-search-index unless configured
dotnet run --project src/GroupDocs.Search.Mcp

# Local pack - validates server.json against dependencies.props and reports the nupkg size
pwsh ./build.ps1

# Build + run the image
docker build -f docker/Dockerfile -t search-net-mcp:local .
docker run --rm -i -v $(pwd)/documents:/data search-net-mcp:local
```

## Version scheme

CalVer `YY.M.N` (M not zero-padded). The version lives in `build/dependencies.props` → `<GroupDocsSearchMcp>` and in `src/GroupDocs.Search.Mcp/.mcp/server.json` → the top-level `"version"` **and** the `:<version>` tag of the OCI `identifier` (an OCI package must not carry its own `version` field). `build.ps1` and `run_tests.yml` enforce this.

## House rules

1. **Tool `[Description]` strings are the product.** Say when to call the tool, the path rules, the query syntax that actually works, the response shape, the evaluation limits and the failure prefix.
2. **New env vars** need `server.json`, `docker-compose.yml`, `README.md` and `llms.txt` updated together.
3. **JSON tools return raw JSON** (`search`, `get_index_info`); the evaluation note is a JSON field, never a text prefix.
4. **Never hand a caller-supplied path to the engine** — resolve it through `IndexPaths`.
5. **Changelog entry + CalVer bump for every change**, in this repo and the Tests repo.
6. **Target framework is `net10.0` only.**

## What NOT to change

- Do not hardcode the version in `.csproj`.
- Do not remove `<PackageType>McpServer</PackageType>`, `<ToolCommandName>`, `StripNativeRuntimePdbs` or `StripMobileAndExoticRuntimes`.
- Do not set `GROUPDOCS_MCP_INDEX_PATH` in the Dockerfile: leaving it unset keeps the index inside the `/data` mount, so one volume is enough.
