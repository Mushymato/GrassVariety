using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrassVariety;

public class GrassIndexHelper
{
    private enum GrassNameToIndex
    {
        springGrass = 1,
        caveGrass = 2,
        frostGrass = 3,
        lavaGrass = 4,
        caveGrass2 = 5,
        cobweb = 6,
        blueGrass = 7,
    }

    internal static byte GetGrassIndexFromString(string val)
    {
        if (Enum.TryParse(val, ignoreCase: true, out GrassNameToIndex res))
        {
            return (byte)res;
        }
        return 1;
    }
}

public class StringIntListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<int>);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => FromString(token.ToObject<string>()),
            JTokenType.Array => token.ToObject<List<int>>(),
            _ => [token.ToObject<int>()!],
        };
    }

    private static List<int>? FromString(string? strValue)
    {
        if (string.IsNullOrEmpty(strValue))
            return null;
        string[] parts = strValue.Split(',');
        List<int> result = [];
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int variant))
            {
                result.Add(variant);
            }
        }
        return result.Count > 0 ? result : null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class GrassIndexSetConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(HashSet<byte>);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => FromStringList(token.ToObject<string>()?.Split(',')),
            _ => FromStringList(token.ToObject<string[]>()),
        };
    }

    private static HashSet<byte>? FromStringList(string[]? parts)
    {
        if (parts == null)
            return null;
        HashSet<byte> result = [];
        foreach (string val in parts)
        {
            result.Add(GrassIndexHelper.GetGrassIndexFromString(val));
        }
        if (result.Count > 0)
            return result;
        return null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class GrassDestroyColorListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<GrassDestroyColor?>);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        switch (token.Type)
        {
            case JTokenType.String:
            case JTokenType.Array:
            {
                GrassDestroyColor? clr = ToGrassDestroyColor(token);
                return new List<GrassDestroyColor?>() { clr, clr, clr, clr, clr, clr, clr };
            }
            case JTokenType.Object:
            {
                if (token.ToObject<Dictionary<string, JToken>>() is not Dictionary<string, JToken> tokenDict)
                    return null;
                List<GrassDestroyColor?> finalResult = [null, null, null, null, null, null, null];
                foreach ((string key, JToken tok) in tokenDict)
                {
                    finalResult[GrassIndexHelper.GetGrassIndexFromString(key) - 1] = ToGrassDestroyColor(tok);
                }
                return finalResult;
            }
            default:
                return null;
        }
    }

    private static GrassDestroyColor? ToGrassDestroyColor(JToken? tok)
    {
        if (tok == null)
            return null;
        GrassDestroyColor? clr = null;
        switch (tok.Type)
        {
            case JTokenType.String:
                if (tok.ToObject<string>() is string value)
                {
                    clr = value;
                }
                break;
            case JTokenType.Array:
                if (tok.ToObject<string[]>() is string[] valueArr)
                {
                    clr = valueArr;
                }
                break;
        }
        return clr;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
