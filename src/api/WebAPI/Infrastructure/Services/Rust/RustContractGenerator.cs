namespace WebAPI.Infrastructure.Services.Rust;

public class RustContractGenerator : IRustContractGenerator
{
    private readonly ILogger<RustContractGenerator> _logger;
    private readonly IHandlebars _handlebars;
    private readonly string _templateProjectPath;
    private readonly string _templatePath;

    public RustContractGenerator(ILogger<RustContractGenerator> logger, IHandlebars? handlebars = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlebars = handlebars ?? Handlebars.Create();
        _templateProjectPath = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "ProjectTemplates",
            "rust-main-template");
        _templatePath = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "Templates", "RustTemplate.hbs");
        EnsureTemplateProjectExists();
        RegisterHelpers(_handlebars);
    }

    private void EnsureTemplateProjectExists()
    {
        if (Directory.Exists(_templateProjectPath)) return;
        _logger.LogInformation("Rust template project not found, creating with `anchor init`...");
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "anchor";
        psi.Arguments = "init rust-main-template";
        psi.WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "ProjectTemplates");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process? process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Unable to start anchor process for project init.");
        process.WaitForExit();
        process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0)
        {
            _logger.LogError("anchor init failed: {StdErr}", stderr);
            throw new Exception("anchor init failed: " + stderr);
        }

        _logger.LogInformation("Rust template project created successfully.");
    }

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

        JObject jObj;
        try
        {
            jObj = JObject.Parse(jsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid JSON payload.");
            throw new InvalidDataException("Invalid JSON payload.", ex);
        }

        IDictionary<string, object?>? model = ConvertJToken(jObj) as IDictionary<string, object?>;
        if (model == null)
        {
            model = new Dictionary<string, object?>();
        }

        model = CleanModel(model) as IDictionary<string, object?>;
        if (model == null)
        {
            model = new Dictionary<string, object?>();
        }

        if (!File.Exists(_templatePath))
        {
            _logger.LogError("Template file not found: {Path}", _templatePath);
            throw new FileNotFoundException($"Template file not found: {_templatePath}");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CopyDirectory(_templateProjectPath, tempDir);

        string tplText = await File.ReadAllTextAsync(_templatePath, token).ConfigureAwait(false);

        HandlebarsTemplate<object, object> template;
        try
        {
            template = _handlebars.Compile(tplText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handlebars template compile error.");
            throw new InvalidDataException("Template compile error.", ex);
        }

        string rustCode;
        try
        {
            rustCode = template(model) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handlebars template render error.");
            throw new InvalidDataException("Template render error.", ex);
        }

        if (string.IsNullOrWhiteSpace(rustCode))
        {
            _logger.LogError("Generated rust code is empty.");
            throw new InvalidDataException("Generated rust code is empty.");
        }

        string libPath = Path.Combine(tempDir, "programs", "rust-main-template", "src", "lib.rs");
        string? libDir = Path.GetDirectoryName(libPath);
        if (!Directory.Exists(libDir))
            if (libDir != null)
                Directory.CreateDirectory(libDir);
        await File.WriteAllTextAsync(libPath, rustCode, token);

        string projectName = (jObj["name"]?.ToString() ?? "anchor_contract").Trim();

        ReplaceProjectName(Path.Combine(tempDir, "Cargo.toml"), projectName);

        string anchorTomlPath = Path.Combine(tempDir, "Anchor.toml");
        AddOrUpdateAnchorToml(anchorTomlPath, projectName);

        string zipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        ZipFile.CreateFromDirectory(tempDir, zipPath);

        byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, token);

        Directory.Delete(tempDir, true);
        File.Delete(zipPath);

        GenerateContractResponse response = new GenerateContractResponse();
        response.ContractFileContent = zipBytes;
        response.FileName = projectName + ".zip";
        response.ContentType = "application/zip";
        return response;
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        string[] files = Directory.GetFiles(sourceDir);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        }

        string[] dirs = Directory.GetDirectories(sourceDir);
        for (int i = 0; i < dirs.Length; i++)
        {
            string dir = dirs[i];
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    private void ReplaceProjectName(string filePath, string projectName)
    {
        if (!File.Exists(filePath)) return;
        string text = File.ReadAllText(filePath);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"name\s*=\s*"".*""", $"name = \"{projectName}\"");
        File.WriteAllText(filePath, text);
    }

    private void AddOrUpdateAnchorToml(string filePath, string projectName)
    {
        string defaultProgramId = "Fg6PaFpoGXkYsidMpWTK6W2BeZ7FEfcYkg476zPFsLnS";
        string tomlDefault = "[programs.localnet]\n" + projectName + " = \"" + defaultProgramId +
                             "\"\n\n[provider]\ncluster = \"localnet\"\nwallet = \"~/.config/solana/id.json\"\n";
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, tomlDefault);
            return;
        }

        string text = File.ReadAllText(filePath);
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(\[programs\.localnet\][^\[]*)",
            "[programs.localnet]\n" + projectName + " = \"" + defaultProgramId + "\"\n"
        );
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"cluster\s*=\s*"".*""",
            "cluster = \"localnet\""
        );
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"wallet\s*=\s*"".*""",
            "wallet = \"~/.config/solana/id.json\""
        );
        File.WriteAllText(filePath, text);
    }

    private static void RegisterHelpers(IHandlebars hb)
    {
        hb.RegisterHelper("snakeCase", (writer, _, args) =>
        {
            string input = args[0]?.ToString() ?? "";
            string snake = System.Text.RegularExpressions.Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
            writer.WriteSafeString(snake);
        });

        hb.RegisterHelper("pascalCase", (writer, _, args) =>
        {
            string input = args[0]?.ToString() ?? "";
            string[] split = input.Split('_', '-', ' ');
            List<string> pascalList = new List<string>();
            for (int i = 0; i < split.Length; i++)
            {
                string s = split[i];
                if (string.IsNullOrEmpty(s)) continue;
                string pascal = char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1).ToLower() : "");
                pascalList.Add(pascal);
            }

            string pascalRes = string.Join("", pascalList);
            writer.WriteSafeString(pascalRes);
        });

        hb.RegisterHelper("rustType", (writer, _, args) =>
        {
            string type = args[0]?.ToString() ?? "";
            string rust;
            if (type == "string") rust = "String";
            else if (type == "int") rust = "i64";
            else if (type == "uint") rust = "u64";
            else if (type == "bool") rust = "bool";
            else if (type == "pubkey") rust = "Pubkey";
            else if (type == "bytes") rust = "Vec<u8>";
            else if (type == "float") rust = "f64";
            else rust = type;
            writer.WriteSafeString(rust);
        });

        hb.RegisterHelper("anchorAccountType", (writer, _, args) =>
        {
            string type = args[0]?.ToString() ?? "";
            string rust;
            if (type == "token") rust = "Account<'info, TokenAccount>";
            else if (type == "mint") rust = "Account<'info, Mint>";
            else if (type == "system") rust = "Program<'info, System>";
            else if (type == "associated_token") rust = "Program<'info, AssociatedToken>";
            else if (type == "signer") rust = "Signer<'info>";
            else rust = type;
            writer.WriteSafeString(rust);
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
            for (int i = 0; i < args.Length; i++)
            {
                if (!IsTruthy(args[i]))
                {
                    ok = false;
                    break;
                }
            }

            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("or", (writer, _, args) =>
        {
            bool ok = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (IsTruthy(args[i]))
                {
                    ok = true;
                    break;
                }
            }

            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("not", (writer, _, args) =>
        {
            bool ok = !(args.Length > 0 && IsTruthy(args[0]));
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
        if (token.Type == JTokenType.Object)
        {
            IDictionary<string, object?> dict = new Dictionary<string, object?>();
            foreach (JProperty prop in ((JObject)token).Properties())
            {
                dict[prop.Name] = ConvertJToken(prop.Value);
            }

            return dict;
        }

        if (token.Type == JTokenType.Array)
        {
            List<object?> list = new List<object?>();
            foreach (JToken item in (JArray)token)
            {
                list.Add(ConvertJToken(item));
            }

            return list;
        }

        if (token.Type == JTokenType.Integer)
            return ((JValue)token).ToObject<long>();
        if (token.Type == JTokenType.Float)
            return ((JValue)token).ToObject<double>();
        if (token.Type == JTokenType.Boolean)
            return ((JValue)token).ToObject<bool>();
        if (token.Type == JTokenType.Null)
            return null;
        return ((JValue)token).ToString(CultureInfo.InvariantCulture);
    }

    private static object? CleanModel(object? node)
    {
        if (node is IDictionary<string, object?> dict)
        {
            List<string> keys = new List<string>(dict.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
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

            if (dict.Count == 0) return null;
            return dict;
        }

        if (node is IList<object> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object? cleaned = CleanModel(list[i]);
                if (cleaned == null)
                    list.RemoveAt(i);
                else
                    list[i] = cleaned;
            }

            if (list.Count == 0) return null;
            return list;
        }

        return node;
    }
}