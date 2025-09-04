namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed class EthereumContractGenerate : IEthereumContractGenerate
{
    private readonly ILogger<EthereumContractGenerate> _logger;
    private readonly IHandlebars _handlebars;
    private readonly string _handlebarTemplatePath;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EthereumContractGenerate(ILogger<EthereumContractGenerate> logger,
        IHandlebars handlebars, IHttpContextAccessor accessor)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(EthereumContractGenerate),
            accessor.GetId().ToString(), accessor.GetCorrelationId());

        _logger = logger;
        _handlebars = handlebars;
        _httpContextAccessor = accessor;

        _handlebarTemplatePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            KeyNames.HandlebarsTemplates,
            KeyNames.SolidityTemplateHb);

        string scProjectScaffoldPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            KeyNames.ScProjectScaffolds,
            KeyNames.SolidityMainTemplate,
            KeyNames.Contracts);

        _handlebars.EthereumRegisterHelpers();
        ScProjectScaffoldHelper.CreateEthereumProjectTemplate(scProjectScaffoldPath, _logger);

        stopwatch.Stop();
        _logger.OperationCompleted(nameof(EthereumContractGenerate),
            stopwatch.ElapsedMilliseconds, accessor.GetCorrelationId());
    }

    public async Task<Result<GenerateContractResponse>> GenerateAsync(IFormFile jsonFile, CancellationToken token = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.OperationStarted(
            nameof(GenerateAsync),
            _httpContextAccessor.GetId().ToString(),
            _httpContextAccessor.GetCorrelationId());

        if (!jsonFile.IsJsonFile())
        {
            _logger.ValidationFailed(nameof(GenerateAsync),
                Messages.InvalidJsonFile, _httpContextAccessor.GetId().ToString());
            return Result<GenerateContractResponse>.Failure(ResultPatternError.BadRequest(Messages.InvalidJsonFile));
        }

        if (jsonFile.Length == 0)
        {
            _logger.ValidationFailed(nameof(GenerateAsync),
                Messages.EmptyJson, _httpContextAccessor.GetId().ToString());
            return Result<GenerateContractResponse>.Failure(ResultPatternError.BadRequest(Messages.EmptyJson));
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
            _logger.OperationFailedWithException(nameof(GenerateAsync), ex.Message,
                _httpContextAccessor.GetId().ToString(), _httpContextAccessor.GetCorrelationId());
            return Result<GenerateContractResponse>.Failure(ResultPatternError.BadRequest(ex.Message));
        }

        IDictionary<string, object?> model = jObj.EthereumConvertJToken() as IDictionary<string, object?> ??
                                             new Dictionary<string, object?>();


        model = model.EthereumCleanModel() as IDictionary<string, object?> ?? new Dictionary<string, object?>();


        if (!File.Exists(_handlebarTemplatePath))
        {
            _logger.OperationFailed(nameof(GenerateAsync), Messages.HandlebarTemplateNotFound,
                _httpContextAccessor.GetId().ToString(), _httpContextAccessor.GetCorrelationId());
            return Result<GenerateContractResponse>.Failure(
                ResultPatternError.InternalServerError(Messages.HandlebarTemplateNotFound));
        }

        string tplText = await File.ReadAllTextAsync(_handlebarTemplatePath, token).ConfigureAwait(false);

        HandlebarsTemplate<object, object>? template;
        try
        {
            template = _handlebars.Compile(tplText);
        }
        catch (Exception ex)
        {
            _logger.OperationFailedWithException(nameof(GenerateAsync), ex.Message,
                _httpContextAccessor.GetId().ToString(), _httpContextAccessor.GetCorrelationId());
            return Result<GenerateContractResponse>.Failure(ResultPatternError.InternalServerError(ex.Message));
        }

        string solidityCode;
        try
        {
            solidityCode = template(model) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.OperationFailedWithException(nameof(GenerateAsync), ex.Message,
                _httpContextAccessor.GetId().ToString(), _httpContextAccessor.GetCorrelationId());
            return Result<GenerateContractResponse>.Failure(ResultPatternError.InternalServerError(ex.Message));
        }

        if (string.IsNullOrWhiteSpace(solidityCode))
        {
            _logger.OperationFailed(nameof(GenerateAsync), Messages.HandlebarTemplateProcessingError,
                _httpContextAccessor.GetId().ToString(), _httpContextAccessor.GetCorrelationId());
            return Result<GenerateContractResponse>.Failure(
                ResultPatternError.InternalServerError(Messages.HandlebarTemplateProcessingError));
        }

        stopwatch.Stop();
        _logger.OperationCompleted(nameof(GenerateAsync),
            stopwatch.ElapsedMilliseconds, _httpContextAccessor.GetCorrelationId());

        return Result<GenerateContractResponse>.Success(new()
        {
            Content = Encoding.UTF8.GetBytes(solidityCode),
            FileName = jObj.GetEthereumContractName(),
            ContentType = MediaTypeNames.Text.Plain
        });
    }
}