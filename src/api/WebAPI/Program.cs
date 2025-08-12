WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)
    );
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddServices();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(o => { o.Level = CompressionLevel.Fastest; });

builder.Services.Configure<GzipCompressionProviderOptions>(o => { o.Level = CompressionLevel.Fastest; });

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseResponseCaching();
app.UseResponseCompression();
app.UseExceptionHandler("/error");
app.MapControllers();
app.Run();