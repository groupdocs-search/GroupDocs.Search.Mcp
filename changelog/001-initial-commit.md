---
id: 001
date: 2026-10-14
version: 26.10.0
type: feature
---

# GroupDocs.Search MCP server — first release

## What changed

New MCP server **`GroupDocs.Search.Mcp`** over **GroupDocs.Search for .NET 26.10.0**, built on
`GroupDocs.Mcp.Core` / `GroupDocs.Mcp.Local.Storage` **26.9.0** and `ModelContextProtocol` 1.1.0,
targeting `net10.0`. It is the thirteenth server in the GroupDocs MCP family and the first stateful
one: indexes persist on disk between tool calls and restarts.

Tools:

| Tool | What it does |
|---|---|
| `add_to_index` | Indexes a file, a folder (recursive) or the whole storage folder; creates the index on demand |
| `search` | Ranked matching documents as JSON, with occurrence counts, terms and fields |
| `get_text_fragments` | Matching passages with surrounding words, as plain text |
| `update_index` | Re-reads changed files, adds new files under indexed folders, drops deleted ones |
| `remove_from_index` | Removes an indexed path; files on disk untouched |
| `create_index` | Creates or opens a named index; `recreate` empties it |
| `get_index_info` | Existence, counts, indexed paths, version, other indexes, optional supported formats |
| `get_license_status` | From Core: licensing mode, engine version, metered consumption |

Distribution — **Docker-first (OCI)**:

- Image `ghcr.io/groupdocs-search/search-net-mcp:26.10.0` (+ `:latest`), multi-arch; Docker Hub
  mirror `groupdocs/search-net-mcp` once the org has the Docker Hub secrets.
- MCP Registry `io.github.groupdocs-search/groupdocs-search-mcp`, `registryType: oci`.
- No NuGet package: the packed tool measured **280.9 MiB** (2026-10-14) against NuGet.org's
  250 MB limit. The engine DLL alone is 199.6 MiB; the rest is ONNX Runtime and SkiaSharp natives
  that remain after `StripMobileAndExoticRuntimes`. Stripping further cannot close the gap.

Configuration — the family's five variables plus one:

- `GROUPDOCS_MCP_STORAGE_PATH`, `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH`,
  `GROUPDOCS_METERED_PUBLIC_KEY`, `GROUPDOCS_METERED_PRIVATE_KEY` (`isSecret`).
- **`GROUPDOCS_MCP_INDEX_PATH`** — where indexes live. Unset, they go to
  `<output or storage>/.groupdocs-search-index`; the image leaves it unset on purpose so the index
  persists inside the one `/data` mount every client config already has.

Design decisions:

- **Relative paths only.** Document paths are relative to the storage folder and indexes are
  addressed by name; rooted paths and `..` escapes are rejected. The server never reads outside
  the folders it was given.
- **The index folder is never indexed**, even when it sits inside the storage folder — an engine
  `DocumentFilter` excludes it.
- **Failures carry `isError: true`.** Tools return `CallToolResult`, so engine failures keep the
  family's `<Operation> failed for '<target>': …` text *and* set the flag; other products return
  `string` and come back success-shaped on engine errors.
- **Evaluation limits are stated in every response** (`evaluation` field in JSON, a trailing note in
  text): 100 documents per index, 10 documents per search.
- **Old indexes are converted.** GroupDocs.Search 26.10 cannot open indexes from 22.7–26.8 until
  they are converted; the server converts on first open and keeps the original as
  `<name>.pre-26.10-backup`.
- Linux image installs `libgdiplus libfontconfig1` for the engine's image paths; the
  `System.Drawing.EnableUnixSupport` switch is kept as the family's inert marker.

Engine behaviour documented in the tool descriptions because an agent would otherwise misread it
(verified on 26.10.0):

- `Index.Delete` accepts only paths passed to `Add`. A single file inside an indexed folder cannot
  be removed on its own; `remove_from_index` says so and lists the indexed paths.
- Unquoted multi-word queries match nothing; phrases need double quotes. `search` adds a `hint`
  when that is the likely reason for zero results.
- `*` is a whole-word wildcard in this DSL; `?` is the character wildcard.

## Why

Search answers the question the other servers cannot: *which of my documents say X, and where?*
Agents can read one file at a time; across hundreds of files they need an index. Running it locally
over MCP keeps confidential collections (contracts, HR, email archives) on the user's machine.

## Verification

- `dotnet test src -c Release`: 37/37 pass, including tool tests that drive the real engine in temp
  folders (add, search, phrase, fragments, update after edit, remove, recreate, missing index,
  index-folder exclusion).
- Live stdio session against the published DLL, evaluation mode: `tools/list` reports 8 tools;
  every tool returns the expected shape; every failure path returns `isError: true` with the
  prefixed text; `get_license_status` reports `evaluation` and engine `26.10`.

## Migration impact

None — first release.
