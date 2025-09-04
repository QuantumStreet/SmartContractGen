namespace ScGen.Lib.Shared.Extensions;

public static class ProcessExtensions
{
    public static async Task<ProcessExecutionResult> ExecuteAsync(
        this Process process,
        CancellationToken cancellationToken = default)
    {
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {process.StartInfo.FileName}");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            IsSuccess = process.ExitCode == 0
        };
    }

    public static async Task<ProcessExecutionResult> RunCommandAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        using Process process = new();
        process.StartInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await process.ExecuteAsync(cancellationToken);
    }

    public static async Task<ProcessExecutionResult> RunSolcAsync(
        string sourceFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        string arguments = $"--abi --bin --optimize -o \"{outputDirectory}\" \"{sourceFilePath}\"";
        return await RunCommandAsync("solc", arguments, outputDirectory, cancellationToken);
    }

    public static async Task<ProcessExecutionResult> RunCargoAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return await RunCommandAsync("cargo", command, workingDirectory, cancellationToken);
    }

    public static async Task<ProcessExecutionResult> RunAnchorAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return await RunCommandAsync("anchor", command, workingDirectory, cancellationToken);
    }

    public static async Task<ProcessExecutionResult> RunNodeAsync(
        string scriptPath,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return await RunCommandAsync("node", scriptPath, workingDirectory, cancellationToken);
    }
}