using Elara.Host.Utilities;
using Xunit;

namespace Elara.Host.UnitTests;

public sealed class HostConstantsTests
{
    [Fact]
    public void HostIntro_MentionsQuitInstructions()
    {
        var intro = HostConstants.ConsoleText.HostIntro;

        Assert.Contains("Host ready", intro);
        Assert.Contains("Press 'Q' to quit", intro);
    }
}
