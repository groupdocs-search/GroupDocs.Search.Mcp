# Backlog & Known Issues

Running list of ideas, planned work, and known limitations for the GroupDocs.Search MCP server.
Grouped by topic. Terse on purpose — each line is a ticket, not an essay. `[ ]` = open,
`[x]` = shipped (kept for context).

**Current surface (26.10.1):** `create_index`, `add_to_index`, `update_index`, `remove_from_index`,
`search`, `get_text_fragments`, `get_index_info`, plus Core's `get_license_status`.

**Channel: Docker/GHCR only (OCI).** The packed tool is 280.9 MiB (measured 2026-10-14), over
NuGet.org's 250 MB limit, so there is no `dnx` path. Registry entry is registered as `oci`.

---

## Known issues & limitations

- **Delete granularity (engine).** `Index.Delete` accepts only paths that were passed to `Add`.
  A file inside an indexed folder cannot be removed alone (`"The path is not indexed"`; the
  document-key overload refuses filesystem documents). `remove_from_index` explains and lists the
  indexed paths. Workaround: index files or sub-folders individually when per-file removal matters.
- **Unquoted multi-word queries match nothing (engine).** `quarterly revenue` → 0,
  `"quarterly revenue"` → 1 on the same index. `search` returns a `hint` in that case.
- **Fragment text can start with metadata (engine).** For TXT files the first fragment of a
  document may begin with the file's modification timestamp glued to the first word
  (`…10/14/2026 15:16:11Quarterly revenue…`). The same gluing means a phrase that starts with
  the document's first word does not match. Seen on 26.10.0 with plain-text files; not yet checked
  on DOCX/PDF. Candidate upstream report.
- **Evaluation limits are counts:** 100 documents per index (repository), 10 documents per search.
  Stated in every response.
- **One process per index folder.** Access is serialised within a server process; two server
  processes writing the same index folder are not coordinated.

## Tools & functionality

- [ ] P2 `optimize_index` — 26.10 removes deleted documents physically only on `Index.Optimize`;
      long-lived indexes with many deletions grow until optimised.
- [ ] P2 Password-protected documents — `Index.Dictionaries.DocumentPasswords` or the
      `PasswordRequired` event; needs a safe way to accept passwords.
- [ ] P2 Search options: synonyms, word forms, homophones, spelling correction as boolean flags.
- [ ] P3 Chunked search for very large indexes (`SearchOptions.IsChunkSearch`, iterate while
      `NextChunkSearchToken` is not null — the chunk count changed in 26.10).
- [ ] P3 Reverse image search (`ImageSearchOptions`) — the main use of the ONNX natives.
- [ ] P3 Cache one opened `Index` per folder for the process lifetime if open latency shows up.

## Testing & CI

- [x] P1 Run the Tests repo's docker channel against the published image — first run (26.10.0) failed on Linux with
      `Permission denied` on `/data/.groupdocs-search-index`; fixed in 26.10.1 (fixture runs `--user`, server explains the fix).
- [ ] P2 Family-wide: every image runs as uid 1654, so on Linux any write to a bind-mounted folder the user owns fails
      the same way (Parser's `extract_images` docker tests fail on it). Document `--user` in every product's Docker section.
- [ ] P2 Linux container check of image indexing (PNG / image-bearing PDF) — `libgdiplus` path.
- [ ] P2 Metered run: `get_license_status` → `metered`, then `search` with the eval cap lifted.

## Documentation & discoverability

- [ ] P1 Docs section `docs.groupdocs.com/search/mcp/` via the kit's MCP docs generator.
- [ ] P2 Launch blog post and forum announcement.
- [ ] P2 Docker Hub mirror once `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` exist in the org.

## Platform & infra (longer-term)

- [ ] P2 NuGet channel if a slimmer engine ships (managed DLL is 199.6 MiB on its own) — follow
      the REVERT TO NUGET notes in `publish_prod.yml`, `build.ps1`, `server.json` and the Tests repo.
