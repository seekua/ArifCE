using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArifCE.Infrastructure;

public sealed class FlexibleEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class FlexibleEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException($"Expected a string for {typeof(TEnum).Name}.");
            var input = reader.GetString() ?? ""; var normalized = input.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
            foreach (var name in Enum.GetNames<TEnum>()) if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase)) return Enum.Parse<TEnum>(name);
            throw new JsonException($"Unknown {typeof(TEnum).Name} value '{input}'.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            var name = value.ToString(); var output = new System.Text.StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++) { if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1])) output.Append('_'); output.Append(char.ToUpperInvariant(name[i])); }
            writer.WriteStringValue(output.ToString());
        }
    }
}
