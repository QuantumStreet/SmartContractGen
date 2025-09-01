namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractDeployer(
    ILogger<ScryptoContractDeployer> logger) : IScryptoContractDeployer
{
    private readonly ILogger<ScryptoContractDeployer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _logger.LogInformation("Created temp directory for deployment: {TempDir}", tempDir);

        try
        {
            // Определяем какой файл является WASM
            string wasmPath;
            IFormFile wasmFile;

            if (bytecodeFile.FileName.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                wasmPath = Path.Combine(tempDir, bytecodeFile.FileName);
                wasmFile = bytecodeFile;
            }
            else if (abiFile.FileName.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File parameters appear to be swapped, correcting: bytecode={BytecodeFile}, abi={AbiFile}",
                    bytecodeFile.FileName, abiFile.FileName);

                wasmPath = Path.Combine(tempDir, abiFile.FileName);
                wasmFile = abiFile;
            }
            else
            {
                throw new ArgumentException(
                    "No .wasm file found among provided files. Expected .wasm file for Scrypto package deployment");
            }

            _logger.LogInformation("Using WASM file: {WasmPath}", wasmPath);

            // Сохраняем WASM файл
            await using (var fs = new FileStream(wasmPath, FileMode.Create))
            {
                await wasmFile.CopyToAsync(fs, token);
            }

            var wasmFileInfo = new FileInfo(wasmPath);
            _logger.LogInformation("WASM file size: {Size} bytes", wasmFileInfo.Length);

            if (wasmFileInfo.Length == 0)
                throw new InvalidDataException("WASM file is empty");

            // Также сохраняем schema файл если есть
            string? schemaPath;
            if (abiFile != wasmFile && abiFile.Length > 0)
            {
                schemaPath = Path.Combine(tempDir, abiFile.FileName);
                await using (var fs = new FileStream(schemaPath, FileMode.Create))
                {
                    await abiFile.CopyToAsync(fs, token);
                }

                _logger.LogInformation("Saved schema file: {SchemaPath}", schemaPath);
            }

            // Выполняем весь процесс деплоя в одном Docker контейнере
            string packageAddress = await DeployInSingleContainerAsync(tempDir, Path.GetFileName(wasmPath), token);

            return new DeployContractResponse(
                ContractAddress: packageAddress,
                Success: true,
                TransactionHash: "deployed_successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying Scrypto package");
            return new DeployContractResponse(
                ContractAddress: string.Empty,
                Success: false,
                TransactionHash: ex.Message);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    private async Task<string> DeployInSingleContainerAsync(string workingDir, string wasmFileName, CancellationToken token)
    {
        if (!CheckDockerAvailability())
        {
            throw new InvalidOperationException("Docker is not available for Scrypto deployment");
        }

        string dockerImageName = "ghcr.io/krulknul/try-scrypto:1.3.0";

        // Проверяем наличие образа
        if (!CheckDockerImage(dockerImageName))
        {
            _logger.LogInformation("Pulling Docker image: {Image}", dockerImageName);
            if (!await PullDockerImageAsync(dockerImageName))
            {
                throw new InvalidOperationException($"Failed to pull Docker image {dockerImageName}");
            }
        }

        // Создаем временную директорию для домашней папки пользователя
        string homeDir = Path.Combine(Path.GetTempPath(), $"radix-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(homeDir);

        try
        {
            string uid = GetCurrentUserId();
            string gid = GetCurrentGroupId();
            string containerName = $"scrypto-deployer-{Guid.NewGuid():N}";

            // Создаем скрипт для выполнения всех команд подряд
            string deployScript = $@"#!/bin/bash
set -e

echo ""Setting up environment...""
mkdir -p /home/runner/.radix
chmod -R 777 /home/runner/.radix

echo ""Resetting resim...""
resim reset

echo ""Creating new account...""
ACCOUNT_OUTPUT=$(resim new-account)
echo ""$ACCOUNT_OUTPUT""

# Извлекаем адрес аккаунта
ACCOUNT=$(echo ""$ACCOUNT_OUTPUT"" | grep -oE 'account_sim1[a-z0-9_]+' | head -1)
echo ""Using account: $ACCOUNT""

echo ""Publishing package...""
PUBLISH_OUTPUT=$(resim publish {wasmFileName})
echo ""$PUBLISH_OUTPUT""

# Извлекаем адрес пакета
PACKAGE=$(echo ""$PUBLISH_OUTPUT"" | grep -oE 'package_sim1[a-z0-9_]+' | head -1)
echo ""Package address: $PACKAGE""

# Выводим результат в специальном формате для парсинга
echo ""DEPLOY_RESULT:$PACKAGE""
";

            string scriptPath = Path.Combine(workingDir, "deploy.sh");
            await File.WriteAllTextAsync(scriptPath, deployScript, token);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --name {containerName} --rm " +
                            $"--user {uid}:{gid} " +
                            $"-v \"{workingDir}:/workspace\" " +
                            $"-v \"{homeDir}:/home/runner\" " +
                            $"-w /workspace " +
                            $"-e HOME=/home/runner " +
                            $"-e USER_ID={uid} " +
                            $"-e GROUP_ID={gid} " +
                            $"{dockerImageName} " +
                            $"sh -c \"chmod +x deploy.sh && ./deploy.sh\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Deploying Scrypto package - Running Docker command: docker {Args}", psi.Arguments);

            using Process? process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Docker process for deployment");
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(token);

            string combinedOutput = $"{output}\n{error}".Trim();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Deployment failed with exit code {ExitCode}. Output: {Output}",
                    process.ExitCode, combinedOutput);
                throw new InvalidOperationException($"Deployment failed: {combinedOutput}");
            }

            _logger.LogInformation("Deployment completed successfully");
            _logger.LogInformation("Deployment output: {Output}", combinedOutput);

            // Извлекаем адрес пакета из результата
            var match = Regex.Match(combinedOutput, @"DEPLOY_RESULT:([a-z0-9_]+)");
            if (!match.Success)
            {
                // Альтернативные способы извлечения
                match = Regex.Match(combinedOutput, @"Package address:\s+([a-z0-9_]+)");
                if (!match.Success)
                {
                    match = Regex.Match(combinedOutput, @"package_sim1[a-z0-9_]+");
                }
            }

            if (!match.Success)
            {
                _logger.LogError("Failed to extract package address from output: {Output}", combinedOutput);
                throw new InvalidOperationException($"Failed to extract package address from output");
            }

            string packageAddress = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            _logger.LogInformation("Successfully deployed package: {PackageAddress}", packageAddress);

            return packageAddress;
        }
        finally
        {
            CleanupTempDirectory(homeDir);
        }
    }

    private string GetCurrentUserId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "1000";
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return "1000";

            process.WaitForExit(5000);
            if (process.ExitCode == 0)
            {
                return process.StandardOutput.ReadToEnd().Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current user ID");
        }

        return "1000";
    }

    private string GetCurrentGroupId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "1000";
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-g",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return "1000";

            process.WaitForExit(5000);
            if (process.ExitCode == 0)
            {
                return process.StandardOutput.ReadToEnd().Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current group ID");
        }

        return "1000";
    }

    private bool CheckDockerAvailability()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Docker availability");
            return false;
        }
    }

    private bool CheckDockerImage(string imageName)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"image inspect {imageName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Docker image");
            return false;
        }
    }

    private async Task<bool> PullDockerImageAsync(string imageName)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"pull {imageName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Docker pull failed: {Error}", error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Docker image pull");
            return false;
        }
    }

    private void CleanupTempDirectory(string tempDir)
    {
        if (!Directory.Exists(tempDir)) return;

        try
        {
            Directory.Delete(tempDir, true);
            _logger.LogDebug("Temporary directory cleaned up successfully: {TempDir}", tempDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary directory: {TempDir}. This is not critical.", tempDir);
        }
    }
}