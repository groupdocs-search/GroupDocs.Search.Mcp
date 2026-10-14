FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GroupDocs.Search.Mcp/GroupDocs.Search.Mcp.csproj \
    -c Release -o /app --no-self-contained

FROM base AS final

# Native graphics deps for GroupDocs.Search's image paths on Linux: indexing
# image-bearing documents and reverse image search decode pictures through
# System.Drawing (libgdiplus) and SkiaSharp; libfontconfig1 is the font-discovery
# dependency both pull in. Search reads text and decodes images - it never
# rasterizes new glyphs, so ttf-mscorefonts-installer is not required.
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgdiplus libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*

# OCI image labels. VERSION is supplied by the publish workflow via
# --build-arg VERSION=YY.MM.N. Default 'dev' makes local `docker build` runs
# self-identify instead of inheriting the base image's misleading version.
ARG VERSION=dev
# DOCKER-FIRST: the io.modelcontextprotocol.server.name label lets the MCP Registry
# verify ownership of this GHCR image, which is how Search is published while its nupkg
# is too big for NuGet (~281 MiB > 250 MB). Revert plan: .github/workflows/publish_prod.yml.
LABEL io.modelcontextprotocol.server.name="io.github.groupdocs-search/groupdocs-search-mcp" \
      org.opencontainers.image.title="GroupDocs.Search.Mcp" \
      org.opencontainers.image.description="MCP server exposing GroupDocs.Search for .NET as AI-callable tools (create_index, add_to_index, update_index, remove_from_index, search, get_text_fragments, get_index_info) - full-text, boolean, phrase and fuzzy search over PDF, Office, email and 70+ formats, locally, for Claude, Cursor, GitHub Copilot, and other MCP agents." \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/groupdocs-search/GroupDocs.Search.Mcp" \
      org.opencontainers.image.url="https://github.com/groupdocs-search/GroupDocs.Search.Mcp/pkgs/container/search-net-mcp" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.vendor="Aspose Pty Ltd"

COPY --from=build /app .

# Documents live in /data. GROUPDOCS_MCP_INDEX_PATH is deliberately NOT set: the index is then
# kept in /data/.groupdocs-search-index, so it survives `docker run --rm` with the one volume
# every client config already mounts, and the server keeps it out of the index itself. For a
# read-only documents mount, add `-v <dir>:/index -e GROUPDOCS_MCP_INDEX_PATH=/index`.
ENV GROUPDOCS_MCP_STORAGE_PATH=/data
VOLUME /data

USER $APP_UID

ENTRYPOINT ["dotnet", "GroupDocs.Search.Mcp.dll"]
