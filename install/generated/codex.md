# Codex CLI (OpenAI)

```bash
codex mcp add groupdocs-search -- docker run --rm -i -v /path/to/documents:/data ghcr.io/groupdocs-search/search-net-mcp:latest
```

Or add to `~/.codex/config.toml`:

```toml
[mcp_servers.groupdocs-search]
command = "docker"
args = ["run", "--rm", "-i", "-v", "/path/to/documents:/data", "ghcr.io/groupdocs-search/search-net-mcp:latest"]
```

Pin a version by replacing `:latest` with `:26.10.0` in the image tag.
