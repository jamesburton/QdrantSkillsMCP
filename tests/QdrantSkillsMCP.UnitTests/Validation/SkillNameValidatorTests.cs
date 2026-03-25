using Xunit;
using QdrantSkillsMCP.Core.Validation;

namespace QdrantSkillsMCP.UnitTests.Validation;

public sealed class SkillNameValidatorTests
{
    [Theory]
    [InlineData("my-skill")]
    [InlineData("a")]
    [InlineData("skill-123")]
    [InlineData("a-b-c")]
    [InlineData("z")]
    [InlineData("abc")]
    public void Validate_ValidNames_ReturnsTrue(string name)
    {
        var (isValid, error) = SkillNameValidator.Validate(name);

        Assert.True(isValid, $"Expected '{name}' to be valid but got error: {error}");
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Empty_ReturnsFalseWithError()
    {
        var (isValid, error) = SkillNameValidator.Validate("");

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("empty", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Null_ReturnsFalseWithError()
    {
        var (isValid, error) = SkillNameValidator.Validate(null);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("My-Skill")]
    [InlineData("MY-SKILL")]
    [InlineData("mySkill")]
    public void Validate_Uppercase_ReturnsFalse(string name)
    {
        var (isValid, error) = SkillNameValidator.Validate(name);

        Assert.False(isValid, $"Expected '{name}' to be invalid");
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("my_skill")]
    [InlineData("my.skill")]
    [InlineData("my skill")]
    [InlineData("my@skill")]
    public void Validate_SpecialChars_ReturnsFalse(string name)
    {
        var (isValid, error) = SkillNameValidator.Validate(name);

        Assert.False(isValid, $"Expected '{name}' to be invalid");
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("-skill")]
    [InlineData("skill-")]
    [InlineData("-")]
    public void Validate_StartEndHyphen_ReturnsFalse(string name)
    {
        var (isValid, error) = SkillNameValidator.Validate(name);

        Assert.False(isValid, $"Expected '{name}' to be invalid");
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_TooLong_ReturnsFalse()
    {
        var longName = new string('a', 65);

        var (isValid, error) = SkillNameValidator.Validate(longName);

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("64", error!);
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsTrue()
    {
        var maxName = new string('a', 64);

        var (isValid, error) = SkillNameValidator.Validate(maxName);

        Assert.True(isValid, $"Expected 64-char name to be valid but got: {error}");
        Assert.Null(error);
    }

    [Fact]
    public void Validate_SingleChar_ReturnsTrue()
    {
        var (isValid, error) = SkillNameValidator.Validate("a");

        Assert.True(isValid);
        Assert.Null(error);
    }
}
