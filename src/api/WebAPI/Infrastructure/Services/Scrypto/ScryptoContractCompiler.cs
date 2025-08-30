namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractCompiler(ILogger<ScryptoContractCompiler> logger) : IScryptoContractCompiler
{
    private readonly ILogger<ScryptoContractCompiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CompileContractResponse> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _logger.LogInformation("Created temp directory: {TempDir}", tempDir);

        try
        {
            string zipPath = Path.Combine(tempDir, "source.zip");
            await using (FileStream fs = new(zipPath, FileMode.Create, FileAccess.Write))
                await sourceCodeFile.CopyToAsync(fs, token);

            _logger.LogInformation("Extracting zip file to {TempDir}", tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            // Remove the zip file
            File.Delete(zipPath);

            _logger.LogInformation("Starting Scrypto build in {TempDir}", tempDir);

            // Build the Scrypto contract
            ProcessStartInfo psi = new()
            {
                FileName = "cargo",
                Arguments = "build --target wasm32-unknown-unknown --release",
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? proc = Process.Start(psi);
            if (proc == null) throw new Exception("Unable to start cargo process");

            string stdout = await proc.StandardOutput.ReadToEndAsync(token);
            string stderr = await proc.StandardError.ReadToEndAsync(token);
            await proc.WaitForExitAsync(token);

            if (proc.ExitCode != 0)
            {
                _logger.LogError("Cargo build failed: {Error}", stderr);
                throw new Exception($"cargo build failed: {stderr}");
            }

            _logger.LogInformation("Cargo build completed successfully");

            // Look for the compiled WASM file
            string targetDir = Path.Combine(tempDir, "target", "wasm32-unknown-unknown", "release");
            string[] wasmFiles = Directory.GetFiles(targetDir, "*.wasm");

            if (!wasmFiles.Any())
            {
                _logger.LogError("No .wasm files found in {TargetDir}", targetDir);
                throw new FileNotFoundException(
                    $"No .wasm files found in target/wasm32-unknown-unknown/release directory.\n" +
                    $"Files in targetDir: {string.Join(", ", Directory.GetFiles(targetDir))}\n" +
                    $"Cargo build stdout:\n{stdout}\n" +
                    $"Cargo build stderr:\n{stderr}\n");
            }

            string wasmPath = wasmFiles.First();
            Path.Combine(tempDir, "target", "wasm32-unknown-unknown", "release", "build");

            _logger.LogInformation("Using WASM path: {WasmPath}", wasmPath);

            string programName = Path.GetFileNameWithoutExtension(wasmPath);

            _logger.LogInformation("Using programName: {ProgramName}", programName);
            _logger.LogInformation("Using wasmPath: {WasmPath}", wasmPath);

            byte[] bytecode = await File.ReadAllBytesAsync(wasmPath, token);

            // For Scrypto, we don't have a separate ABI file like in Solidity/Ethereum
            // The WASM file contains both the bytecode and the interface information
            byte[] abi = Array.Empty<byte>(); // Empty for now, can be enhanced later

            return new CompileContractResponse
            {
                BytecodeFileContent = bytecode,
                BytecodeFileName = Path.GetFileName(wasmPath),
                AbiFileContent = abi,
                AbiFileName = string.Empty // No separate ABI for Scrypto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compilation failed");
            throw;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}