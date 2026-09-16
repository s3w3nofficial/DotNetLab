using AwesomeAssertions;
using DotNetLab.Features.Sharing;

namespace DotNetLab;

[TestClass]
public sealed class UrlStateTests
{
    [TestMethod]
    [DataRow(null, "")]
    [DataRow("", "")]
    [DataRow("csharp", "csharp")]
    [DataRow("  razor  ", "razor")]
    [DataRow("https://example/#cshtml", "cshtml")]
    [DataRow("https://example/#slug-with#extra", "slug-with#extra")]
    public void GetSlugFromClipboardText(string? text, string expected)
        => LabUrlSync.GetSlugFromClipboardText(text).Should().Be(expected);

    [TestMethod]
    [DataRow("abcdef12", true, "abcdef12")]
    [DataRow("https://gist.github.com/user/0123456789abcdef0123456789abcdef", true, "0123456789abcdef0123456789abcdef")]
    [DataRow("https://gist.githubusercontent.com/user/deadbeef/raw", true, "deadbeef")]
    [DataRow("https://github.com/user/repo", false, "")]
    [DataRow("", false, "")]
    public void TryParseGistId(string url, bool expected, string gistId)
    {
        LabLinks.TryParseGistId(url, out var parsed).Should().Be(expected);
        parsed.Should().Be(gistId);
    }
}
