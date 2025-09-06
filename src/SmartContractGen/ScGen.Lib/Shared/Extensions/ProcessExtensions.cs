namespace ScGen.Lib.Shared.Extensions;

public static class ProcessExtensions
{
    private const string Localhost = "127.0.0.1";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GanacheStartupTimeout = TimeSpan.FromSeconds(30);

    public static async Task<ProcessExecutionResult> ExecuteAsync(
        this Process process,
        ILogger logger,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        TimeSpan actualTimeout = timeout ?? DefaultTimeout;
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(actualTimeout);

        try
        {
            DateTime startTime = DateTime.UtcNow;

            if (!process.Start())
            {
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartProcess + process.StartInfo.FileName,
                    TimeSpan.Zero);
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            TimeSpan duration = DateTime.UtcNow - startTime;

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                IsSuccess = process.ExitCode == 0,
                Duration = duration,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (OperationCanceledException e) when (timeoutCts.Token.IsCancellationRequested &&
                                                   !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(ExecuteAsync), ex.Message);
            }

            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                e.Message,
                actualTimeout);
        }
        catch (Exception ex)
        {
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                ex.Message,
                TimeSpan.Zero);
        }
    }

    public static async Task<ProcessExecutionResult> RunCommandAsync(
        string fileName,
        string arguments,
        ILogger logger,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        using Process process = new();
        process.StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);

        return await process.ExecuteAsync(logger, cancellationToken, timeout);
    }


    public static async Task<ProcessExecutionResult> RunCommandAsync(
        this Process process,
        string fileName,
        string arguments,
        ILogger logger,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        process.StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);
        return await process.ExecuteAsync(logger, cancellationToken, timeout);
    }

    public static async Task<ProcessExecutionResult> RunSolcAsync(
        string sourceFilePath,
        string outputDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string solc = "solc";
        if (!File.Exists(sourceFilePath))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.FileNotFound,
                TimeSpan.Zero);

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        string arguments = new StringBuilder()
            .Append("--abi --bin --optimize ")
            .Append($"-o \"{outputDirectory}\" ")
            .Append($"\"{sourceFilePath}\"")
            .ToString();

        return await RunCommandAsync(solc, arguments, logger, outputDirectory, cancellationToken, TimeSpan.FromMinutes(2));
    }

    public static async Task<ProcessExecutionResult> RunCargoAsync(
        string command,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string cargo = "cargo";

        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        return await RunCommandAsync(cargo, command, logger, workingDirectory, cancellationToken, TimeSpan.FromMinutes(10));
    }


    public static async Task<ProcessExecutionResult> RunAnchorAsync(
        string command,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string anchor = "anchor";
        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        return await RunCommandAsync(anchor, command, logger, workingDirectory, cancellationToken, TimeSpan.FromMinutes(5));
    }


    public static async Task<ProcessExecutionResult> RunGanacheAsync(
        this Process process,
        ILogger logger,
        int port = 8545,
        bool deterministic = true,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        const string ganache = "ganache";
        if (!await IsPortAvailableAsync(port, cancellationToken))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.PortAlreadyUse(port),
                TimeSpan.Zero);

        StringBuilder arguments = new StringBuilder()
            .Append($"--port {port} ")
            .Append("--host 0.0.0.0 ")
            .Append("--accounts 10 ")
            .Append("--defaultBalanceEther 1000 ");

        if (deterministic)
        {
            arguments.Append("--deterministic ");
        }

        arguments.Append("--gasLimit 12000000 ")
            .Append("--gasPrice 20000000000 ")
            .Append("--blockTime 1");

        process.StartInfo = CreateProcessStartInfo(ganache, arguments.ToString().Trim(), workingDirectory);

        try
        {
            DateTime startTime = DateTime.UtcNow;

            if (!process.Start())
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartGanache,
                    TimeSpan.Zero);

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(GanacheStartupTimeout);

            bool isReady = await WaitForGanacheReadyAsync(port, logger, timeoutCts.Token);

            TimeSpan duration = DateTime.UtcNow - startTime;

            if (!isReady)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.OperationFailedWithException(nameof(RunGanacheAsync), ex.Message);
                }

                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.GanacheFailed(GanacheStartupTimeout.TotalSeconds),
                    duration);
            }

            return new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = Messages.GanacheSuccessStart(port),
                StandardError = string.Empty,
                IsSuccess = true,
                Duration = duration,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (Exception ex)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception e)
            {
                logger.OperationFailedWithException(nameof(RunGanacheAsync), e.Message);
            }

            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                ex.Message,
                TimeSpan.Zero);
        }
    }

    // ===== HELPER METHODS =====

    private static ProcessStartInfo CreateProcessStartInfo(
        string fileName,
        string arguments,
        string? workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static async Task<bool> IsPortAvailableAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient client = new TcpClient();
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(Localhost, port, timeoutCts.Token);
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static async Task<bool> WaitForGanacheReadyAsync(int port, ILogger logger, CancellationToken cancellationToken)
    {
        const int maxAttempts = 15;
        const int delayMs = 2000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            try
            {
                using HttpClient client = new();
                using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

                HttpResponseMessage response = await client.PostAsync(
                    $"http://127.0.0.1:{port}",
                    new StringContent("""{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}""",
                        Encoding.UTF8, "application/json"),
                    timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(WaitForGanacheReadyAsync), ex.Message);
            }

            try
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }
}