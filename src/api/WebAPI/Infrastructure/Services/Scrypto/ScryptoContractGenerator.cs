using System.Runtime.InteropServices;

namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractGenerator : IScryptoContractGenerator
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

        _logger.LogInformation("Scrypto template project not found or incomplete, creating with Docker...");

        string parentDir = Path.GetDirectoryName(_templateProjectPath) ?? string.Empty;
        if (!Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ProcessStartInfo chmodPsi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"-R 775 \"{parentDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? chmodProcess = Process.Start(chmodPsi);
                chmodProcess?.WaitForExit();
            }
        }

        string dockerImageName = "ghcr.io/krulknul/try-scrypto:1.3.0";
        string containerName = $"scrypto-template-creator-{Guid.NewGuid():N}";
        string containerMountPath = "/app/template";
        string templateName = "scrypto-main-template";

        try
        {
            bool dockerAvailable = CheckDockerAvailability();
            if (!dockerAvailable)
            {
                _logger.LogError("Docker is not available. Unable to create template.");
                throw new InvalidOperationException("Docker is required for template creation but is not available");
            }

            if (!CheckDockerImage(dockerImageName))
            {
                _logger.LogInformation("Docker image not found, pulling: {Image}", dockerImageName);
                if (!PullDockerImage(dockerImageName))
                {
                    _logger.LogError("Failed to pull Docker image. Unable to create template.");
                    throw new InvalidOperationException($"Failed to pull Docker image {dockerImageName}");
                }
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                _logger.LogInformation("Creating Scrypto template with Docker container...");

                bool success = RunDockerContainer(dockerImageName, containerName, tempDir, containerMountPath, templateName);
                if (!success)
                {
                    _logger.LogError("Docker container failed to create template.");
                    throw new InvalidOperationException("Failed to create template with Docker");
                }

                // Проверяем, создана ли структура проекта напрямую в tempDir или в поддиректории
                string srcPath = Path.Combine(tempDir, "src");
                string generatedPath = tempDir;

                // Если src нет в корне, возможно проект создан в поддиректории
                if (!Directory.Exists(srcPath))
                {
                    string potentialSubdir = Path.Combine(tempDir, templateName);
                    if (Directory.Exists(potentialSubdir) && Directory.Exists(Path.Combine(potentialSubdir, "src")))
                    {
                        generatedPath = potentialSubdir;
                        srcPath = Path.Combine(generatedPath, "src");
                        _logger.LogInformation("Found project structure in subdirectory: {Path}", generatedPath);
                    }
                }

                // Проверяем, что структура проекта корректна
                if (Directory.Exists(srcPath) && File.Exists(Path.Combine(srcPath, "lib.rs")))
                {
                    if (Directory.Exists(_templateProjectPath))
                    {
                        try
                        {
                            Directory.Delete(_templateProjectPath, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not delete existing template directory");
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(_templateProjectPath) ?? string.Empty);

                    _logger.LogInformation("Copying from {Source} to {Destination}", generatedPath, _templateProjectPath);
                    CopyDirectory(generatedPath, _templateProjectPath);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        ProcessStartInfo chmodPsi = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"-R 775 \"{_templateProjectPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using Process? chmodProcess = Process.Start(chmodPsi);
                        chmodProcess?.WaitForExit();
                    }

                    _logger.LogInformation("Scrypto template project copied successfully to: {Path}", _templateProjectPath);
                }
                else
                {
                    // Выводим содержимое директории для отладки
                    _logger.LogError("Generated template structure is invalid. Contents of temp directory:");
                    foreach (var dir in Directory.GetDirectories(tempDir))
                    {
                        _logger.LogError("Directory: {Dir}", dir);
                    }

                    foreach (var file in Directory.GetFiles(tempDir))
                    {
                        _logger.LogError("File: {File}", file);
                    }

                    throw new DirectoryNotFoundException($"Generated template structure not found at path: {srcPath}");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary directory: {TempDir}", tempDir);
                    }
                }

                try
                {
                    ProcessStartInfo stopPsi = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"rm -f {containerName}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using Process? stopProcess = Process.Start(stopPsi);
                    stopProcess?.WaitForExit();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove Docker container");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during template creation with Docker");
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

    private bool RunDockerContainer(string imageName, string containerName, string hostPath, string containerPath,
        string templateName)
    {
        try
        {
            EnsureDirectoryPermissions(hostPath);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments =
                    $"run --name {containerName} --rm -v \"{hostPath}:{containerPath}\" {imageName} scrypto new-package {templateName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null)
                return false;

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogInformation("Docker: {0}", e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("Docker: {0}", e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit(30000);

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

    private bool CheckDockerAvailability()
    {
        try
        {
            _logger.LogInformation("Checking if Docker is available...");
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckDockerImage(string imageName)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = $"image inspect {imageName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private bool PullDockerImage(string imageName)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = $"pull {imageName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(60000); // Ждем до 60 секунд для скачивания

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


    private void EnsureDirectoryPermissions(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "chmod",
                    Arguments = $"-R 777 \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using Process? process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set directory permissions for: {Path}", path);
            }
        }
    }

    public async Task<GenerateContractResponse> GenerateAsync(IFormFile? jsonFile, CancellationToken token = default)
    {
        // 1. Проверка входных данных
        if (jsonFile == null || jsonFile.Length == 0)
        {
            _logger.LogWarning("JSON file is null or empty.");
            throw new ArgumentException("JSON file is required.", nameof(jsonFile));
        }

        // 2. Чтение и парсинг JSON
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

        // 3. Подготовка модели для шаблона
        IDictionary<string, object?> model = ConvertJToken(jObj) as IDictionary<string, object?> ??
                                             new Dictionary<string, object?>();
        model = CleanModel(model) as IDictionary<string, object?> ?? new Dictionary<string, object?>();

        // 4. Проверка наличия шаблона и шаблонного проекта
        if (!File.Exists(_templatePath))
        {
            _logger.LogError("Template file not found: {Path}", _templatePath);
            throw new FileNotFoundException($"Template file not found: {_templatePath}");
        }

        // Убедимся, что шаблонный проект существует
        EnsureTemplateProjectExists();

        if (!Directory.Exists(_templateProjectPath))
        {
            _logger.LogError("Template project not found: {Path}", _templateProjectPath);
            throw new DirectoryNotFoundException($"Template project not found: {_templateProjectPath}");
        }

        // 5. Создание временной директории для нового проекта
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _logger.LogInformation("Creating temporary project directory: {TempDir}", tempDir);

        try
        {
            // 5.1 Создаем директорию и копируем шаблонный проект
            Directory.CreateDirectory(tempDir);
            CopyDirectory(_templateProjectPath, tempDir);
            _logger.LogDebug("Template project copied to temp directory");

            // 5.2 Чтение шаблона Handlebars
            string tplText = await File.ReadAllTextAsync(_templatePath, token).ConfigureAwait(false);

            // 5.3 Компиляция и применение шаблона
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

            // 5.4 Генерация кода смарт-контракта
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

            // 5.5 Запись сгенерированного кода
            string srcDir = Path.Combine(tempDir, "src");
            if (!Directory.Exists(srcDir))
            {
                _logger.LogInformation("Creating src directory: {Path}", srcDir);
                Directory.CreateDirectory(srcDir);
            }

            string libPath = Path.Combine(srcDir, "lib.rs");
            await File.WriteAllTextAsync(libPath, scryptoCode, token);
            _logger.LogDebug("Generated code written to lib.rs");

            // 6. Настройка имени проекта
            string projectName = MakeSafeProjectName(jObj["name"]?.ToString() ?? "scrypto_contract");
            _logger.LogInformation("Using project name: {ProjectName}", projectName);
            ReplaceProjectName(Path.Combine(tempDir, "Cargo.toml"), projectName);

            // 7. Создание ZIP архива
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
            // Очистка временной директории
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    _logger.LogDebug("Temporary project directory deleted");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary directory: {TempDir}", tempDir);
                }
            }
        }
    }

    private static string MakeSafeProjectName(string projectName)
    {
        // Удаляем спецсимволы и заменяем пробелы на подчеркивания
        string safe = Regex.Replace(projectName.Trim(), @"[^\w\-]", "_");

        // Cargo требует имена пакетов в нижнем регистре
        safe = safe.ToLowerInvariant();

        // Удаляем повторяющиеся подчеркивания
        safe = Regex.Replace(safe, @"_+", "_");

        // Имя не должно начинаться с цифры
        if (Regex.IsMatch(safe, @"^\d"))
        {
            safe = "scrypto_" + safe;
        }

        // Если имя пустое, используем дефолт
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

        // Создаем целевую директорию, если её нет
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

    private void ReplaceProjectName(string filePath, string projectName)
    {
        if (!File.Exists(filePath)) return;
        string text = File.ReadAllText(filePath);
        text = Regex.Replace(text, @"name\s*=\s*"".*""", $"name = \"{projectName}\"");
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
            string[] split = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

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
        IDictionary<string, object?> dict = new Dictionary<string, object?>();
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