using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

public class CreateAsyncTests
{
    [Fact]
    public async Task CreateAsync_NullPath_ThrowsInvalidFilePath()
    {
        var ex = await Assert.ThrowsAsync<SolutionItemsException>(
            () => SolutionItemsService.CreateAsync(null!));
        Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
    }

    [Fact]
    public async Task CreateAsync_EmptyPath_ThrowsInvalidFilePath()
    {
        var ex = await Assert.ThrowsAsync<SolutionItemsException>(
            () => SolutionItemsService.CreateAsync(""));
        Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
    }

    [Fact]
    public async Task CreateAsync_WhitespacePath_ThrowsInvalidFilePath()
    {
        var ex = await Assert.ThrowsAsync<SolutionItemsException>(
            () => SolutionItemsService.CreateAsync("   "));
        Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
    }

    [Fact]
    public async Task CreateAsync_NonExistentFile_ThrowsSolutionFileNotFound()
    {
        string path = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".slnx");
        var ex = await Assert.ThrowsAsync<SolutionItemsException>(
            () => SolutionItemsService.CreateAsync(path));
        Assert.Equal(SolutionItemsErrorKind.SolutionFileNotFound, ex.Kind);
        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_UnsupportedExtension_ThrowsUnsupportedFormat()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "not a solution");
        try
        {
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                () => SolutionItemsService.CreateAsync(path));
            Assert.Equal(SolutionItemsErrorKind.UnsupportedFormat, ex.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreateAsync_SlnfFile_ThrowsSlnfNotSupported()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".slnf");
        File.WriteAllText(path, "{ \"solution\": { \"path\": \".\" } }");
        try
        {
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                () => SolutionItemsService.CreateAsync(path));
            Assert.Equal(SolutionItemsErrorKind.SlnfNotSupported, ex.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }
}