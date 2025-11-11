using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;

namespace AI4NGExperimentsLambda.Services;

public class TaskService : ITaskService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _experimentsTable;

    public TaskService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<object>> GetTasksAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _experimentsTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Task") }
        });

        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("TASK#", ""),
            data = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]),
            createdAt = item["createdAt"]?.S,
            updatedAt = item["updatedAt"]?.S
        });
    }

    public async Task<object?> GetTaskAsync(string taskId)
    {
        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"TASK#{taskId}"),
                ["SK"] = new("METADATA")
            }
        });

        if (!response.IsItemSet)
            return null;

        return new
        {
            id = taskId,
            data = DynamoDBHelper.ConvertAttributeValueToObject(response.Item["data"]),
            createdAt = response.Item["createdAt"]?.S,
            updatedAt = response.Item["updatedAt"]?.S
        };
    }

    public async Task<object> CreateTaskAsync(CreateTaskRequest request, string username)
    {
        var taskId = Guid.NewGuid().ToString();
        var taskData = new TaskData
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            Configuration = request.Configuration,
            EstimatedDuration = request.EstimatedDuration
        };

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"TASK#{taskId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Task"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(taskData)) },
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O")),
                ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return new { id = taskId };
    }

    public async Task UpdateTaskAsync(string taskId, TaskData data, string username)
    {
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"TASK#{taskId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task DeleteTaskAsync(string taskId, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"TASK#{taskId}"),
                ["SK"] = new("METADATA")
            }
        });
    }
}