using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpinionesDW;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddHttpClient("social").ConfigurePrimaryHttpMessageHandler(
    () => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<ISource, SurveySource>();
builder.Services.AddSingleton<ISource, ReviewSource>();
builder.Services.AddSingleton<ISource, SocialSource>();
builder.Services.AddSingleton<BatchWriter>();
builder.Services.AddHostedService<ExtractionWorker>();
using var host = builder.Build();
await host.RunAsync();
