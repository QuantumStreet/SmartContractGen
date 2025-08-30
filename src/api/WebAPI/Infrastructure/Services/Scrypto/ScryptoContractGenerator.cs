namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractGenerator : IScryptoContractGenerator
{
    private readonly ILogger<ScryptoContractGenerator> _logger;
    private readonly IHandlebars _handlebars;
    private readonly string _templatePath;

    public ScryptoContractGenerator(ILogger<ScryptoContractGenerator> logger, IHandlebars? handlebars = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlebars = handlebars ?? Handlebars.Create();
        _templatePath = Path.Combine(Directory.GetCurrentDirectory(), nameof(Infrastructure), "Templates", "ScryptoTemplate.hbs");
        RegisterHelpers(_handlebars);
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

        string scryptoCode;
        try
        {
            scryptoCode = template(model) ?? string.Empty;
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

        string projectName = (jObj["name"]?.ToString() ?? "scrypto_contract").Trim();
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Create Scrypto project structure
            CreateScryptoProjectStructure(tempDir, projectName);

            // Write the generated code to lib.rs
            string libPath = Path.Combine(tempDir, "src", "lib.rs");
            await File.WriteAllTextAsync(libPath, scryptoCode, token);

            // Update Cargo.toml with project name
            UpdateCargoToml(Path.Combine(tempDir, "Cargo.toml"), projectName);

            // Create zip archive
            string zipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
            ZipFile.CreateFromDirectory(tempDir, zipPath);

            byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, token);

            File.Delete(zipPath);

            GenerateContractResponse response = new GenerateContractResponse
            {
                ContractFileContent = zipBytes,
                FileName = $"{projectName}.zip",
                ContentType = "application/zip"
            };

            return response;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private void CreateScryptoProjectStructure(string tempDir, string projectName)
    {
        // Create directory structure
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        // Create Cargo.toml
        string cargoToml = $@"[package]
name = ""{projectName}""
version = ""0.1.0""
edition = ""2021""

[dependencies]
scrypto = ""{{ git = \""https://github.com/radixdlt/radix-engine"", tag = ""v0.10.0"" }}""
sbor = ""{{ git = \""https://github.com/radixdlt/radix-engine"", tag = ""v0.10.0"" }}""

[lib]
crate-type = [""cdylib""]";

        File.WriteAllText(Path.Combine(tempDir, "Cargo.toml"), cargoToml);

        // Create rust-toolchain.toml
        string toolchainToml = @"[toolchain]
channel = ""1.70""
components = [""rustfmt"", ""clippy""]";

        File.WriteAllText(Path.Combine(tempDir, "rust-toolchain.toml"), toolchainToml);
    }

    private void UpdateCargoToml(string cargoTomlPath, string projectName)
    {
        if (!File.Exists(cargoTomlPath)) return;

        string text = File.ReadAllText(cargoTomlPath);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"name\s*=\s*"".*""", $"name = \"{projectName}\"");
        File.WriteAllText(cargoTomlPath, text);
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

        hb.RegisterHelper("scryptoType", (writer, _, args) =>
        {
            string type = args[0]?.ToString() ?? "";
            string scrypto;
            if (type == "string") scrypto = "String";
            else if (type == "int") scrypto = "i64";
            else if (type == "uint") scrypto = "u64";
            else if (type == "bool") scrypto = "bool";
            else if (type == "decimal") scrypto = "Decimal";
            else if (type == "bytes") scrypto = "Vec<u8>";
            else if (type == "float") scrypto = "Decimal";
            else if (type == "address") scrypto = "ComponentAddress";
            else if (type == "resource") scrypto = "ResourceAddress";
            else scrypto = type;
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
            bool ok = args.Length > 0;
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