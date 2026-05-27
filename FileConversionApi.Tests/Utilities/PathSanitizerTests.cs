using FileConversionApi.Utilities;
using Xunit;

namespace FileConversionApi.Tests.Utilities;

/// <summary>
/// Direct unit tests for the pure static functions on <see cref="PathSanitizer"/>.
/// Branches under test: null input, empty input, full Windows paths, trailing-separator paths.
/// </summary>
public sealed class PathSanitizerTests
{
    [Fact]
    public void GetSafeFileName_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathSanitizer.GetSafeFileName(null));
    }

    [Fact]
    public void GetSafeFileName_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathSanitizer.GetSafeFileName(string.Empty));
    }

    [Fact]
    public void GetSafeFileName_FullPath_ReturnsFileNameOnly()
    {
        Assert.Equal("bar.docx", PathSanitizer.GetSafeFileName(@"C:\foo\bar.docx"));
    }

    [Fact]
    public void GetSafeDirectoryName_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathSanitizer.GetSafeDirectoryName(null));
    }

    [Fact]
    public void GetSafeDirectoryName_TrailingSeparator_ReturnsLastSegment()
    {
        Assert.Equal("bar", PathSanitizer.GetSafeDirectoryName(@"C:\foo\bar\"));
    }

    [Fact]
    public void GetSafeDirectoryName_FullPath_ReturnsLastSegment()
    {
        Assert.Equal("bar", PathSanitizer.GetSafeDirectoryName(@"C:\foo\bar"));
    }
}
