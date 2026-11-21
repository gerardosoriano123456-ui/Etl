using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;

namespace OpinionesDW;

internal sealed class SurveySource(IConfiguration config) : ISource
{
    public string Code => "encuesta";
    public async IAsyncEnumerable<SourceRecord> ReadAsync([EnumeratorCancellation] CancellationToken token)
    {
        using var file = File.OpenText(config["Sources:SurveyCsv"] ?? "fixtures/respuesta_encuesta.csv");
        using var csv = new CsvReader(file, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ExceptionMessagesContainRawData = false,
            DetectColumnCountChanges = true
        });
        if (!await csv.ReadAsync())
            throw new InvalidDataException("CSV sin encabezado.");
        csv.ReadHeader();
        var fields = new[] { "id_respuesta", "id_pregunta", "id_cliente", "id_opinion",
            "valor_numerico", "texto_respuesta", "fecha_respuesta" };
        if (!fields.All(field => csv.HeaderRecord!.Contains(field)))
            throw new InvalidDataException("Faltan columnas de la encuesta.");
        while (await csv.ReadAsync())
        {
            token.ThrowIfCancellationRequested();
            var values = fields.ToDictionary(field => field, field => csv.GetField(field));
            string? id = values["id_respuesta"];
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException("Respuesta sin identificador.");
            yield return new SourceRecord(id, values);
        }
    }
}
