namespace AI4NGResponsesLambda.Models;

public class QuestionResponse
{
    public string QuestionId { get; set; } = string.Empty;
    public object Answer { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
