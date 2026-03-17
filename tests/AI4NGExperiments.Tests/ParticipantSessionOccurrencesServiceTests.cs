using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Mappers;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Constants;
using AI4NGExperimentsLambda.Models.Requests.Participant;
using AI4NGExperimentsLambda.Services.Participant;
using Moq;

namespace AI4NGExperiments.Tests;

public class ParticipantSessionOccurrencesServiceTests
{
    private const string ExperimentsTable = "experiments-test";
    private const string ResponsesTable = "responses-test";
    private const string ExperimentId = "exp-1";
    private const string ParticipantId = "participant-1";
    private const string OccurrenceKey = "DAILY#2026-03-17";

    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly ParticipantSessionOccurrencesService _service;

    public ParticipantSessionOccurrencesServiceTests()
    {
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", ExperimentsTable);
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", ResponsesTable);
        _service = new ParticipantSessionOccurrencesService(_dynamo.Object);
    }

    [Fact]
    public async Task SubmitTaskResponseAsync_ShouldAdvanceAndCompleteOccurrence_WhenFinalTask()
    {
        var occurrence = BuildOccurrence(
            status: OccurrenceStatuses.InProgress,
            currentTaskIndex: 0,
            taskOrder: new List<string> { "task-a" },
            taskStatuses: new List<string> { OccurrenceTaskStatuses.Pending });

        SetupGetItemRouting(occurrence, existingResponseItem: null);

        TransactWriteItemsRequest? capturedTxn = null;
        _dynamo.Setup(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => capturedTxn = request)
            .ReturnsAsync(new TransactWriteItemsResponse());

        var result = await _service.SubmitTaskResponseAsync(
            ExperimentId,
            ParticipantId,
            OccurrenceKey,
            "task-a",
            new SubmitTaskResponseRequest
            {
                ClientSubmissionId = "submission-1",
                Payload = new { score = 42 }
            });

        Assert.Equal(OccurrenceStatuses.Completed, result.Status);
        Assert.Equal(1, result.CurrentTaskIndex);
        Assert.Equal(1, result.CompletedTaskCount);
        Assert.Single(result.TaskState);
        Assert.Equal(OccurrenceTaskStatuses.Completed, result.TaskState[0].Status);
        Assert.NotNull(capturedTxn);
        Assert.Equal(2, capturedTxn.TransactItems.Count);
    }

    [Fact]
    public async Task SubmitTaskResponseAsync_ShouldRejectOutOfOrderTask()
    {
        var occurrence = BuildOccurrence(
            status: OccurrenceStatuses.InProgress,
            currentTaskIndex: 0,
            taskOrder: new List<string> { "task-a", "task-b" },
            taskStatuses: new List<string> { OccurrenceTaskStatuses.Pending, OccurrenceTaskStatuses.Pending });

        SetupGetItemRouting(occurrence, existingResponseItem: null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SubmitTaskResponseAsync(
            ExperimentId,
            ParticipantId,
            OccurrenceKey,
            "task-b",
            new SubmitTaskResponseRequest
            {
                ClientSubmissionId = "submission-1",
                Payload = new { answer = "x" }
            }));

        Assert.Contains("not the current task", ex.Message);
        _dynamo.Verify(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(OccurrenceStatuses.Scheduled)]
    [InlineData(OccurrenceStatuses.Completed)]
    [InlineData(OccurrenceStatuses.Cancelled)]
    public async Task SubmitTaskResponseAsync_ShouldReject_WhenOccurrenceNotInProgress(string status)
    {
        var occurrence = BuildOccurrence(
            status: status,
            currentTaskIndex: 0,
            taskOrder: new List<string> { "task-a" },
            taskStatuses: new List<string> { OccurrenceTaskStatuses.Pending });

        SetupGetItemRouting(occurrence, existingResponseItem: null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SubmitTaskResponseAsync(
            ExperimentId,
            ParticipantId,
            OccurrenceKey,
            "task-a",
            new SubmitTaskResponseRequest
            {
                ClientSubmissionId = "submission-1",
                Payload = new { value = true }
            }));

        Assert.Contains("must be 'in_progress'", ex.Message);
        _dynamo.Verify(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitTaskResponseAsync_ShouldBeIdempotent_ForSameClientSubmissionId_WhenTaskAlreadyCompleted()
    {
        var occurrence = BuildOccurrence(
            status: OccurrenceStatuses.InProgress,
            currentTaskIndex: 1,
            taskOrder: new List<string> { "task-a" },
            taskStatuses: new List<string> { OccurrenceTaskStatuses.Completed });

        var existingResponse = new TaskResponse
        {
            ResponseId = "resp-1",
            ExperimentId = ExperimentId,
            ParticipantId = ParticipantId,
            OccurrenceKey = OccurrenceKey,
            TaskKey = "task-a",
            ClientSubmissionId = "submission-1",
            SubmittedAt = DateTime.UtcNow.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Payload = new { score = 1 }
        };

        SetupGetItemRouting(occurrence, TaskResponseItemMapper.MapToItem(existingResponse));

        var result = await _service.SubmitTaskResponseAsync(
            ExperimentId,
            ParticipantId,
            OccurrenceKey,
            "task-a",
            new SubmitTaskResponseRequest
            {
                ClientSubmissionId = "submission-1",
                Payload = new { score = 1 }
            });

        Assert.Equal(1, result.CurrentTaskIndex);
        Assert.Equal(OccurrenceStatuses.InProgress, result.Status);
        _dynamo.Verify(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitTaskResponseAsync_ShouldRejectDifferentSubmissionId_WhenTaskAlreadyCompleted()
    {
        var occurrence = BuildOccurrence(
            status: OccurrenceStatuses.InProgress,
            currentTaskIndex: 1,
            taskOrder: new List<string> { "task-a" },
            taskStatuses: new List<string> { OccurrenceTaskStatuses.Completed });

        var existingResponse = new TaskResponse
        {
            ResponseId = "resp-1",
            ExperimentId = ExperimentId,
            ParticipantId = ParticipantId,
            OccurrenceKey = OccurrenceKey,
            TaskKey = "task-a",
            ClientSubmissionId = "submission-1",
            SubmittedAt = DateTime.UtcNow.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Payload = new { score = 1 }
        };

        SetupGetItemRouting(occurrence, TaskResponseItemMapper.MapToItem(existingResponse));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitTaskResponseAsync(
            ExperimentId,
            ParticipantId,
            OccurrenceKey,
            "task-a",
            new SubmitTaskResponseRequest
            {
                ClientSubmissionId = "submission-2",
                Payload = new { score = 2 }
            }));

        _dynamo.Verify(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupGetItemRouting(ParticipantSessionOccurrence occurrence, Dictionary<string, AttributeValue>? existingResponseItem)
    {
        var occurrenceItem = OccurrenceItemMapper.MapToItem(occurrence, ParticipantId, ParticipantId);
        var occurrencePk = OccurrenceItemMapper.BuildPk(ExperimentId, ParticipantId);
        var responsePk = TaskResponseItemMapper.BuildPk(ExperimentId, ParticipantId, OccurrenceKey, "task-a");

        _dynamo.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetItemRequest request, CancellationToken _) =>
            {
                var pk = request.Key.GetValueOrDefault("PK")?.S ?? string.Empty;
                var sk = request.Key.GetValueOrDefault("SK")?.S ?? string.Empty;

                if (request.TableName == ExperimentsTable &&
                    pk == $"{DynamoTableKeys.ExperimentPkPrefix}{ExperimentId}" &&
                    sk == $"{DynamoTableKeys.MemberSkPrefix}{ParticipantId}")
                {
                    return new GetItemResponse
                    {
                        IsItemSet = true,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = pk },
                            ["SK"] = new AttributeValue { S = sk }
                        }
                    };
                }

                if (request.TableName == ExperimentsTable &&
                    pk == occurrencePk &&
                    sk == OccurrenceKey)
                {
                    return new GetItemResponse
                    {
                        IsItemSet = true,
                        Item = occurrenceItem
                    };
                }

                if (request.TableName == ResponsesTable &&
                    pk == responsePk &&
                    sk == DynamoTableKeys.MetadataSk &&
                    existingResponseItem != null)
                {
                    return new GetItemResponse
                    {
                        IsItemSet = true,
                        Item = existingResponseItem
                    };
                }

                return new GetItemResponse
                {
                    IsItemSet = false,
                    Item = new Dictionary<string, AttributeValue>()
                };
            });
    }

    private static ParticipantSessionOccurrence BuildOccurrence(
        string status,
        int currentTaskIndex,
        List<string> taskOrder,
        List<string> taskStatuses)
    {
        var nowIso = DateTime.UtcNow.ToString("O");
        return new ParticipantSessionOccurrence
        {
            ExperimentId = ExperimentId,
            ParticipantId = ParticipantId,
            OccurrenceKey = OccurrenceKey,
            SessionTypeKey = "session-a",
            OccurrenceType = OccurrenceTypes.Protocol,
            Status = status,
            TaskOrder = taskOrder,
            TaskState = taskOrder.Select((x, i) => new OccurrenceTaskState
            {
                Order = i + 1,
                TaskKey = x,
                Status = taskStatuses[i],
                IsRequired = true
            }).ToList(),
            CurrentTaskIndex = currentTaskIndex,
            CompletedTaskCount = taskStatuses.Count(x => x == OccurrenceTaskStatuses.Completed),
            TotalTaskCount = taskOrder.Count,
            CreatedAt = nowIso,
            CreatedBy = ParticipantId,
            UpdatedAt = nowIso,
            UpdatedBy = ParticipantId
        };
    }
}
