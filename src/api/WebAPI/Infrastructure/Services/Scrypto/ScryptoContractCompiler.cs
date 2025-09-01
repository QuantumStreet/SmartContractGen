namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractCompiler(ILogger<ScryptoContractCompiler> logger) : IScryptoContractCompiler
{
    private readonly ILogger<ScryptoContractCompiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CompileContractResponse> CompileAsync(IFormFile? sourceCodeFile, CancellationToken token = default)
    {
        if (sourceCodeFile == null || sourceCodeFile.Length == 0)
        {
            _logger.LogWarning("Source code file is null or empty.");
            throw new ArgumentException("Source code file is required.", nameof(sourceCodeFile));
        }

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _logger.LogInformation("Created temp directory: {TempDir}", tempDir);

        try
        {
            // Сохраняем и извлекаем ZIP файл
            string zipPath = Path.Combine(tempDir, "source.zip");
            await using (FileStream fs = new(zipPath, FileMode.Create, FileAccess.Write))
            {
                await sourceCodeFile.CopyToAsync(fs, token);
            }

            _logger.LogInformation("Extracting zip file to {TempDir}", tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            // Ищем проект Scrypto (Cargo.toml)
            string? projectDir = FindScryptoProject(tempDir);
            if (string.IsNullOrEmpty(projectDir))
            {
                throw new InvalidOperationException("No Scrypto project found (Cargo.toml not found)");
            }

            _logger.LogInformation("Found Scrypto project at: {ProjectDir}", projectDir);

            // Компилируем проект через Docker
            bool buildSuccess = await CompileWithDockerAsync(projectDir, token);
            if (!buildSuccess)
            {
                throw new InvalidOperationException("Scrypto compilation failed");
            }

            // Ищем скомпилированные файлы
            var compiledFiles = FindCompiledFiles(projectDir);
            if (compiledFiles.wasmPath == null)
            {
                throw new FileNotFoundException("WASM file not found after successful build");
            }

            _logger.LogInformation("Found compiled files:");
            _logger.LogInformation("WASM: {WasmPath}", compiledFiles.wasmPath);
            if (compiledFiles.schemaPath != null)
            {
                _logger.LogInformation("Schema: {SchemaPath}", compiledFiles.schemaPath);
            }

            // Читаем скомпилированные файлы
            byte[] wasmContent = await File.ReadAllBytesAsync(compiledFiles.wasmPath, token);
            byte[]? schemaContent = null;

            if (compiledFiles.schemaPath != null && File.Exists(compiledFiles.schemaPath))
            {
                schemaContent = await File.ReadAllBytesAsync(compiledFiles.schemaPath, token);
            }

            string contractName = Path.GetFileNameWithoutExtension(compiledFiles.wasmPath);

            _logger.LogInformation("Successfully compiled Scrypto contract: {ContractName}", contractName);

            return new CompileContractResponse
            {
                BytecodeFileContent = wasmContent,
                BytecodeFileName = Path.GetFileName(compiledFiles.wasmPath),
                AbiFileContent = schemaContent ?? [],
                AbiFileName = compiledFiles.schemaPath != null
                    ? Path.GetFileName(compiledFiles.schemaPath)
                    : $"{contractName}.schema",
                ContentType = "application/octet-stream"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Scrypto contract compilation");
            throw;
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    private string? FindScryptoProject(string baseDir)
    {
        // Ищем Cargo.toml в корне или в поддиректориях
        string cargoPath = Path.Combine(baseDir, "Cargo.toml");
        if (File.Exists(cargoPath))
        {
            _logger.LogDebug("Found Cargo.toml in root: {Path}", cargoPath);
            return baseDir;
        }

        // Ищем в поддиректориях
        foreach (string subDir in Directory.GetDirectories(baseDir))
        {
            string subCargoPath = Path.Combine(subDir, "Cargo.toml");
            if (File.Exists(subCargoPath))
            {
                _logger.LogDebug("Found Cargo.toml in subdirectory: {Path}", subCargoPath);
                return subDir;
            }

            // Рекурсивный поиск
            string? found = FindScryptoProject(subDir);
            if (!string.IsNullOrEmpty(found))
            {
                return found;
            }
        }

        return null;
    }

    private async Task<bool> CompileWithDockerAsync(string projectDir, CancellationToken token)
    {
        if (!CheckDockerAvailability())
        {
            throw new InvalidOperationException("Docker is not available for Scrypto compilation");
        }

        string dockerImageName = "ghcr.io/krulknul/try-scrypto:1.3.0";
        string containerName = $"scrypto-compiler-{Guid.NewGuid():N}";

        // Создаем временную директорию для Cargo cache
        string cargoCache = Path.Combine(Path.GetTempPath(), $"cargo-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cargoCache);

        try
        {
            // Проверяем наличие образа
            if (!CheckDockerImage(dockerImageName))
            {
                _logger.LogInformation("Pulling Docker image: {Image}", dockerImageName);
                if (!await PullDockerImageAsync(dockerImageName))
                {
                    throw new InvalidOperationException($"Failed to pull Docker image {dockerImageName}");
                }
            }

            // Получаем UID и GID текущего пользователя
            string uid = GetCurrentUserId();
            string gid = GetCurrentGroupId();

            _logger.LogInformation("Starting Scrypto compilation with Docker...");
            _logger.LogInformation("Using Cargo cache directory: {CargoCache}", cargoCache);

            // Запускаем с volume для Cargo cache и переменными окружения
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --name {containerName} --rm " +
                            $"--user {uid}:{gid} " +
                            $"-v \"{projectDir}:/workspace\" " +
                            $"-v \"{cargoCache}:/usr/local/cargo/registry\" " +
                            $"-w /workspace " +
                            $"-e CARGO_HOME=/usr/local/cargo " +
                            $"-e USER_ID={uid} " +
                            $"-e GROUP_ID={gid} " +
                            $"{dockerImageName} " +
                            $"sh -c \"" +
                            $"mkdir -p /usr/local/cargo/registry && " +
                            $"chmod -R 777 /usr/local/cargo/registry && " +
                            $"scrypto build\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Running Docker command: docker {Args}", psi.Arguments);

            using Process? process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start Docker process");
                return false;
            }

            string output = await process.StandardOutput.ReadToEndAsync(token);
            string error = await process.StandardError.ReadToEndAsync(token);

            await process.WaitForExitAsync(token);

            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Scrypto build output:\n{Output}", output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Scrypto build error:\n{Error}", error);
                }
                else
                {
                    _logger.LogWarning("Scrypto build warnings:\n{Error}", error);
                }
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("Scrypto build failed with exit code: {ExitCode}", process.ExitCode);
                return false;
            }

            _logger.LogInformation("Scrypto build completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Docker compilation");
            return false;
        }
        finally
        {
            CleanupDockerContainer(containerName);
            CleanupTempDirectory(cargoCache);
        }
    }

    private (string? wasmPath, string? schemaPath) FindCompiledFiles(string projectDir)
    {
        // Возможные пути для target директории
        string[] possibleTargetDirs =
        [
            Path.Combine(projectDir, "target", "wasm32-unknown-unknown", "release"),
            Path.Combine(projectDir, "target", "release"),
            Path.Combine(projectDir, "target")
        ];

        _logger.LogDebug("Looking for compiled files in project: {ProjectDir}", projectDir);

        string? targetDir = null;
        foreach (string dir in possibleTargetDirs)
        {
            if (Directory.Exists(dir))
            {
                targetDir = dir;
                _logger.LogDebug("Found target directory: {TargetDir}", targetDir);
                break;
            }
        }

        if (targetDir == null)
        {
            _logger.LogWarning("No target directory found");
            LogDirectoryContents(projectDir, "project root");

            // Рекурсивный поиск всех .wasm файлов
            var allWasmFiles = Directory.GetFiles(projectDir, "*.wasm", SearchOption.AllDirectories);
            if (allWasmFiles.Any())
            {
                _logger.LogInformation("Found WASM files through recursive search:");
                foreach (var file in allWasmFiles)
                {
                    _logger.LogInformation("Found WASM: {File}", file);
                }

                // Ищем соответствующие schema файлы
                foreach (var wasmFile in allWasmFiles)
                {
                    string wasmFileDir = Path.GetDirectoryName(wasmFile) ?? "";
                    string wasmBaseName = Path.GetFileNameWithoutExtension(wasmFile);

                    // Возможные расширения для schema файлов (в рекурсивном поиске)
                    string[] recursiveSchemaExtensions = [".rpd", ".schema", ".json"];

                    foreach (string ext in recursiveSchemaExtensions)
                    {
                        string schemaFile = Path.Combine(wasmFileDir, wasmBaseName + ext);
                        if (File.Exists(schemaFile))
                        {
                            _logger.LogInformation("Found matching schema: {Schema}", schemaFile);
                            return (wasmFile, schemaFile);
                        }
                    }

                    // Если schema не найден, возвращаем только WASM
                    return (wasmFile, null);
                }
            }

            return (null, null);
        }

        LogDirectoryContents(targetDir, "target directory");

        // Ищем .wasm файлы
        string[] wasmFiles = Directory.GetFiles(targetDir, "*.wasm", SearchOption.AllDirectories);

        if (!wasmFiles.Any())
        {
            _logger.LogError("No .wasm files found in {TargetDir}", targetDir);

            // Попробуем поискать в родительской target директории
            string parentTargetDir = Path.Combine(projectDir, "target");
            if (Directory.Exists(parentTargetDir))
            {
                LogDirectoryContents(parentTargetDir, "parent target directory");
                wasmFiles = Directory.GetFiles(parentTargetDir, "*.wasm", SearchOption.AllDirectories);

                if (wasmFiles.Any())
                {
                    _logger.LogInformation("Found WASM files in parent target directory");
                }
            }

            if (!wasmFiles.Any())
            {
                return (null, null);
            }
        }

        string foundWasmPath = wasmFiles.First();
        string foundWasmDir = Path.GetDirectoryName(foundWasmPath) ?? "";
        string foundBaseName = Path.GetFileNameWithoutExtension(foundWasmPath);

        _logger.LogInformation("Found WASM file: {WasmPath}", foundWasmPath);
        _logger.LogInformation("Looking for schema files with base name: {BaseName}", foundBaseName);

        // Ищем соответствующие schema файлы в той же директории (в основном поиске)
        string[] mainSchemaExtensions = [".rpd", ".schema", ".json", ".abi"];
        string? foundSchemaPath = null;

        foreach (string ext in mainSchemaExtensions)
        {
            string potentialSchema = Path.Combine(foundWasmDir, foundBaseName + ext);
            if (File.Exists(potentialSchema))
            {
                foundSchemaPath = potentialSchema;
                _logger.LogInformation("Found schema file: {SchemaPath}", foundSchemaPath);
                break;
            }
        }

        // Также ищем любые schema файлы в той же директории
        if (foundSchemaPath == null)
        {
            foreach (string ext in mainSchemaExtensions)
            {
                string[] schemaFiles = Directory.GetFiles(foundWasmDir, $"*{ext}");
                if (schemaFiles.Any())
                {
                    foundSchemaPath = schemaFiles.First();
                    _logger.LogInformation("Found schema file (fallback): {SchemaPath}", foundSchemaPath);
                    break;
                }
            }
        }

        if (foundSchemaPath == null)
        {
            _logger.LogWarning("No schema file found, only WASM will be returned");
        }

        return (foundWasmPath, foundSchemaPath);
    }

    private void LogDirectoryContents(string directory, string description)
    {
        try
        {
            _logger.LogDebug("Contents of {Description} ({Directory}):", description, directory);

            if (!Directory.Exists(directory))
            {
                _logger.LogDebug("Directory does not exist!");
                return;
            }

            foreach (string dir in Directory.GetDirectories(directory))
            {
                _logger.LogDebug("  DIR: {Name}", Path.GetFileName(dir));
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                _logger.LogDebug("  FILE: {Name} ({Size} bytes)", Path.GetFileName(file), new FileInfo(file).Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging directory contents");
        }
    }

    private string GetCurrentUserId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "1000"; // Для Windows используем дефолтное значение
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

        return "1000"; // fallback
    }

    private string GetCurrentGroupId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "1000"; // Для Windows используем дефолтное значение
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

        return "1000"; // fallback
    }

    private bool CheckDockerAvailability()
    {
        try
        {
            _logger.LogDebug("Checking if Docker is available...");
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
            bool isAvailable = process.ExitCode == 0;

            if (isAvailable)
            {
                _logger.LogDebug("Docker is available");
            }
            else
            {
                _logger.LogWarning("Docker is not available");
            }

            return isAvailable;
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

    private void CleanupDockerContainer(string containerName)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove Docker container: {ContainerName}", containerName);
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