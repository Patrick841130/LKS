using System.Text.Json;
using System.Text.Json.Serialization;
using LksBrothers.Core.Models;

namespace LksBrothers.Genesis.Services;

public class UInt256JsonConverter : JsonConverter<UInt256>
{
    public override UInt256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? UInt256.Zero : UInt256.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, UInt256 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class HashJsonConverter : JsonConverter<Hash>
{
    public override Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? Hash.Zero : Hash.FromString(value);
    }

    public override void Write(Utf8JsonWriter writer, Hash value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class AddressJsonConverter : JsonConverter<Address>
{
    public override Address Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? Address.Zero : Address.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
