namespace WebAPI.Infrastructure.Services.Rust;

public sealed class RustContractGenerator : IRustContractGenerator
{
    private readonly ILogger<RustContractGenerator> _logger;
    private readonly IHandlebars _handlebars;
    private readonly string _templatePath;

    public RustContractGenerator(ILogger<RustContractGenerator> logger, IHandlebars? handlebars = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlebars = handlebars ?? Handlebars.Create();
        _templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Templates", "RustTemplate.hbs");
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

        jsonContent = WebUtility.HtmlDecode(jsonContent);

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

        IDictionary<string, object?> model = ConvertJToken(jObj) as IDictionary<string, object?> ??
                                             new Dictionary<string, object?>();
        model = CleanModel(model) as IDictionary<string, object?> ?? new Dictionary<string, object?>();

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
            _logger.LogError("Generated Rust code is empty.");
            throw new InvalidDataException("Generated Rust code is empty.");
        }

        string fileName = (jObj["name"]?.ToString() ?? "Contract").Trim();
        if (!fileName.EndsWith(".rs", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".rs";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(rustCode);

        return new GenerateContractResponse
        {
            ContractFileContent = bytes,
            FileName = fileName,
            ContentType = "text/plain"
        };
    }

    private static void RegisterHelpers(IHandlebars hb)
    {
        hb.RegisterHelper("eq",
            (writer, _, args) =>
                writer.WriteSafeString(args.Length >= 2 &&
                                       string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal)
                    ? "true"
                    : ""));
        hb.RegisterHelper("ne",
            (writer, _, args) =>
                writer.WriteSafeString(args.Length >= 2 &&
                                       !string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal)
                    ? "true"
                    : ""));
        hb.RegisterHelper("and",
            (writer, _, args) => writer.WriteSafeString(args.Length > 0 && args.All(IsTruthy) ? "true" : ""));
        hb.RegisterHelper("or", (writer, _, args) => writer.WriteSafeString(args.Length > 0 && args.Any(IsTruthy) ? "true" : ""));
        hb.RegisterHelper("not",
            (writer, _, args) => writer.WriteSafeString(!(args.Length > 0 && IsTruthy(args[0])) ? "true" : ""));
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
        switch (token.Type)
        {
            case JTokenType.Object:
                IDictionary<string, object?> dict = new Dictionary<string, object?>();
                foreach (JProperty prop in ((JObject)token).Properties())
                {
                    dict[prop.Name] = ConvertJToken(prop.Value);
                }

                return dict;
            case JTokenType.Array:
                List<object?> list = new List<object?>();
                foreach (JToken item in (JArray)token)
                {
                    list.Add(ConvertJToken(item));
                }

                return list;
            case JTokenType.Integer: return ((JValue)token).ToObject<long>();
            case JTokenType.Float: return ((JValue)token).ToObject<double>();
            case JTokenType.Boolean: return ((JValue)token).ToObject<bool>();
            case JTokenType.Null: return null;
            default: return ((JValue)token).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static object? CleanModel(object? node)
    {
        if (node is IDictionary<string, object?> dict)
        {
            List<string> keys = new List<string>(dict.Keys);
            foreach (string k in keys)
            {
                object? cleaned = CleanModel(dict[k]);
                if (cleaned == null) dict.Remove(k);
                else dict[k] = cleaned;
            }

            return dict.Count == 0 ? null : dict;
        }

        if (node is IList<object> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object? cleaned = CleanModel(list[i]);
                if (cleaned == null) list.RemoveAt(i);
                else list[i] = cleaned;
            }

            return list.Count == 0 ? null : list;
        }

        return node;
    }
}