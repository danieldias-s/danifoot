using System.Text.Json;
using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

/// <summary>014B uses the canonical current label while accepting every persisted 013C origin.</summary>
public sealed class WorldPlayerOriginJsonConverter : JsonConverter<WorldPlayerOrigin>
{
    public override WorldPlayerOrigin Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value) && value is >= 0 and <= 2)
            return (WorldPlayerOrigin)value;
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Origem de atleta inválida.");
        return reader.GetString()?.ToUpperInvariant() switch
        {
            "REAL_CURRENT_VERIFIED" or "REAL_VERIFIED_CURRENT" => WorldPlayerOrigin.REAL_CURRENT_VERIFIED,
            "REAL_HISTORICAL_REFERENCE" => WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE,
            "FICTIONAL_GENERATED" => WorldPlayerOrigin.FICTIONAL_GENERATED,
            _ => throw new JsonException("Origem de atleta inválida.")
        };
    }
    public override void Write(Utf8JsonWriter writer, WorldPlayerOrigin value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            WorldPlayerOrigin.REAL_CURRENT_VERIFIED => "REAL_CURRENT_VERIFIED",
            WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE => "REAL_HISTORICAL_REFERENCE",
            WorldPlayerOrigin.FICTIONAL_GENERATED => "FICTIONAL_GENERATED",
            _ => throw new JsonException("Origem de atleta inválida.")
        });
}
