using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace OpinionesDW;

internal sealed class SocialSource(IConfiguration config, IHttpClientFactory clients) : ISource
{
    public string Code => "social";
    public async IAsyncEnumerable<SourceRecord> ReadAsync([EnumeratorCancellation] CancellationToken token)
    {
        string? secret = config["Sources:SocialToken"];
        if (!Uri.TryCreate(config["Sources:SocialUrl"], UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !(uri.Scheme == Uri.UriSchemeHttps ||
                (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback && string.IsNullOrEmpty(secret))))
            throw new InvalidOperationException("La API remota requiere HTTPS.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var client = clients.CreateClient("social");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(secret))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(body,
            cancellationToken: timeout.Token))
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                throw new InvalidDataException("Comentario sin identificador.");
            yield return new SourceRecord(id.GetString()!, item);
        }
    }
}
