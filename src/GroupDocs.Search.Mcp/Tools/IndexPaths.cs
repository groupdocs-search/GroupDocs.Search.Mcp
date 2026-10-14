using System.Text;

namespace GroupDocs.Search.Mcp.Tools;

/// <summary>
/// The path contract of every Search tool, in one place: index names live under
/// <see cref="SearchConfig.IndexRoot"/>, document paths under <see cref="SearchConfig.StorageRoot"/>,
/// and nothing a caller passes can step outside either.
/// </summary>
internal static class IndexPaths
{
    private const int MaxListedEntries = 50;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>Resolves an index name to its folder. Blank means the default index.</summary>
    /// <exception cref="ArgumentException">The name is rooted or escapes the index root.</exception>
    public static (string Name, string Folder) ResolveIndex(SearchConfig cfg, string? indexName)
    {
        var name = string.IsNullOrWhiteSpace(indexName) ? cfg.DefaultIndexName : indexName.Trim();
        name = name.Replace('\\', '/').Trim('/');

        if (Path.IsPathRooted(indexName ?? string.Empty) || name.Length == 0
            || name.Split('/').Any(s => s is "" or "." or ".."))
        {
            throw new ArgumentException(
                $"indexName must be a plain name such as 'contracts' (sub-folders like 'legal/2026' are allowed), " +
                $"not a filesystem path. Got '{indexName}'. Indexes are stored under the server's index folder " +
                $"(GROUPDOCS_MCP_INDEX_PATH).");
        }

        var folder = Path.GetFullPath(Path.Combine(cfg.IndexRoot, name));
        if (!IsUnder(folder, cfg.IndexRoot))
            throw new ArgumentException($"indexName '{indexName}' resolves outside the index folder.");

        return (name, folder);
    }

    /// <summary>
    /// Resolves a document path (file or folder) under the storage root. Blank means the whole
    /// storage folder.
    /// </summary>
    /// <exception cref="ArgumentException">The path is rooted or escapes the storage root.</exception>
    /// <exception cref="FileNotFoundException">Nothing exists at the path; the message lists what does.</exception>
    public static string ResolveDocuments(SearchConfig cfg, string? documentsPath, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(documentsPath) || documentsPath.Trim() is "." or "/" or "\\")
            return cfg.StorageRoot;

        var raw = documentsPath.Trim();
        if (Path.IsPathRooted(raw))
        {
            throw new ArgumentException(
                $"documentsPath must be relative to the storage folder, for example 'contracts' or " +
                $"'contracts/2026/lease.pdf'. Got the absolute path '{documentsPath}'. Omit it to use the " +
                $"whole storage folder.");
        }

        var full = Path.GetFullPath(Path.Combine(cfg.StorageRoot, raw));
        if (!IsUnder(full, cfg.StorageRoot) && !string.Equals(full, cfg.StorageRoot, PathComparison))
            throw new ArgumentException($"documentsPath '{documentsPath}' resolves outside the storage folder.");

        if (mustExist && !File.Exists(full) && !Directory.Exists(full))
            throw new FileNotFoundException(NotFoundMessage(cfg, documentsPath), documentsPath);

        return full;
    }

    /// <summary>Path relative to the storage root with forward slashes; absolute when outside it.</summary>
    public static string ToStorageRelative(SearchConfig cfg, string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return string.Empty;
        if (string.Equals(Path.TrimEndingDirectorySeparator(fullPath), cfg.StorageRoot, PathComparison)) return ".";
        return IsUnder(fullPath, cfg.StorageRoot)
            ? Path.GetRelativePath(cfg.StorageRoot, fullPath).Replace('\\', '/')
            : fullPath;
    }

    /// <summary>A path for use inside a sentence: quoted relative path, or "the whole storage folder".</summary>
    public static string Describe(SearchConfig cfg, string? fullPath)
    {
        var rel = ToStorageRelative(cfg, fullPath);
        return rel == "." ? "the whole storage folder" : $"'{rel}'";
    }

    /// <summary>Names of the existing indexes (one level deep) under the index root.</summary>
    public static string[] ListIndexes(SearchConfig cfg) =>
        Directory.Exists(cfg.IndexRoot)
            ? Directory.GetDirectories(cfg.IndexRoot)
                .Select(Path.GetFileName)
                .Where(n => n is not null && !n.EndsWith(IndexStore.BackupSuffix, StringComparison.Ordinal)
                            && !n.EndsWith(IndexStore.ConvertingSuffix, StringComparison.Ordinal))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    public static bool IsUnder(string path, string root)
    {
        var rootWithSep = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(rootWithSep, PathComparison);
    }

    private static string NotFoundMessage(SearchConfig cfg, string requested)
    {
        var sb = new StringBuilder($"Path '{requested}' not found in storage.");
        if (!Directory.Exists(cfg.StorageRoot))
            return sb.Append(" The storage folder does not exist.").ToString();

        var entries = new DirectoryInfo(cfg.StorageRoot).EnumerateFileSystemInfos()
            .Where(e => !e.Name.StartsWith('.') && !e.Attributes.HasFlag(FileAttributes.Hidden))
            .OrderBy(e => e is FileInfo).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
            return sb.Append(" The storage folder is empty.").ToString();

        sb.Append("\n\nAvailable files and folders:");
        foreach (var e in entries.Take(MaxListedEntries))
            sb.Append("\n- ").Append(e.Name).Append(e is DirectoryInfo ? "/" : string.Empty);
        if (entries.Count > MaxListedEntries)
            sb.Append($"\n…and {entries.Count - MaxListedEntries} more");
        return sb.ToString();
    }
}
