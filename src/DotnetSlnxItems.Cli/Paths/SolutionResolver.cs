namespace DotnetSlnxItems.Cli.Paths;

/// <summary>
/// Resolves the solution file path when the user did not pass
/// <c>--solution</c>. Looks for <c>.sln</c> / <c>.slnx</c> files in
/// the current working directory (no recursion, <c>.slnf</c>
/// ignored). Does not enumerate, decide, or throw on ambiguity: the
/// caller passes the resolved target verbatim to
/// <see cref="DotnetSlnxItems.Core.SolutionItemsService.CreateAsync(string, System.Threading.CancellationToken)"/>,
/// which detects the directory case and throws
/// <see cref="DotnetSlnxItems.Core.SolutionItemsException"/> with
/// <see cref="DotnetSlnxItems.Core.SolutionItemsErrorKind.AmbiguousSolutionInDirectory"/>
/// itself, keeping the "Multiple solution files found" string in
/// one place.
/// </summary>
internal static class SolutionResolver
{
    private static readonly string[] SolutionExtensions = { ".sln", ".slnx" };

    /// <summary>
    /// Returns:
    /// <list type="bullet">
    /// <item><description><c>null</c> when no <c>.sln</c>/<c>.slnx</c> exists in <paramref name="workingDirectory"/>.</description></item>
    /// <item><description>the file path when exactly one such file exists.</description></item>
    /// <item><description><paramref name="workingDirectory"/> itself when multiple such files exist; the caller hands the directory to Core, which throws the ambiguous-solution exception.</description></item>
    /// </list>
    /// </summary>
    public static string? ResolveFromCwd(string workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return null;
        }

        List<string> found = new();
        foreach (string file in Directory.EnumerateFiles(workingDirectory))
        {
            string ext = Path.GetExtension(file);
            foreach (string solution in SolutionExtensions)
            {
                if (string.Equals(ext, solution, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(file);
                    break;
                }
            }
        }

        return found.Count switch
        {
            0 => null,
            1 => found[0],
            _ => workingDirectory,
        };
    }
}