using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Errors;

/// <summary>
/// Maps <see cref="SolutionItemsException"/> to the CLI's exit-code
/// contract. The switch is exhaustive over
/// <see cref="SolutionItemsErrorKind"/>; the default arm catches
/// <c>FolderNotFound</c>, <c>PersistenceError</c>, and any unknown
/// kind and returns exit 1 with the exception's text. The message
/// stays as Core produced it so error text never drifts between the
/// library and the CLI.
/// </summary>
internal static class ErrorMapper
{
    public static (string Message, int ExitCode) Map(SolutionItemsException exception)
    {
        return exception.Kind switch
        {
            SolutionItemsErrorKind.SolutionFileNotFound => (exception.Message, 2),
            SolutionItemsErrorKind.UnsupportedFormat => (exception.Message, 3),
            SolutionItemsErrorKind.SlnfNotSupported => (exception.Message, 4),
            SolutionItemsErrorKind.AmbiguousSolutionInDirectory => (exception.Message, 5),
            SolutionItemsErrorKind.InvalidFolderPath => (exception.Message, 6),
            SolutionItemsErrorKind.InvalidFilePath => (exception.Message, 7),
            SolutionItemsErrorKind.FolderNotFound => (exception.Message, 1),
            SolutionItemsErrorKind.PersistenceError => (exception.Message, 1),
            _ => (exception.Message, 1),
        };
    }
}