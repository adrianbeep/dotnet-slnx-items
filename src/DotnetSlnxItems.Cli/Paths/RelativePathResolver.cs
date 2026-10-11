namespace DotnetSlnxItems.Cli.Paths;

/// <summary>
/// Resolves a file path to a forward-slash form relative to the
/// solution directory. Replicates
/// <c>SolutionItemsService.ResolveRelativeFilePath</c> (private to
/// Core) so the CLI can decide which input paths are already
/// present before calling <c>AddFilesAsync</c> / <c>RemoveFilesAsync</c>.
/// Kept in sync with Core's implementation; if Core changes its
/// relative storage rules, this must change too.
/// </summary>
internal static class RelativePathResolver
{
    public static string ToRelativeFilePath(string solutionDir, string filePath)
    {
        string fullPath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(solutionDir, filePath));
        string relative = Path.GetRelativePath(solutionDir, fullPath);
        return relative.Replace('\\', '/');
    }
}