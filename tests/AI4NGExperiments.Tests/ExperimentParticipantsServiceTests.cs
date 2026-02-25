using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using Moq;

namespace AI4NGExperiments.Tests;

public class ExperimentParticipantsServiceTests
{
    private const string TableName = "experiments-test";

    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly IExperimentParticipantsService _service;

    public ExperimentParticipantsServiceTests()
    {
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", TableName);
        _service = new TestExperimentParticipantsService(_dynamo.Object, TableName);
    }

    [Fact]
    public async Task GetExperimentMembersAsync_ShouldReturnMembers()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue($"EXPERIMENT#exp-123"),
                    ["SK"] = new AttributeValue("MEMBER#alice"),
                    ["type"] = new AttributeValue("Member"),
                    ["role"] = new AttributeValue("researcher"),
                    ["status"] = new AttributeValue("active"),
                    ["cohort"] = new AttributeValue("A"),
                    ["joinedAt"] = new AttributeValue("2024-01-01T00:00:00Z")
                }
            }
        };

        QueryRequest? captured = null;
        _dynamo.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .Callback<QueryRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetExperimentParticipantsAsync("exp-123");

        // Assert
        _dynamo.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(TableName, captured.TableName);
        Assert.Equal("PK = :pk AND begins_with(SK, :sk)", captured.KeyConditionExpression);
        Assert.Equal("EXPERIMENT#exp-123", captured.ExpressionAttributeValues[":pk"].S);
        Assert.Equal("MEMBER#", captured.ExpressionAttributeValues[":sk"].S);

        var list = result.ToList();
        Assert.Single(list);
        Assert.Equal("alice", list[0].Username);
        Assert.Equal("researcher", list[0].Role);
        Assert.Equal("active", list[0].Status);
        Assert.Equal("A", list[0].Cohort);
        Assert.Equal("2024-01-01T00:00:00Z", list[0].JoinedAt);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldAddMember_WhenValid()
    {
        // Arrange
        PutItemRequest? captured = null;
        _dynamo.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutItemResponse());

        var request = new MemberRequest
        {
            Role = "participant",
            Status = "active",
            Cohort = "B"
        };

        // Act
        await _service.AddParticipantAsync("exp-456", "alice", request, "researcher@user");

        // Assert
        _dynamo.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(TableName, captured.TableName);
        Assert.Equal("EXPERIMENT#exp-456", captured.Item["PK"].S);
        Assert.Equal("MEMBER#alice", captured.Item["SK"].S);
        Assert.Equal("Member", captured.Item["type"].S);
        Assert.Equal("participant", captured.Item["role"].S);
        Assert.Equal("active", captured.Item["status"].S);
        Assert.Equal("B", captured.Item["cohort"].S);
        Assert.Equal("USER#alice", captured.Item["GSI1PK"].S);
        Assert.Equal("EXPERIMENT#exp-456", captured.Item["GSI1SK"].S);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMember_WhenExists()
    {
        // Arrange
        DeleteItemRequest? captured = null;
        _dynamo.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .Callback<DeleteItemRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.RemoveParticipantAsync("exp-789", "bob", "researcher@user");

        // Assert
        _dynamo.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(TableName, captured.TableName);
        Assert.Equal("EXPERIMENT#exp-789", captured.Key["PK"].S);
        Assert.Equal("MEMBER#bob", captured.Key["SK"].S);
    }

    private sealed class TestExperimentParticipantsService : IExperimentParticipantsService
    {
        private readonly IAmazonDynamoDB _dynamo;
        private readonly string _experimentsTable;

        public TestExperimentParticipantsService(IAmazonDynamoDB dynamo, string experimentsTable)
        {
            _dynamo = dynamo;
            _experimentsTable = experimentsTable;
        }

        public async Task AddParticipantAsync(string experimentId, string participantId, MemberRequest request, string performedBy, CancellationToken ct = default)
        {
            var nowIso = DateTime.UtcNow.ToString("O");
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"EXPERIMENT#{experimentId}"),
                ["SK"] = new AttributeValue($"MEMBER#{participantId}"),
                ["type"] = new AttributeValue("Member"),
                ["role"] = new AttributeValue(request.Role ?? string.Empty),
                ["status"] = new AttributeValue(request.Status ?? string.Empty),
                ["cohort"] = new AttributeValue(request.Cohort ?? string.Empty),
                ["createdBy"] = new AttributeValue(performedBy),
                ["createdAt"] = new AttributeValue(nowIso),
                ["updatedBy"] = new AttributeValue(performedBy),
                ["updatedAt"] = new AttributeValue(nowIso),
                ["GSI1PK"] = new AttributeValue($"USER#{participantId}"),
                ["GSI1SK"] = new AttributeValue($"EXPERIMENT#{experimentId}")
            };

            await _dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = _experimentsTable,
                ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
                Item = item
            }, ct);
        }

        public async Task AddParticipantsBatchAsync(string experimentId, IEnumerable<MemberBatchItem> participants, string performedBy, CancellationToken ct = default)
        {
            foreach (var p in participants)
            {
                var req = new MemberRequest { Cohort = p.Cohort, Role = p.Role, Status = p.Status };
                await AddParticipantAsync(experimentId, p.Username, req, performedBy, ct);
            }
        }

        public async Task<IEnumerable<MemberDto>> GetExperimentParticipantsAsync(string experimentId, string? cohort = null, string? status = null, string? role = null, CancellationToken ct = default)
        {
            var resp = await _dynamo.QueryAsync(new QueryRequest
            {
                TableName = _experimentsTable,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue($"EXPERIMENT#{experimentId}"),
                    [":sk"] = new AttributeValue("MEMBER#")
                }
            }, ct);

            var list = new List<MemberDto>();
            foreach (var item in resp.Items)
            {
                var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
                var username = sk.StartsWith("MEMBER#", StringComparison.OrdinalIgnoreCase)
                    ? sk.Substring("MEMBER#".Length)
                    : sk;
                list.Add(new MemberDto
                {
                    Username = username,
                    Role = item.GetValueOrDefault("role")?.S ?? "participant",
                    Status = item.GetValueOrDefault("status")?.S ?? "active",
                    Cohort = item.GetValueOrDefault("cohort")?.S ?? string.Empty,
                    JoinedAt = item.GetValueOrDefault("joinedAt")?.S
                });
            }

            return list;
        }

        public async Task RemoveParticipantAsync(string experimentId, string participantId, string performedBy, CancellationToken ct = default)
        {
            await _dynamo.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _experimentsTable,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue($"EXPERIMENT#{experimentId}"),
                    ["SK"] = new AttributeValue($"MEMBER#{participantId}")
                },
                ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)"
            }, ct);
        }
    }
}
