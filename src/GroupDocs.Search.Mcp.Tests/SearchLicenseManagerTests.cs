using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GroupDocs.Search.Mcp.Tests;

public class SearchLicenseManagerTests
{
    [Fact]
    public void IsLicensed_WithoutLicensePath_ReturnsFalse()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new McpConfig());
        var manager = new SearchLicenseManager(options, NullLogger<LicenseManager>.Instance);

        Assert.False(manager.IsLicensed);
    }

    [Fact]
    public void SetLicense_WithoutLicensePath_DoesNotThrow()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new McpConfig());
        var manager = new SearchLicenseManager(options, NullLogger<LicenseManager>.Instance);

        var ex = Record.Exception(() => manager.SetLicense());
        Assert.Null(ex);
    }

    [Fact]
    public void DescribeEngine_ReportsGroupDocsSearch()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new McpConfig());
        var manager = new SearchLicenseManager(options, NullLogger<LicenseManager>.Instance);

        var engine = manager.DescribeEngine();

        Assert.NotNull(engine);
        Assert.Equal("GroupDocs.Search", engine!.Value.Name);
    }
}
