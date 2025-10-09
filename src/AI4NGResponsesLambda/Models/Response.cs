namespace AI4NGResponsesLambda.Models;

public class Response
{
    public string Id { get; set; } = string.Empty;
    public ResponseData Data { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ResponseData
{
    public string ExperimentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string QuestionnaireId { get; set; } = string.Empty;
    public List<QuestionResponse> Responses { get; set; } = new();
}

public class QuestionResponse
{
    public string QuestionId { get; set; } = string.Empty;
    public object Answer { get; set; } = new();
    public DateTime Timestamp { get; set; }
}