namespace WebAPI.Infrastructure.Services.Rust;

public class RustContractCompiler(ILogger<RustContractCompiler> logger) : IRustContractCompiler
{
    private readonly ILogger<RustContractCompiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            _logger.LogInformation("Starting anchor build in {TempDir}", tempDir);
            ProcessStartInfo psi = new()
            {
                FileName = "anchor",
                Arguments = "build",
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? proc = Process.Start(psi);
            if (proc == null) throw new Exception("Unable to start anchor process");

            string stdout = await proc.StandardOutput.ReadToEndAsync(token);
            string stderr = await proc.StandardError.ReadToEndAsync(token);
            await proc.WaitForExitAsync(token);

            if (proc.ExitCode != 0)
            {
                _logger.LogError("Anchor build failed: {Error}", stderr);
                throw new Exception($"anchor build failed: {stderr}");
            }

            _logger.LogInformation("Anchor build completed successfully");

            string deployDir = Path.Combine(tempDir, "target", "deploy");
            string idlDir = Path.Combine(tempDir, "target", "idl");

            _logger.LogInformation("Checking compiled files");
            _logger.LogDebug("deployDir: {DeployDir}", deployDir);
            var deployAllFiles = Directory.Exists(deployDir) ? Directory.GetFiles(deployDir) : Array.Empty<string>();
            foreach (var f in deployAllFiles) _logger.LogDebug("deployDir file: {File}", f);

            _logger.LogDebug("idlDir: {IdlDir}", idlDir);
            var idlAllFiles = Directory.Exists(idlDir) ? Directory.GetFiles(idlDir) : Array.Empty<string>();
            foreach (var f in idlAllFiles) _logger.LogDebug("idlDir file: {File}", f);

            string[] soFiles = Directory.GetFiles(deployDir, "*.so");
            string[] idlFiles = Directory.GetFiles(idlDir, "*.json");

            if (!soFiles.Any())
            {
                _logger.LogError("No .so files found in {DeployDir}", deployDir);
                throw new FileNotFoundException(
                    $"No .so files found in target/deploy directory.\n" +
                    $"Files in deployDir: {string.Join(", ", deployAllFiles)}\n" +
                    $"Anchor build stdout:\n{stdout}\n" +
                    $"Anchor build stderr:\n{stderr}\n");
            }

            if (!idlFiles.Any())
            {
                _logger.LogError("No .json files found in {IdlDir}", idlDir);
                throw new FileNotFoundException(
                    $"No .json files found in target/idl directory.\n" +
                    $"Files in idlDir: {string.Join(", ", idlAllFiles)}\n" +
                    $"Anchor build stdout:\n{stdout}\n" +
                    $"Anchor build stderr:\n{stderr}\n");
            }

            string soPath = soFiles.First();
            string idlPath = idlFiles.First();

            string programName = Path.GetFileNameWithoutExtension(soPath);

            _logger.LogInformation("Using programName: {ProgramName}", programName);
            _logger.LogInformation("Using soPath: {SoPath}", soPath);
            _logger.LogInformation("Using idlPath: {IdlPath}", idlPath);

            byte[] bytecode = await File.ReadAllBytesAsync(soPath, token);
            byte[] abi = await File.ReadAllBytesAsync(idlPath, token);

            return new ()
            {
                BytecodeFileContent = bytecode,
                BytecodeFileName = Path.GetFileName(soPath),
                AbiFileContent = abi,
                AbiFileName = Path.GetFileName(idlPath),
                ContentType = "application/octet-stream"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Anchor contract compilation");
            throw;
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
        }
    }
}