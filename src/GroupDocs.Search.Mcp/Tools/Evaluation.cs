using GroupDocs.Mcp.Core.Licensing;

namespace GroupDocs.Search.Mcp.Tools;

/// <summary>
/// The evaluation limits of GroupDocs.Search, stated once. Unlike most products they are counts,
/// not watermarks: the output looks complete and is not, so every response says so.
/// </summary>
internal static class Evaluation
{
    public const int MaxIndexedDocuments = 100;
    public const int MaxReturnedDocuments = 10;

    public const string Notice =
        "Evaluation mode: an index holds at most 100 documents and a search returns at most 10 documents. " +
        "Set GROUPDOCS_LICENSE_PATH, or GROUPDOCS_METERED_PUBLIC_KEY and GROUPDOCS_METERED_PRIVATE_KEY, to lift both limits.";

    /// <summary>Applies the licence once, then returns the notice when the server is unlicensed.</summary>
    public static string? For(ILicenseManager licenseManager)
    {
        licenseManager.SetLicense();
        return licenseManager.IsLicensed ? null : Notice;
    }

    public static string Append(string text, string? notice) =>
        notice is null ? text : text + "\n\n" + notice;
}
