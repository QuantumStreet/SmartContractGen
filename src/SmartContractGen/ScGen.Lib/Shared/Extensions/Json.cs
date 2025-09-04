namespace ScGen.Lib.Shared.Extensions;

public static class Json
{
    public static object? EthereumConvertJToken(this JToken? token)
    {
        if (token == null) return null;

        switch (token.Type)
        {
            case JTokenType.Object:
                IDictionary<string, object?> dict = new Dictionary<string, object?>();
                foreach (JProperty prop in ((JObject)token).Properties())
                {
                    dict[prop.Name] = EthereumConvertJToken(prop.Value);
                }

                return dict;

            case JTokenType.Array:
                List<object?> list = new List<object?>();
                foreach (JToken item in (JArray)token)
                {
                    list.Add(EthereumConvertJToken(item));
                }

                return list;

            case JTokenType.Integer:
                return ((JValue)token).ToObject<long>();

            case JTokenType.Float:
                return ((JValue)token).ToObject<double>();

            case JTokenType.Boolean:
                return ((JValue)token).ToObject<bool>();

            case JTokenType.Null:
                return null;

            default:
                return ((JValue)token).ToString(CultureInfo.InvariantCulture);
        }
    }

    public static object? EthereumCleanModel(this object? node)
    {
        if (node is IDictionary<string, object?> dict)
        {
            List<string> keys = new List<string>(dict.Keys);
            foreach (string k in keys)
            {
                object? v = dict[k];
                object? cleaned = EthereumCleanModel(v);
                if (cleaned == null)
                {
                    dict.Remove(k);
                }
                else
                {
                    dict[k] = cleaned;
                }
            }

            return dict.Count == 0 ? null : dict;
        }

        if (node is IList<object> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object? cleaned = EthereumCleanModel(list[i]);
                if (cleaned == null)
                    list.RemoveAt(i);
                else
                    list[i] = cleaned;
            }

            return list.Count == 0 ? null : list;
        }

        return node;
    }

    public static string GetEthereumContractName(this JObject jObject)
    {
        string fileName = (jObject["name"]?.ToString() ?? "Contract").Trim();
        if (!fileName.EndsWith(".sol", StringComparison.OrdinalIgnoreCase))
            fileName += ".sol";
        return fileName;
    }
}