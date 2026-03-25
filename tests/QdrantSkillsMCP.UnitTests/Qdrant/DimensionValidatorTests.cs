using QdrantSkillsMCP.Infrastructure.Qdrant;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Qdrant;

/// <summary>
/// Unit tests for DimensionValidator validation logic.
/// Tests the internal static helper methods which contain the core validation decisions.
/// The IHostedService orchestration is tested via integration tests.
/// </summary>
public sealed class DimensionValidatorTests
{
    // --- ValidateDimensionMismatch tests ---

    [Fact]
    public void ValidateDimensionMismatch_DimensionsMatch_ReturnsMatch()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 1536, mismatchResolution: null);

        Assert.Equal("match", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_NoResolution_ThrowsWithClearMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DimensionValidator.ValidateDimensionMismatch(
                "skills", existingDims: 1536, providerDims: 384, mismatchResolution: null));

        Assert.Contains("skills", ex.Message);
        Assert.Contains("1536", ex.Message);
        Assert.Contains("384", ex.Message);
        Assert.Contains("MismatchResolution", ex.Message);
        Assert.Contains("rename", ex.Message);
        Assert.Contains("suffix", ex.Message);
        Assert.Contains("replace", ex.Message);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_ResolutionRename_ReturnsRename()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 384, mismatchResolution: "rename");

        Assert.Equal("rename", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_ResolutionSuffix_ReturnsSuffix()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 384, mismatchResolution: "suffix");

        Assert.Equal("suffix", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_ResolutionReplace_ReturnsReplace()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 384, mismatchResolution: "replace");

        Assert.Equal("replace", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_ResolutionCaseInsensitive()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 384, mismatchResolution: "SUFFIX");

        Assert.Equal("suffix", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_ResolutionWithWhitespace()
    {
        var result = DimensionValidator.ValidateDimensionMismatch(
            "skills", existingDims: 1536, providerDims: 384, mismatchResolution: "  replace  ");

        Assert.Equal("replace", result);
    }

    [Fact]
    public void ValidateDimensionMismatch_Mismatch_UnrecognizedResolution_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DimensionValidator.ValidateDimensionMismatch(
                "my-collection", existingDims: 768, providerDims: 384, mismatchResolution: "invalid"));

        Assert.Contains("my-collection", ex.Message);
        Assert.Contains("768", ex.Message);
        Assert.Contains("384", ex.Message);
    }

    // --- ValidateEmbeddingOutput tests ---

    [Fact]
    public void ValidateEmbeddingOutput_DimensionsMatch_NoException()
    {
        var vector = new float[384];
        DimensionValidator.ValidateEmbeddingOutput(vector, declaredDimensions: 384);
        // No exception = pass
    }

    [Fact]
    public void ValidateEmbeddingOutput_DimensionsMismatch_Throws()
    {
        var vector = new float[256]; // provider returned 256 but claims 384

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DimensionValidator.ValidateEmbeddingOutput(vector, declaredDimensions: 384));

        Assert.Contains("256", ex.Message);
        Assert.Contains("384", ex.Message);
        Assert.Contains("inconsistent", ex.Message);
    }

    [Fact]
    public void ValidateEmbeddingOutput_EmptyVector_MismatchThrows()
    {
        var vector = Array.Empty<float>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DimensionValidator.ValidateEmbeddingOutput(vector, declaredDimensions: 384));

        Assert.Contains("0", ex.Message);
        Assert.Contains("384", ex.Message);
    }

    [Fact]
    public void ValidateEmbeddingOutput_ZeroDeclaredDimensions_EmptyVector_Passes()
    {
        var vector = Array.Empty<float>();
        DimensionValidator.ValidateEmbeddingOutput(vector, declaredDimensions: 0);
        // No exception = pass (edge case)
    }
}
