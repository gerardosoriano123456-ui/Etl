using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpinionesDW;

internal record Part(string File, int Rows, string Sha256);
internal record SourceReport(string Source, long Rows, long ElapsedMs, IReadOnlyList<Part> Parts);
internal record BatchManifest(string RunId, DateTimeOffset CompletedUtc, IReadOnlyList<SourceReport> Sources);

internal sealed class BatchWriter(ILogger<BatchWriter> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SourceReport> WriteAsync(ISource source, string pending, int size, CancellationToken token)
    {
        var stopWatch = Stopwatch.StartNew();
        var parts = new List<Part>();
        var rows = new List<SourceRecord>(size);
        long total = 0;
        try
        {
            await foreach (var record in source.ReadAsync(token))
            {
                rows.Add(record);
                total++;
                if (rows.Count == size)
                    await FlushAsync();
            }
            if (rows.Count > 0)
                await FlushAsync();
            log.LogInformation("{Source}: {Rows} registros en {Ms} ms", source.Code, total, stopWatch.ElapsedMilliseconds);
            return new SourceReport(source.Code, total, stopWatch.ElapsedMilliseconds, parts);
        }
        catch (Exception ex)
        {
            log.LogError("Falló {Source} tras {Rows} registros ({Category})",
                source.Code, total, ex.GetType().Name);
            throw;
        }

        async Task FlushAsync()
        {
            var name = $"{source.Code}-{parts.Count + 1:D4}.json";
            var path = Path.Combine(pending, name);
            await SaveJsonAsync(path, rows, token);
            await using var complete = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(complete, token)).ToLowerInvariant();
            parts.Add(new Part(name, rows.Count, hash));
            rows.Clear();
        }
    }

    public Task WriteManifestAsync(string pending, string run, SourceReport[] sources, CancellationToken token) =>
        SaveJsonAsync(Path.Combine(pending, "manifest.json"),
            new BatchManifest(run, DateTimeOffset.UtcNow, sources), token);

    private static async Task SaveJsonAsync<T>(string path, T payload, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 65536, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, payload, Json, token);
    }
}
