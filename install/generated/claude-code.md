# Claude Code

```bash
claude mcp add groupdocs-search -- docker run --rm -i -v /path/to/documents:/data ghcr.io/groupdocs-search/search-net-mcp:latest
```

With a GroupDocs license:

```bash
claude mcp add groupdocs-search -- docker run --rm -i -v /path/to/documents:/data -v /path/to/license-folder:/license -e GROUPDOCS_LICENSE_PATH=/license/GroupDocs.Total.lic ghcr.io/groupdocs-search/search-net-mcp:latest
```

Pin a version by replacing `:latest` with `:26.10.1` in the image tag.
