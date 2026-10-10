namespace DotnetSlnxItems.Core;

/// <summary>
/// Whether a <see cref="SolutionItemInfo"/> represents a folder
/// (a virtual grouping of Solution Items) or a file (a single file
/// registered as a Solution Item).
/// </summary>
public enum SolutionItemKind
{
    Folder,
    File,
}