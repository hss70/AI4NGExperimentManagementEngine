using AI4NGExperimentsLambda.Models;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGResponsesLambda.Models;

namespace AI4NGExperimentManagementTests.Shared;

public static class TestDataBuilder
{
    public const string TestUserId = "test-id";
    public const string TestUsername = "testuser";
    public const string NonExistentId = "nonexistent";

    public static class Paths
    {
        public const string ResearcherQuestionnaires = "/api/researcher/questionnaires";
        public const string ParticipantQuestionnaires = "/api/questionnaires";
    }

    public static Experiment CreateValidExperiment()
    {
        return new Experiment
        {
            Id = "test-experiment-id",
            Data = new ExperimentData
            {
                Name = "Test Experiment",
                Description = "A test experiment for integration testing",
                Status = "active",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["daily"] = new SessionType
                    {
                        Name = "Daily Session",
                        Questionnaires = new List<string> { "test-questionnaire" },
                        Tasks = new List<string> { "test-task" },
                        EstimatedDuration = 30
                    }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig(),
            Sessions = new List<Session>(),
            CreatedBy = TestUsername,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static QuestionnaireDto CreateValidQuestionnaire()
    {
        return new QuestionnaireDto
        {
            Id = "test-questionnaire-id",
            Data = new QuestionnaireDataDto
            {
                Name = "Test Questionnaire",
                Description = "A test questionnaire for integration testing",
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Id = "1",
                        Text = "First question",
                        Type = "text",
                        Required = true
                    }
                }
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    public static QuestionnaireDataDto CreateValidQuestionnaireData()
    {
        return new QuestionnaireDataDto
        {
            Name = "Valid Questionnaire",
            Description = "Auto-generated valid questionnaire for tests",
            Questions = new List<QuestionDto>
            {
                new QuestionDto
                {
                    Id = "1",
                    Text = "How are you?",
                    Type = "text",
                    Required = true
                }
            }
        };
    }

    public static Response CreateValidResponse()
    {
        return new Response
        {
            Id = "test-response-id",
            Data = new ResponseData
            {
                ExperimentId = "test-experiment-id",
                SessionId = "test-session-id",
                QuestionnaireId = "test-questionnaire-id"
            },
            CreatedBy = TestUsername,
            CreatedAt = DateTime.UtcNow
        };
    }
}