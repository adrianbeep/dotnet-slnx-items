namespace DotnetSlnxItems.Core;

/// <summary>
/// Represents a Solution Item in a solution: either a folder or a file.
/// </summary>
/// <param name="Path">
/// For a <see cref="SolutionItemKind.Folder"/>, the full virtual path
/// (for example <c>/Solution Items/</c>, <c>/Build/</c>,
/// <c>/docs/infra/</c>).
/// For a <see cref="SolutionItemKind.File"/>, the path relative to the
/// folder that contains it (for example <c>README.md</c>,
/// <c>Directory.Build.props</c>).
/// </param>
/// <param name="Kind">Whether this is a folder or a file.</param>
public sealed record SolutionItemInfo(
    string Path,
    SolutionItemKind Kind);