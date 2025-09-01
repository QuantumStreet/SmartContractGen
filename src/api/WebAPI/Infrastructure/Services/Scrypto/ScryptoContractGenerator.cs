namespace WebAPI.Infrastructure.Services.Scrypto;

public sealed class ScryptoContractGenerator : IScryptoContractGenerator
{
    private readonly ILogger<ScryptoContractGenerator> _logger;
    private readonly IHandlebars _handlebars;
    private readonly string _templateProjectPath;
    private readonly string _templatePath;

    public ScryptoContractGenerator(ILogger<ScryptoContractGenerator> logger, IHandlebars? handlebars = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlebars = handlebars ?? Handlebars.Create();
        _templateProjectPath = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "ProjectTemplates",
            "scrypto-main-template");
        _templatePath = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "Templates", "ScryptoTemplate.hbs");

        RegisterHelpers(_handlebars);
        EnsureTemplateProjectExists();
    }

    private void EnsureTemplateProjectExists()
    {
        if (Directory.Exists(_templateProjectPath) && Directory.Exists(Path.Combine(_templateProjectPath, "src")))
        {
            _logger.LogInformation("Scrypto template project already exists at: {Path}", _templateProjectPath);
            return;
        }

        _logger.LogInformation("Scrypto template project not found or incomplete, creating...");

        string parentDir = Path.GetDirectoryName(_templateProjectPath) ?? string.Empty;
        if (!Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        try
        {
            // Сначала пытаемся создать проект локально
            if (TryCreateProjectLocally())
            {
                _logger.LogInformation("Successfully created Scrypto template project locally");
                return;
            }

            // Если локально не получилось, используем Docker
            CreateProjectWithDocker();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Scrypto template project");
            throw new InvalidOperationException("Failed to create Scrypto template with Docker", ex);
        }

        if (!Directory.Exists(_templateProjectPath) ||
            !Directory.Exists(Path.Combine(_templateProjectPath, "src")) ||
            !File.Exists(Path.Combine(_templateProjectPath, "src", "lib.rs")))
        {
            _logger.LogError("Template creation failed. Template structure is invalid.");
            throw new InvalidOperationException("Failed to create a valid Scrypto template");
        }
    }

    private bool TryCreateProjectLocally()
    {
        try
        {
            _logger.LogInformation("Attempting to create Scrypto project locally...");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "scrypto",
                Arguments = "new-package scrypto-main-template",
                WorkingDirectory = Path.GetDirectoryName(_templateProjectPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(30000);

            if (process.ExitCode == 0 && Directory.Exists(_templateProjectPath))
            {
                return true;
            }

            string error = process.StandardError.ReadToEnd();
            _logger.LogWarning("Local Scrypto creation failed: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create project locally");
            return false;
        }
    }

    private void CreateProjectWithDocker()
    {
        _logger.LogInformation("Creating Scrypto template with Docker...");

        if (!CheckDockerAvailability())
        {
            throw new InvalidOperationException("Docker is not available and local Scrypto installation not found");
        }

        string dockerImageName = "ghcr.io/krulknul/try-scrypto:1.3.0";
        string containerName = $"scrypto-template-creator-{Guid.NewGuid():N}";
        string templateName = "scrypto-main-template";
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Directory.CreateDirectory(tempDir);

        try
        {
            if (!CheckDockerImage(dockerImageName))
            {
                _logger.LogInformation("Pulling Docker image: {Image}", dockerImageName);
                if (!PullDockerImage(dockerImageName))
                {
                    throw new InvalidOperationException($"Failed to pull Docker image {dockerImageName}");
                }
            }

            // Запускаем Docker с текущим пользователем
            bool success = RunDockerContainerAsCurrentUser(dockerImageName, containerName, tempDir, templateName);
            if (!success)
            {
                throw new InvalidOperationException("Docker container failed to create template");
            }

            // Находим созданный проект
            string? projectPath = FindCreatedProject(tempDir, templateName);
            if (string.IsNullOrEmpty(projectPath))
            {
                LogDirectoryContents(tempDir, "temp directory");
                throw new DirectoryNotFoundException($"Created project not found in {tempDir}");
            }

            // Копируем в финальное место
            if (Directory.Exists(_templateProjectPath))
            {
                Directory.Delete(_templateProjectPath, true);
            }

            string? parentDir = Path.GetDirectoryName(_templateProjectPath);
            if (parentDir != null && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            CopyDirectory(projectPath, _templateProjectPath);

            _logger.LogInformation("Scrypto template project created successfully at: {Path}", _templateProjectPath);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
            CleanupDockerContainer(containerName);
        }
    }

    // Получение UID текущего пользователя
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

    // Получение GID текущего пользователя
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

    // Запуск Docker с текущим пользователем (БЕЗ SUDO!)
    private bool RunDockerContainerAsCurrentUser(string imageName, string containerName, string hostPath, string templateName)
    {
        try
        {
            string uid = GetCurrentUserId();
            string gid = GetCurrentGroupId();
            
            _logger.LogInformation("Running Docker with user {Uid}:{Gid}", uid, gid);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --name {containerName} --rm --user {uid}:{gid} -v \"{hostPath}:/workspace\" -w /workspace {imageName} scrypto new-package {templateName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Running Docker command: docker {Args}", psi.Arguments);

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit(60000);

            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Docker output: {Output}", output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Docker error: {Error}", error);
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("Docker container execution failed with code: {ExitCode}", process.ExitCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Docker container execution");
            return false;
        }
    }

    private string? FindCreatedProject(string baseDir, string templateName)
    {
        string[] possiblePaths =
        [
            Path.Combine(baseDir, templateName),
            Path.Combine(baseDir, "src"),
            baseDir
        ];

        foreach (string path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                string srcPath = Path.Combine(path, "src");
                string libPath = Path.Combine(srcPath, "lib.rs");
                string cargoPath = Path.Combine(path, "Cargo.toml");

                if (Directory.Exists(srcPath) && File.Exists(libPath) && File.Exists(cargoPath))
                {
                    _logger.LogInformation("Found valid Scrypto project at: {Path}", path);
                    return path;
                }
            }
        }

        // Рекурсивный поиск
        try
        {
            foreach (string subDir in Directory.GetDirectories(baseDir))
            {
                string? found = FindCreatedProject(subDir, templateName);
                if (!string.IsNullOrEmpty(found))
                {
                    return found;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during recursive project search");
        }

        return null;
    }

    private void LogDirectoryContents(string directory, string description)
    {
        try
        {
            _logger.LogError("Contents of {Description} ({Directory}):", description, directory);

            if (!Directory.Exists(directory))
            {
                _logger.LogError("Directory does not exist!");
                return;
            }

            foreach (string dir in Directory.GetDirectories(directory))
            {
                _logger.LogError("  DIR: {Name}", Path.GetFileName(dir));
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                _logger.LogError("  FILE: {Name}", Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging directory contents");
        }
    }

    // Упрощенная очистка БЕЗ SUDO
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
            // Не критично, если не удалось удалить временные файлы
        }
    }

    private bool CheckDockerAvailability()
    {
        try
        {
            _logger.LogInformation("Checking if Docker is available...");
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
                _logger.LogInformation("Docker is available");
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

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Docker image");
            return false;
        }
    }

    private bool PullDockerImage(string imageName)
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

            process.WaitForExit(120000);

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
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

    // Остальные методы остаются без изменений...
    public async Task<GenerateContractResponse> GenerateAsync(IFormFile? jsonFile, CancellationToken token = default)
    {
        if (jsonFile == null || jsonFile.Length == 0)
        {
            _logger.LogWarning("JSON file is null or empty.");
            throw new ArgumentException("JSON file is required.", nameof(jsonFile));
        }

        string jsonContent;
        using (StreamReader sr = new StreamReader(jsonFile.OpenReadStream()))
        {
            jsonContent = await sr.ReadToEndAsync(token).ConfigureAwait(false);
        }

        _logger.LogInformation("Received JSON contract specification: {Length} bytes", jsonContent.Length);

        JObject jObj;
        try
        {
            jObj = JObject.Parse(jsonContent);
            _logger.LogDebug("Successfully parsed JSON contract specification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid JSON payload.");
            throw new InvalidDataException("Invalid JSON payload.", ex);
        }

        IDictionary<string, object?> model = ConvertJToken(jObj) as IDictionary<string, object?> ??
                                             new Dictionary<string, object?>();
        model = CleanModel(model) as IDictionary<string, object?> ?? new Dictionary<string, object?>();

        if (!File.Exists(_templatePath))
        {
            _logger.LogError("Template file not found: {Path}", _templatePath);
            throw new FileNotFoundException($"Template file not found: {_templatePath}");
        }

        EnsureTemplateProjectExists();

        if (!Directory.Exists(_templateProjectPath))
        {
            _logger.LogError("Template project not found: {Path}", _templateProjectPath);
            throw new DirectoryNotFoundException($"Template project not found: {_templateProjectPath}");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _logger.LogInformation("Creating temporary project directory: {TempDir}", tempDir);

        try
        {
            Directory.CreateDirectory(tempDir);
            CopyDirectory(_templateProjectPath, tempDir);
            _logger.LogDebug("Template project copied to temp directory");

            string tplText = await File.ReadAllTextAsync(_templatePath, token).ConfigureAwait(false);

            HandlebarsTemplate<object, object> template;
            try
            {
                template = _handlebars.Compile(tplText);
                _logger.LogDebug("Handlebars template compiled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handlebars template compile error.");
                throw new InvalidDataException("Template compile error.", ex);
            }

            string scryptoCode;
            try
            {
                scryptoCode = template(model) ?? string.Empty;
                _logger.LogDebug("Generated Scrypto code: {Length} bytes", scryptoCode.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handlebars template render error.");
                throw new InvalidDataException("Template render error.", ex);
            }

            if (string.IsNullOrWhiteSpace(scryptoCode))
            {
                _logger.LogError("Generated Scrypto code is empty.");
                throw new InvalidDataException("Generated Scrypto code is empty.");
            }

            string srcDir = Path.Combine(tempDir, "src");
            if (!Directory.Exists(srcDir))
            {
                _logger.LogInformation("Creating src directory: {Path}", srcDir);
                Directory.CreateDirectory(srcDir);
            }

            string libPath = Path.Combine(srcDir, "lib.rs");
            await File.WriteAllTextAsync(libPath, scryptoCode, token);
            _logger.LogDebug("Generated code written to lib.rs");

            string projectName = MakeSafeProjectName(jObj["name"]?.ToString() ?? "scrypto_contract");
            _logger.LogInformation("Using project name: {ProjectName}", projectName);
            ReplaceProjectName(Path.Combine(tempDir, "Cargo.toml"), projectName);

            string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
            try
            {
                _logger.LogInformation("Creating ZIP archive for contract");
                ZipFile.CreateFromDirectory(tempDir, zipPath);
                byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, token);
                _logger.LogInformation("Successfully created contract ZIP package: {Size} bytes", zipBytes.Length);

                return new GenerateContractResponse
                {
                    ContractFileContent = zipBytes,
                    FileName = $"{projectName}.zip",
                    ContentType = "application/zip"
                };
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                    _logger.LogDebug("Temporary ZIP file deleted");
                }
            }
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    // Остальные helper методы остаются без изменений...
    private static string MakeSafeProjectName(string projectName)
    {
        string safe = Regex.Replace(projectName.Trim(), @"[^\w\-]", "_");
        safe = safe.ToLowerInvariant();
        safe = Regex.Replace(safe, @"_+", "_");

        if (Regex.IsMatch(safe, @"^\d"))
        {
            safe = "scrypto_" + safe;
        }

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "scrypto_contract";
        }

        return safe;
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, targetSubDir);
        }
    }

    private static void ReplaceProjectName(string filePath, string projectName)
    {
        if (!File.Exists(filePath)) return;

        string text = File.ReadAllText(filePath);
        text = Regex.Replace(text, @"name\s*=\s*"".*?""", $"name = \"{projectName}\"");
        File.WriteAllText(filePath, text);
    }

    private static void RegisterHelpers(IHandlebars hb)
    {
        hb.RegisterHelper("snakeCase", (writer, _, args) =>
        {
            if (args.Length == 0 || args[0] == null)
            {
                writer.WriteSafeString(string.Empty);
                return;
            }

            string input = args[0].ToString() ?? string.Empty;
            string snake = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
            writer.WriteSafeString(snake);
        });

        hb.RegisterHelper("pascalCase", (writer, _, args) =>
        {
            if (args.Length == 0 || args[0] == null)
            {
                writer.WriteSafeString(string.Empty);
                return;
            }

            string input = args[0].ToString() ?? string.Empty;
            string[] split = input.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);

            if (split.Length == 0)
            {
                writer.WriteSafeString(string.Empty);
                return;
            }

            List<string> pascalList = new List<string>(split.Length);

            foreach (string s in split)
            {
                if (string.IsNullOrEmpty(s)) continue;
                string pascal = char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1).ToLower() : "");
                pascalList.Add(pascal);
            }

            string pascalRes = string.Join("", pascalList);
            writer.WriteSafeString(pascalRes);
        });

        hb.RegisterHelper("scryptoType", (writer, _, args) =>
        {
            if (args.Length == 0 || args[0] == null)
            {
                writer.WriteSafeString("String");
                return;
            }

            string type = args[0].ToString()?.ToLowerInvariant() ?? "string";

            string scrypto = type switch
            {
                "string" => "String",
                "int" => "i64",
                "uint" => "u64",
                "bool" => "bool",
                "decimal" => "Decimal",
                "bytes" => "Vec<u8>",
                "float" => "Decimal",
                "address" => "ComponentAddress",
                "resource" => "ResourceAddress",
                _ => type
            };

            writer.WriteSafeString(scrypto);
        });

        hb.RegisterHelper("eq", (writer, _, args) =>
        {
            bool ok = args.Length >= 2 &&
                      string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("ne", (writer, _, args) =>
        {
            bool ok = args.Length >= 2 &&
                      !string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("and", (writer, _, args) =>
        {
            bool ok = args.Length > 0;
            for (int i = 0; i < args.Length && ok; i++)
            {
                if (!IsTruthy(args[i]))
                {
                    ok = false;
                }
            }

            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("or", (writer, _, args) =>
        {
            bool ok = false;
            for (int i = 0; i < args.Length && !ok; i++)
            {
                if (IsTruthy(args[i]))
                {
                    ok = true;
                }
            }

            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("not", (writer, _, args) =>
        {
            bool ok = args.Length == 0 || !IsTruthy(args[0]);
            writer.WriteSafeString(ok ? "true" : "");
        });
    }

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        string s = value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(s)) return false;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Equals("0")) return false;
        return true;
    }

    private static object? ConvertJToken(JToken? token)
    {
        if (token == null) return null;

        return token.Type switch
        {
            JTokenType.Object => ConvertJObject((JObject)token),
            JTokenType.Array => ConvertJArray((JArray)token),
            JTokenType.Integer => ((JValue)token).ToObject<long>(),
            JTokenType.Float => ((JValue)token).ToObject<double>(),
            JTokenType.Boolean => ((JValue)token).ToObject<bool>(),
            JTokenType.Null => null,
            _ => ((JValue)token).ToString(CultureInfo.InvariantCulture)
        };
    }

    private static IDictionary<string, object?> ConvertJObject(JObject jobject)
    {
        Dictionary<string, object?> dict = new Dictionary<string, object?>();
        foreach (JProperty prop in jobject.Properties())
        {
            dict[prop.Name] = ConvertJToken(prop.Value);
        }

        return dict;
    }

    private static List<object?> ConvertJArray(JArray jarray)
    {
        List<object?> list = new List<object?>(jarray.Count);
        foreach (JToken item in jarray)
        {
            list.Add(ConvertJToken(item));
        }

        return list;
    }

    private static object? CleanModel(object? node)
    {
        if (node is IDictionary<string, object?> dict)
        {
            List<string> keys = new List<string>(dict.Keys);
            foreach (string k in keys)
            {
                object? v = dict[k];
                object? cleaned = CleanModel(v);
                if (cleaned == null)
                {
                    dict.Remove(k);
                }
                else
                {
                    dict[k] = cleaned;
                }
            }

            return dict.Count == 0 ? null : dict;
        }

        if (node is IList<object?> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object? cleaned = CleanModel(list[i]);
                if (cleaned == null)
                    list.RemoveAt(i);
                else
                    list[i] = cleaned;
            }

            return list.Count == 0 ? null : list;
        }

        return node;
    }
}