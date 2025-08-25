namespace WebAPI.Infrastructure.Services.Rust;

public class RustContractDeployer(
    ILogger<RustContractDeployer> logger,
    IOptions<SolanaOptions> options) : IRustContractDeployer
{
    private readonly ILogger<RustContractDeployer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SolanaOptions _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    private const decimal MinRequiredSol = 1.0m;

    public async Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _logger.LogInformation("Created temp directory for deployment: {TempDir}", tempDir);

        Process? validatorProcess = null;

        try
        {
            string expandedKeypairPath =
                _options.KeypairPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (!File.Exists(expandedKeypairPath))
            {
                _logger.LogInformation("Default keypair not found at {KeypairPath}, creating one...", expandedKeypairPath);
                await CreateDefaultKeypairAsync(expandedKeypairPath);
            }

            string programPath;
            IFormFile programFile;

            if (bytecodeFile.FileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            {
                programPath = Path.Combine(tempDir, bytecodeFile.FileName);
                programFile = bytecodeFile;
            }
            else if (abiFile.FileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File parameters appear to be swapped, correcting: bytecode={BytecodeFile}, abi={AbiFile}",
                    bytecodeFile.FileName, abiFile.FileName);

                programPath = Path.Combine(tempDir, abiFile.FileName);
                programFile = abiFile;
            }
            else
            {
                throw new ArgumentException(
                    "No .so file found among provided files. Expected .so file for Solana program deployment");
            }

            _logger.LogInformation("Using program file: {ProgramPath}", programPath);

            await using (var fs = new FileStream(programPath, FileMode.Create))
                await programFile.CopyToAsync(fs, token);

            var programFileInfo = new FileInfo(programPath);
            _logger.LogInformation("Program file size: {Size} bytes", programFileInfo.Length);

            if (programFileInfo.Length == 0)
                throw new InvalidDataException("Program file is empty");

            if (_options.UseLocalValidator)
            {
                bool isValidatorRunning = await CheckValidatorStatusAsync();
                if (!isValidatorRunning)
                {
                    _logger.LogInformation("Starting local Solana validator...");
                    validatorProcess = StartLocalValidator();

                    _logger.LogInformation("Waiting for validator to start...");
                    await Task.Delay(5000, token);

                    isValidatorRunning = await CheckValidatorStatusAsync();
                    if (!isValidatorRunning)
                    {
                        _logger.LogWarning("Validator appears to still be starting, proceeding with deployment anyway");
                    }
                    else
                    {
                        _logger.LogInformation("Validator is now running");
                    }
                }
                else
                {
                    _logger.LogInformation("Local Solana validator is already running");
                }
            }

            await EnsureSufficientBalanceAsync(expandedKeypairPath, token);

            _logger.LogInformation("Deploying program to Solana at {RpcUrl}...", _options.RpcUrl);
            (string programId, string signature) = await DeployProgramAsync(programPath, expandedKeypairPath, token);

            return new DeployContractResponse(
                ContractAddress: programId,
                Success: true,
                TransactionHash: signature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying Solana program");
            return new DeployContractResponse(
                ContractAddress: string.Empty,
                Success: false,
                TransactionHash: ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    _logger.LogInformation("Cleaning up temp directory: {TempDir}", tempDir);
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
            }

            if (validatorProcess != null && _options.StopValidatorAfterDeploy)
            {
                _logger.LogInformation("Stopping local Solana validator");
                try
                {
                    validatorProcess.Kill(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to stop local Solana validator");
                }
            }
        }
    }

    private async Task EnsureSufficientBalanceAsync(string walletKeypairPath, CancellationToken token = default)
    {
        await Task.Delay(2000, token);

        decimal balance = 0;
        for (int i = 0; i < 3; i++)
        {
            balance = await GetAccountBalanceAsync(walletKeypairPath);
            if (balance > 0) break;

            _logger.LogInformation("Waiting for validator to be ready for balance check (attempt {Attempt}/3)...", i + 1);
            await Task.Delay(2000, token);
        }

        _logger.LogInformation("Current wallet balance: {Balance} SOL", balance);

        if (balance < MinRequiredSol)
        {
            _logger.LogInformation("Insufficient balance for deployment, requesting airdrop...");
            decimal amountToRequest = MinRequiredSol - balance;

            bool success = false;
            for (int i = 0; i < 3; i++)
            {
                success = await RequestAirdropAsync(walletKeypairPath, amountToRequest);
                if (success) break;

                _logger.LogWarning("Airdrop attempt {Attempt}/3 failed, retrying...", i + 1);
                await Task.Delay(2000, token);
            }

            if (success)
            {
                balance = await GetAccountBalanceAsync(walletKeypairPath);
                _logger.LogInformation("Airdrop successful. New balance: {Balance} SOL", balance);
            }
            else
            {
                _logger.LogWarning("All airdrop attempts failed. Continuing with current balance: {Balance} SOL", balance);
            }
        }
    }

    private async Task<decimal> GetAccountBalanceAsync(string walletKeypairPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "solana",
            Arguments = $"balance --url {_options.RpcUrl} --keypair {walletKeypairPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? proc = Process.Start(psi);
        if (proc == null) return 0;

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            _logger.LogWarning("Failed to get account balance: {Error}", error);
            return 0;
        }

        var match = Regex.Match(output, @"(\d+\.?\d*)\s*SOL");
        if (match.Success && match.Groups.Count > 1 && decimal.TryParse(match.Groups[1].Value, out decimal balance))
        {
            return balance;
        }

        return 0;
    }

    private async Task<bool> RequestAirdropAsync(string walletKeypairPath, decimal amount)
    {
        decimal maxAirdropAmount = _options.RpcUrl.Contains("localhost") ? 10.0m : 2.0m;
        int solToRequest = (int)Math.Ceiling(Math.Min(amount, maxAirdropAmount));

        var psi = new ProcessStartInfo
        {
            FileName = "solana",
            Arguments = $"airdrop {solToRequest} --url {_options.RpcUrl} --keypair {walletKeypairPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Requesting airdrop of {Amount} SOL", solToRequest);

        Process? proc = Process.Start(psi);
        if (proc == null) return false;

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            _logger.LogWarning("Airdrop failed: {Error}", error);
            return false;
        }

        _logger.LogInformation("Airdrop requested successfully: {Output}", output);

        await Task.Delay(2000);

        return true;
    }

    private async Task CreateDefaultKeypairAsync(string keypairPath)
    {
        string? directory = Path.GetDirectoryName(keypairPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "solana-keygen",
            Arguments = $"new -o {keypairPath} --no-bip39-passphrase --force",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Creating default keypair with command: {Command} {Args}", psi.FileName, psi.Arguments);

        Process? proc = Process.Start(psi);
        if (proc == null) throw new Exception("Unable to start solana-keygen process");

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            _logger.LogError("Failed to create default keypair: {Error}", error);
            throw new Exception($"Failed to create default keypair: {error}");
        }

        _logger.LogInformation("Default keypair created: {Output}", output);
    }

    private async Task<bool> CheckValidatorStatusAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "solana",
            Arguments = $"validators --url {_options.RpcUrl}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? proc = Process.Start(psi);
        if (proc == null) return false;

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            _logger.LogDebug("Validator check failed: {Error}", error);
            return false;
        }

        _logger.LogDebug("Validator check succeeded: {Output}", output);
        return true;
    }

    private Process StartLocalValidator()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "solana-test-validator",
            Arguments = "--reset",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? proc = Process.Start(psi);
        if (proc == null) throw new Exception("Failed to start local Solana validator");

        return proc;
    }

    private async Task<(string programId, string signature)> DeployProgramAsync(string programPath, string walletKeypairPath,
        CancellationToken token)
    {
        if (!programPath.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Expected .so file for program deployment but got: {ProgramPath}", programPath);
            throw new ArgumentException($"Invalid program file: {programPath}. Expected .so file for Solana program deployment.");
        }

        var createKeypairProcess = new ProcessStartInfo
        {
            FileName = "solana-keygen",
            Arguments = "new -o program-keypair.json --no-bip39-passphrase",
            WorkingDirectory = Path.GetDirectoryName(programPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Creating program keypair...");
        Process? proc = Process.Start(createKeypairProcess);
        if (proc == null) throw new Exception("Unable to start solana-keygen process");

        string keygenOutput = await proc.StandardOutput.ReadToEndAsync(token);
        string keygenError = await proc.StandardError.ReadToEndAsync(token);
        await proc.WaitForExitAsync(token);

        if (proc.ExitCode != 0)
        {
            _logger.LogError("Failed to create keypair: {Error}", keygenError);
            throw new Exception($"Failed to create program keypair: {keygenError}");
        }

        _logger.LogInformation("Keypair created: {Output}", keygenOutput);

        string keypairPath = Path.Combine(Path.GetDirectoryName(programPath)!, "program-keypair.json");

        var getProgramIdProcess = new ProcessStartInfo
        {
            FileName = "solana",
            Arguments = $"address -k {keypairPath}",
            WorkingDirectory = Path.GetDirectoryName(programPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        proc = Process.Start(getProgramIdProcess);
        if (proc == null) throw new Exception("Unable to start solana process for getting program ID");
        string programId = (await proc.StandardOutput.ReadToEndAsync(token)).Trim();
        string addressError = await proc.StandardError.ReadToEndAsync(token);
        await proc.WaitForExitAsync(token);

        if (proc.ExitCode != 0)
        {
            _logger.LogError("Failed to get program ID: {Error}", addressError);
            throw new Exception($"Failed to get program ID: {addressError}");
        }

        _logger.LogInformation("Generated program ID: {ProgramId}", programId);

        var deployProcess = new ProcessStartInfo
        {
            FileName = "solana",
            Arguments = $"program deploy --url {_options.RpcUrl} --keypair {walletKeypairPath} " +
                        $"--program-id {keypairPath} {programPath}",
            WorkingDirectory = Path.GetDirectoryName(programPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Deploying program to Solana with command: solana {Args}", deployProcess.Arguments);
        proc = Process.Start(deployProcess);
        if (proc == null) throw new Exception("Unable to start solana process for program deployment");

        string deployOutput = await proc.StandardOutput.ReadToEndAsync(token);
        string deployError = await proc.StandardError.ReadToEndAsync(token);
        await proc.WaitForExitAsync(token);

        if (proc.ExitCode != 0)
        {
            _logger.LogError("Program deployment failed: {Error}", deployError);
            throw new Exception($"Failed to deploy program: {deployError}");
        }

        string signature = "n/a";
        if (deployOutput.Contains("Signature"))
        {
            var match = Regex.Match(deployOutput, @"Signature:\s+([a-zA-Z0-9]+)");
            if (match.Success && match.Groups.Count > 1)
            {
                signature = match.Groups[1].Value;
            }
        }

        _logger.LogInformation("Program deployed successfully with signature: {Signature}", signature);
        return (programId, signature);
    }
}