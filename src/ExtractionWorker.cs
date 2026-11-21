using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpinionesDW;

internal record SourceRecord(string OriginalId, object Payload);

internal interface ISource
{
    string Code { get; }
    IAsyncEnumerable<SourceRecord> ReadAsync(CancellationToken token);
}

internal sealed class ExtractionWorker(IEnumerable<ISource> sources, BatchWriter writer,
    IConfiguration config, ILogger<ExtractionWorker> log, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await Task.Yield();
        string run = Guid.NewGuid().ToString("N");
        var clock = Stopwatch.StartNew();
        string? pending = null;
        try
        {
            var chunkSize = config.GetValue("Staging:RowsPerFile", 250);
            if (chunkSize is < 1 or > 5000)
                throw new ArgumentException("RowsPerFile debe estar entre 1 y 5000.");
            string root = Path.GetFullPath(config["Staging:Directory"] ?? "staging");
            pending = Path.Combine(root, $".pending-{run}");
            Directory.CreateDirectory(pending);

            var reports = await Task.WhenAll(sources.Select(
                source => writer.WriteAsync(source, pending, chunkSize, token)));
            await writer.WriteManifestAsync(pending, run, reports, token);
            token.ThrowIfCancellationRequested();
            Directory.Move(pending, Path.Combine(root, run));
            log.LogInformation("Lote {Run} confirmado: {Total} registros, {Files} partes, {Ms} ms",
                run, reports.Sum(report => report.Rows), reports.Sum(report => report.Parts.Count),
                clock.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Environment.ExitCode = token.IsCancellationRequested ? 2 : 1;
            log.LogError("Lote {Run} descartado ({Category})", run, ex.GetType().Name);
        }
        finally
        {
            try
            {
                if (pending is not null && Directory.Exists(pending))
                    Directory.Delete(pending, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.LogWarning("Temporal pendiente de limpieza: {Run}", run);
            }
            lifetime.StopApplication();
        }
    }
}
