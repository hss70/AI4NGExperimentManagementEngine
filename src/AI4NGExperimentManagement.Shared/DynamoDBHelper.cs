using Amazon.DynamoDBv2.Model;
using System.Text.Json;

namespace AI4NGExperimentManagement.Shared;

public static class DynamoDBHelper
{
    public static Dictionary<string, AttributeValue> JsonToAttributeValue(JsonElement element)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonElementToAttributeValue(property.Value);
        }
        return result;
    }

    public static AttributeValue ConvertJsonElementToAttributeValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new AttributeValue(element.GetString()),
            JsonValueKind.Number => new AttributeValue { N = element.GetRawText() },
            JsonValueKind.True or JsonValueKind.False => new AttributeValue { BOOL = element.GetBoolean() },
            JsonValueKind.Array => new AttributeValue { L = element.EnumerateArray().Select(ConvertJsonElementToAttributeValue).ToList() },
            JsonValueKind.Object => new AttributeValue { M = JsonToAttributeValue(element) },
            JsonValueKind.Null => new AttributeValue { NULL = true },
            _ => new AttributeValue(element.GetRawText())
        };
    }

    public static object ConvertAttributeValueToObject(AttributeValue attributeValue)
    {
        if (attributeValue.M != null)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in attributeValue.M)
            {
                result[kvp.Key] = ConvertAttributeValueToObject(kvp.Value);
            }
            return result;
        }
        if (attributeValue.L != null)
            return attributeValue.L.Select(ConvertAttributeValueToObject).ToList();
        if (attributeValue.S != null)
            return attributeValue.S;
        if (attributeValue.N != null)
            return decimal.Parse(attributeValue.N);
        if (attributeValue.BOOL.HasValue)
            return attributeValue.BOOL.Value;
        if (attributeValue.NULL == true)
            return null!;
        return attributeValue.S ?? "";
    }

    public static string GenerateId() => Guid.NewGuid().ToString();

    public static string GetTimestamp() => DateTime.UtcNow.ToString("O");
}