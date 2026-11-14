using System.IO.Compression;
using System.Text.Json;
using Elifoot.Domain;

namespace Elifoot.Infrastructure;

public sealed record SqliteSnapshotOptions(bool CompressNewWorlds);

internal static class WorldSnapshotCodec
{
    internal const string Prefix = "ELIFOOT:GZIP:1:";

    internal static bool IsCompressed(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    internal static async Task<string> WriteAsync(GameWorld world, JsonSerializerOptions options,
        bool compress, CancellationToken ct)
    {
        if (!compress) return JsonSerializer.Serialize(world, options);
        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            await JsonSerializer.SerializeAsync(gzip, world, options, ct);
        int bytes = checked((int)output.Length);
        int length = checked(Prefix.Length + ((bytes + 2) / 3 * 4));
        return string.Create(length, (Buffer: output.GetBuffer(), Bytes: bytes), static (span, source) =>
        {
            Prefix.AsSpan().CopyTo(span);
            if (!Convert.TryToBase64Chars(source.Buffer.AsSpan(0, source.Bytes), span[Prefix.Length..], out _))
                throw new InvalidOperationException("O buffer Base64 do snapshot tem tamanho inconsistente.");
        });
    }

    internal static async Task<GameWorld?> ReadAsync(string snapshot, JsonSerializerOptions options, CancellationToken ct)
    {
        if (!IsCompressed(snapshot)) return JsonSerializer.Deserialize<GameWorld>(snapshot, options);
        using var input = OpenCompressed(snapshot);
        return await JsonSerializer.DeserializeAsync<GameWorld>(input, options, ct);
    }

    internal static async Task<JsonDocument> ReadDocumentAsync(string snapshot, CancellationToken ct)
    {
        if (!IsCompressed(snapshot)) return JsonDocument.Parse(snapshot);
        using var input = OpenCompressed(snapshot);
        return await JsonDocument.ParseAsync(input, cancellationToken: ct);
    }

    private static GZipStream OpenCompressed(string snapshot)
    {
        var encoded = snapshot.AsSpan(Prefix.Length);
        var payload = new byte[encoded.Length / 4 * 3];
        if (!Convert.TryFromBase64Chars(encoded, payload, out int bytes))
            throw new InvalidDataException("O snapshot SQLite compactado não contém Base64 válido.");
        return new GZipStream(new MemoryStream(payload, 0, bytes, writable: false), CompressionMode.Decompress);
    }
}
