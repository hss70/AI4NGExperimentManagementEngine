namespace AI4NGResponsesLambda.Models;

public class ResponseData
{
    public string ExperimentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string QuestionnaireId { get; set; } = string.Empty;
    public List<QuestionResponse> Responses { get; set; } = new();
}
