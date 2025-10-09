using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;

namespace AI4NGResponsesLambda.Services;

public class ResponseService : IResponseService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _responsesTable;

    public ResponseService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _responsesTable = Environment.GetEnvironmentVariable("RESPONSES_TABLE") ?? "";
    }

    public async Task<IEnumerable<object>> GetResponsesAsync(string? experimentId = null, string? sessionId = null)
    {
        QueryRequest request;

        if (!string.IsNullOrEmpty(experimentId) && !string.IsNullOrEmpty(sessionId))
        {
            request = new QueryRequest
            {
                TableName = _responsesTable,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :pk AND GSI1SK = :sk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new($"EXPERIMENT#{experimentId}"),
                    [":sk"] = new($"SESSION#{sessionId}")
                }
            };
        }
        else if (!string.IsNullOrEmpty(experimentId))
        {
            request = new QueryRequest
            {
                TableName = _responsesTable,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new($"EXPERIMENT#{experimentId}")
                }
            };
        }
        else
        {
            var scanResponse = await _dynamoClient.ScanAsync(new ScanRequest
            {
                TableName = _responsesTable,
                FilterExpression = "#type = :type",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Response") }
            });

            return scanResponse.Items.Select(item => new
            {
                id = item["PK"].S.Replace("RESPONSE#", ""),
                data = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]),
                createdBy = item["createdBy"]?.S,
                createdAt = item["createdAt"]?.S
            });
        }

        var response = await _dynamoClient.QueryAsync(request);
        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("RESPONSE#", ""),
            data = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]),
            createdBy = item["createdBy"]?.S,
            createdAt = item["createdAt"]?.S
        });
    }

    public async Task<object?> GetResponseAsync(string responseId)
    {
        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _responsesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"RESPONSE#{responseId}"),
                ["SK"] = new("METADATA")
            }
        });

        if (!response.IsItemSet)
            return null;

        return new
        {
            id = responseId,
            data = DynamoDBHelper.ConvertAttributeValueToObject(response.Item["data"]),
            createdBy = response.Item["createdBy"]?.S,
            createdAt = response.Item["createdAt"]?.S
        };
    }

    public async Task<object> CreateResponseAsync(Response response, string username)
    {
        var responseId = Guid.NewGuid().ToString();

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _responsesTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"RESPONSE#{responseId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Response"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(response.Data)) },
                ["GSI1PK"] = new($"EXPERIMENT#{response.Data.ExperimentId}"),
                ["GSI1SK"] = new($"SESSION#{response.Data.SessionId}"),
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return new { id = responseId };
    }

    public async Task UpdateResponseAsync(string responseId, ResponseData data, string username)
    {
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _responsesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"RESPONSE#{responseId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedBy = :user, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":user"] = new(username),
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task DeleteResponseAsync(string responseId, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _responsesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"RESPONSE#{responseId}"),
                ["SK"] = new("METADATA")
            }
        });
    }


}