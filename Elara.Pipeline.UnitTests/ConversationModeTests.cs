using Elara.Pipeline;
using Xunit;

namespace Elara.Pipeline.UnitTests;

public sealed class ConversationModeTests
{
    [Fact]
    public void Enum_HasExpectedOrdering()
    {
        Assert.Equal(0, (int)ConversationMode.Quiescent);
        Assert.Equal(1, (int)ConversationMode.Listening);
        Assert.Equal(2, (int)ConversationMode.Processing);
        Assert.Equal(3, (int)ConversationMode.Speaking);
    }
}
