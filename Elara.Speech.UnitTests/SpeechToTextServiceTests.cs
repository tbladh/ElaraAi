using System;
using Elara.Speech;
using Xunit;

namespace Elara.Speech.UnitTests;

public sealed class SpeechToTextServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidPath_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => new SpeechToTextService(path));
    }
}
