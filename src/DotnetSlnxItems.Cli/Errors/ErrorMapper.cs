using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Errors;

/// <summary>
/// Maps <see cref="SolutionItemsException"/> to the CLI's exit-code
/// contract. Exit codes are taken from <see cref="CliExitCodes"/>:
/// the public, documented mapping scripts and CI pipelines key off
/// of. The switch is exhaustive over
/// <see cref="SolutionItemsErrorKind"/>; the default arm catches
/// <c>FolderNotFound</c>, <c>PersistenceError</c>, and any unknown
/// kind and returns <see cref="CliExitCodes.Unhandled"/> with the
/// exception's text. The message stays as Core produced it so
/// error text never drifts between the library and the CLI.
/// </summary>
internal static class ErrorMapper
{
    public static (string Message, int ExitCode) Map(SolutionItemsException exception)
    {
        return exception.Kind switch
        {
            SolutionItemsErrorKind.SolutionFileNotFound => (exception.Message, CliExitCodes.SolutionFileNotFound),
            SolutionItemsErrorKind.UnsupportedFormat => (exception.Message, CliExitCodes.UnsupportedFormat),
            SolutionItemsErrorKind.SlnfNotSupported => (exception.Message, CliExitCodes.SlnfNotSupported),
            SolutionItemsErrorKind.AmbiguousSolutionInDirectory => (exception.Message, CliExitCodes.AmbiguousSolutionInDirectory),
            SolutionItemsErrorKind.InvalidFolderPath => (exception.Message, CliExitCodes.InvalidFolderPath),
            SolutionItemsErrorKind.InvalidFilePath => (exception.Message, CliExitCodes.InvalidFilePath),
            SolutionItemsErrorKind.FolderNotFound => (exception.Message, CliExitCodes.Unhandled),
            SolutionItemsErrorKind.PersistenceError => (exception.Message, CliExitCodes.Unhandled),
            SolutionItemsErrorKind.ManifestNotFound => (exception.Message, CliExitCodes.SolutionFileNotFound),
            SolutionItemsErrorKind.InvalidManifest => (exception.Message, CliExitCodes.Unhandled),
            SolutionItemsErrorKind.FileReferencedInManifestNotFound => (exception.Message, CliExitCodes.Unhandled),
            _ => (exception.Message, CliExitCodes.Unhandled),
        };
    }
}