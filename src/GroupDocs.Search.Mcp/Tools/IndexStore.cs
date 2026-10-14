using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GroupDocs.Search.Common;
using GroupDocs.Search.Options;

namespace GroupDocs.Search.Mcp.Tools;

/// <summary>
/// Opens, creates and upgrades indexes with one set of settings, and serialises access to each
/// index folder within this process.
/// </summary>
internal static class IndexStore
{
    public const string BackupSuffix = ".pre-26.10-backup";
    public const string ConvertingSuffix = ".converting";

    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lock object for an index folder. MCP clients may issue tool calls concurrently.</summary>
    public static object Gate(string folder) => Gates.GetOrAdd(folder, _ => new object());

    /// <summary>True when the folder holds an index (any content at all).</summary>
    public static bool Exists(string folder) =>
        Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any();

    /// <summary>
    /// Settings every tool uses. Text storage is what makes get_text_fragments possible; the
    /// document filter keeps the index folder itself out of the index when it lives inside the
    /// storage folder (the default when GROUPDOCS_MCP_INDEX_PATH is not set).
    /// </summary>
    public static IndexSettings Settings(SearchConfig cfg)
    {
        var options = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;
        var indexRoot = Regex.Escape(Path.TrimEndingDirectorySeparator(cfg.IndexRoot));
        var sep = Regex.Escape(Path.DirectorySeparatorChar.ToString());

        return new IndexSettings
        {
            TextStorageSettings = new TextStorageSettings(Compression.High),
            DocumentFilter = DocumentFilter.CreateNot(
                DocumentFilter.CreateFilePathRegularExpression($"^{indexRoot}{sep}", options))
        };
    }

    /// <summary>Creates a new index, or recreates it when <paramref name="overwrite"/> is set.</summary>
    public static Index Create(SearchConfig cfg, string folder, bool overwrite)
    {
        Directory.CreateDirectory(folder);
        return new Index(folder, Settings(cfg), overwrite);
    }

    /// <summary>
    /// Opens an existing index. Indexes written by GroupDocs.Search 22.7–26.8 are converted to the
    /// 26.10 format first — 26.10 cannot open them otherwise — and the original is kept next to it.
    /// </summary>
    /// <param name="conversionNote">A sentence for the caller when a conversion happened, else null.</param>
    public static Index Open(SearchConfig cfg, string folder, out string? conversionNote)
    {
        conversionNote = null;
        var updater = new IndexUpdater();
        if (!updater.IsLatestVersion(folder))
        {
            if (!updater.CanUpdateVersion(folder))
            {
                throw new InvalidOperationException(
                    "The index was written by a GroupDocs.Search version this engine cannot convert " +
                    "(older than 22.7). Recreate it with create_index (recreate: true) and add the documents again.");
            }

            var converting = folder + ConvertingSuffix;
            var backup = folder + BackupSuffix;
            if (Directory.Exists(converting)) Directory.Delete(converting, recursive: true);

            var result = updater.UpdateVersion(folder, converting);
            if (result == VersionUpdateResult.Updated)
            {
                if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
                Directory.Move(folder, backup);
                Directory.Move(converting, folder);
                conversionNote =
                    $"The index was converted from an older GroupDocs.Search format; the original was kept as " +
                    $"'{Path.GetFileName(backup)}'.";
            }
        }

        return new Index(folder, Settings(cfg));
    }

    /// <summary>
    /// The indexing report produced by the operation that started at <paramref name="started"/>, or
    /// null. The engine keeps a rolling list, so the last entry may belong to an earlier call.
    /// </summary>
    public static IndexingReport? LatestReportSince(Index index, DateTime started) =>
        index.GetIndexingReports()
            .Where(r => r.StartTime >= started)
            .OrderBy(r => r.StartTime)
            .LastOrDefault();
}
