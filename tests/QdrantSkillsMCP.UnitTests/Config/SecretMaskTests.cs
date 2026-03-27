using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Config;

/// <summary>
/// Tests for SecretMask API key masking utility.
/// </summary>
public sealed class SecretMaskTests
{
    [Fact]
    public void Mask_StandardKey_ShowsFirst3AndLast4()
    {
        Assert.Equal("sk-****f456", SecretMask.Mask("sk-abc123def456"));
    }

    [Fact]
    public void Mask_Null_ReturnsNotSet()
    {
        Assert.Equal("(not set)", SecretMask.Mask(null));
    }

    [Fact]
    public void Mask_Empty_ReturnsNotSet()
    {
        Assert.Equal("(not set)", SecretMask.Mask(""));
    }

    [Fact]
    public void Mask_Short_ReturnsStars()
    {
        Assert.Equal("****", SecretMask.Mask("short"));
    }

    [Fact]
    public void Mask_ExactlyEight_ReturnsStars()
    {
        Assert.Equal("****", SecretMask.Mask("12345678"));
    }

    [Fact]
    public void Mask_NineChars_ShowsFirst3AndLast4()
    {
        Assert.Equal("123****6789", SecretMask.Mask("123456789"));
    }

    [Fact]
    public void IsSecret_QdrantApiKey_ReturnsTrue()
    {
        Assert.True(SecretMask.IsSecret("QdrantApiKey"));
    }

    [Fact]
    public void IsSecret_OpenAiApiKey_ReturnsTrue()
    {
        Assert.True(SecretMask.IsSecret("OpenAiApiKey"));
    }

    [Fact]
    public void IsSecret_AzureOpenAiApiKey_ReturnsTrue()
    {
        Assert.True(SecretMask.IsSecret("AzureOpenAiApiKey"));
    }

    [Fact]
    public void IsSecret_QdrantHost_ReturnsFalse()
    {
        Assert.False(SecretMask.IsSecret("QdrantHost"));
    }
}
