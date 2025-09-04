namespace ScGen.Lib.Shared.Models;

public sealed class ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public bool HasError => !string.IsNullOrWhiteSpace(StandardError);

    public string GetErrorMessage()
    {
        return IsSuccess
            ? string.Empty
            : string.IsNullOrWhiteSpace(StandardError)
                ? $"Process failed with exit code: {ExitCode}"
                : StandardError;
    }
}