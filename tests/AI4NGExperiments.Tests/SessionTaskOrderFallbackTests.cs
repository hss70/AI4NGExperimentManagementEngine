using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentManagement.Shared;
using Moq;
using Xunit;

namespace AI4NGExperiments.Tests
{
    public class SessionTaskOrderFallbackTests
    {
        private static Dictionary<string, AttributeValue> MakeSessionItem(string experimentId, string sessionId,
            List<string>? topLevelTaskOrder = null,
            Dictionary<string, object>? dataOverrides = null)
        {
            var dataObj = new Dictionary<string, object>
            {
                ["Date"] = sessionId,
                ["SessionType"] = "FIRST",
                ["SessionName"] = "First Session",
                ["Description"] = "",
                ["Status"] = "updated",
                ["Metadata"] = new Dictionary<string, object>()
            };
            if (dataOverrides != null)
            {
                foreach (var kv in dataOverrides)
                    dataObj[kv.Key] = kv.Value;
            }

            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"SESSION#{experimentId}#{sessionId}"),
                ["SK"] = new AttributeValue("METADATA"),
                ["GSI1PK"] = new AttributeValue($"EXPERIMENT#{experimentId}"),
                ["GSI1SK"] = new AttributeValue($"SESSION#{experimentId}#{sessionId}"),
                ["type"] = new AttributeValue("Session"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(dataObj)) },
                ["createdAt"] = new AttributeValue(DateTime.UtcNow.ToString("O")),
                ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
            };

            if (topLevelTaskOrder != null)
            {
                item["taskOrder"] = new AttributeValue { L = topLevelTaskOrder.Select(x => new AttributeValue(x)).ToList() };
            }

            return item;
        }

        private static Dictionary<string, AttributeValue> MakeExperimentMeta(string experimentId)
        {
            var dataObj = new ExperimentData { Name = "X", Description = "Y" };
            return new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"EXPERIMENT#{experimentId}"),
                ["SK"] = new AttributeValue("METADATA"),
                ["type"] = new AttributeValue("Experiment"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(dataObj)) },
                ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() },
                ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
            };
        }

        private static IExperimentService CreateServiceWithMock(IAmazonDynamoDB client)
        {
            return new ExperimentService(client);
        }

        // Note: Some serializers return task order only within the data object. Validate fallback behavior.
        [Fact]
        public async Task GetSessionAsync_FillsTaskOrder_From_Data_WhenTopLevelEmpty()
        {
            var experimentId = "EXP-DATA";
            var sessionId = "2025-12-08";
            var meta = MakeExperimentMeta(experimentId);
            var dataOverrides = new Dictionary<string, object>
            {
                ["TaskOrder"] = new List<string> { "TASK#A", "TASK#B" }
            };
            var sessionItem = MakeSessionItem(experimentId, sessionId, topLevelTaskOrder: new List<string>(), dataOverrides: dataOverrides);

            var mock = new Mock<IAmazonDynamoDB>();
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"EXPERIMENT#{experimentId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = meta });
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"SESSION#{experimentId}#{sessionId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = sessionItem });

            var svc = CreateServiceWithMock(mock.Object);
            var dto = await svc.GetSessionAsync(experimentId, sessionId);
            Assert.NotNull(dto);
            Assert.Equal(new List<string> { "TASK#A", "TASK#B" }, dto!.TaskOrder);
        }

        [Fact]
        public async Task GetSessionAsync_FallsBackToPascalCaseDataTaskOrder_WhenTopLevelEmpty()
        {
            var experimentId = "EXP-PASCAL";
            var sessionId = "2025-12-08";
            var meta = MakeExperimentMeta(experimentId);
            var dataOverrides = new Dictionary<string, object>
            {
                ["TaskOrder"] = new List<string> { "TASK#P1", "TASK#P2" }
            };
            var sessionItem = MakeSessionItem(experimentId, sessionId, topLevelTaskOrder: new List<string>(), dataOverrides: dataOverrides);

            var mock = new Mock<IAmazonDynamoDB>();
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"EXPERIMENT#{experimentId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = meta });
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"SESSION#{experimentId}#{sessionId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = sessionItem });

            var svc = CreateServiceWithMock(mock.Object);
            var dto = await svc.GetSessionAsync(experimentId, sessionId);
            Assert.NotNull(dto);
            Assert.Equal(new List<string> { "TASK#P1", "TASK#P2" }, dto!.TaskOrder);
        }

        [Fact]
        public async Task GetSessionAsync_FallsBackToCamelCaseDataTaskOrder_WhenTopLevelEmpty()
        {
            var experimentId = "EXP-CAMEL";
            var sessionId = "2025-12-08";
            var meta = MakeExperimentMeta(experimentId);
            var dataOverrides = new Dictionary<string, object>
            {
                ["taskOrder"] = new List<string> { "TASK#c1", "TASK#c2" }
            };
            var sessionItem = MakeSessionItem(experimentId, sessionId, topLevelTaskOrder: new List<string>(), dataOverrides: dataOverrides);

            var mock = new Mock<IAmazonDynamoDB>();
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"EXPERIMENT#{experimentId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = meta });
            mock.Setup(m => m.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == $"SESSION#{experimentId}#{sessionId}" && r.Key["SK"].S == "METADATA"), default))
                .ReturnsAsync(new GetItemResponse { Item = sessionItem });

            var svc = CreateServiceWithMock(mock.Object);
            var dto = await svc.GetSessionAsync(experimentId, sessionId);
            Assert.NotNull(dto);
            Assert.Equal(new List<string> { "TASK#c1", "TASK#c2" }, dto!.TaskOrder);
        }

        [Fact]
        public async Task GetExperimentSessionsAsync_Fallbacks_Work_For_Multi_Sessions()
        {
            var experimentId = "EXP-MULTI";
            var meta = MakeExperimentMeta(experimentId);
            var s1 = MakeSessionItem(experimentId, "2025-12-08", topLevelTaskOrder: new List<string>(), dataOverrides: new Dictionary<string, object> { ["TaskOrder"] = new List<string> { "TASK#A" } });
            var s2 = MakeSessionItem(experimentId, "2025-12-09", topLevelTaskOrder: new List<string>(), dataOverrides: new Dictionary<string, object> { ["TaskOrder"] = new List<string> { "TASK#B" } });
            var s3 = MakeSessionItem(experimentId, "2025-12-10", topLevelTaskOrder: new List<string>(), dataOverrides: new Dictionary<string, object> { ["taskOrder"] = new List<string> { "TASK#C" } });

            var mock = new Mock<IAmazonDynamoDB>();
            mock.Setup(m => m.QueryAsync(It.Is<QueryRequest>(q => q.IndexName == "GSI1" && q.ExpressionAttributeValues[":pk"].S == $"EXPERIMENT#{experimentId}"), default))
                .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>> { s1, s2, s3 } });

            var svc = CreateServiceWithMock(mock.Object);
            var list = (await svc.GetExperimentSessionsAsync(experimentId)).ToList();
            Assert.Equal(3, list.Count);
            Assert.Equal(new List<string> { "TASK#A" }, list[0].TaskOrder);
            Assert.Equal(new List<string> { "TASK#B" }, list[1].TaskOrder);
            Assert.Equal(new List<string> { "TASK#C" }, list[2].TaskOrder);
        }
    }
}
