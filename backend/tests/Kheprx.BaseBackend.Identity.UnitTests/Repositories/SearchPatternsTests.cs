using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests;

public class SearchPatternsTests
{
    [Fact]
    public void ForNameAndNid_builds_contains_and_startswith_patterns()
    {
        var (name, nid) = SearchPatterns.ForNameAndNid("محمد");
        Assert.Equal("%محمد%", name);
        Assert.Equal("محمد%", nid);
    }

    [Fact]
    public void ForNameAndNid_trims_the_term()
    {
        var (name, nid) = SearchPatterns.ForNameAndNid("  ali  ");
        Assert.Equal("%ali%", name);
        Assert.Equal("ali%", nid);
    }

    [Fact]
    public void ForNameAndNid_escapes_like_wildcards_backslash_first()
    {
        // Input "50%_x\y" — all three LIKE special chars plus a backslash.
        // Order \ → \\, then % → \%, then _ → \_  yields term  50\%\_x\\y
        var (name, nid) = SearchPatterns.ForNameAndNid("50%_x\\y");
        Assert.Equal("%50\\%\\_x\\\\y%", name);
        Assert.Equal("50\\%\\_x\\\\y%", nid);
    }
}
