using System.Text;
using ModelContextProtocol.Protocol;

namespace GroupDocs.Search.Mcp.Tools;

// The family's errors-as-text contract, with one difference from the other products: Search tools
// return CallToolResult, so a failure carries BOTH the readable "<op> failed for '<target>': ..."
// text AND isError: true. A tool returning string can only ever come back success-shaped, which
// is the defect class the 2026-08-16 audit called S3.
internal static class ToolError
{
    public static string Format(string op, string target, Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append($"{op} failed for '{target}': {ex.GetType().FullName}: {ex.Message}");
        var inner = ex.InnerException;
        for (int d = 0; inner != null && d < 5; d++, inner = inner.InnerException)
        {
            sb.Append($" | inner({d}): {inner.GetType().FullName}: {inner.Message}");
        }
        return sb.ToString();
    }

    public static CallToolResult Fail(string op, string target, Exception ex) => Fail(Format(op, target, ex));

    public static CallToolResult Fail(string op, string target, string reason) =>
        Fail($"{op} failed for '{target}': {reason}");

    public static CallToolResult Fail(string text) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = text }]
    };

    public static CallToolResult Ok(string text) => new()
    {
        Content = [new TextContentBlock { Text = text }]
    };
}
