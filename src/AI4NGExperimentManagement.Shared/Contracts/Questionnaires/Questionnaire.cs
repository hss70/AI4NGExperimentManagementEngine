namespace AI4NG.ExperimentManagement.Contracts.Questionnaires;

public class QuestionnaireDto
{
    public string Id { get; set; } = string.Empty;
    public QuestionnaireDataDto Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class QuestionnaireDataDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedTime { get; set; }
    public string Version { get; set; } = "1.0";
    public List<QuestionDto> Questions { get; set; } = new();
}

public class QuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public bool Required { get; set; } = true;
    public ScaleDto? Scale { get; set; } = null;
}
public class ScaleDto
{
    public int Min { get; set; }
    public int Max { get; set; }
    public string? MinLabel { get; set; }
    public string? MaxLabel { get; set; }
}

public sealed class CreateQuestionnaireRequest
{
    public string Id { get; set; } = string.Empty;
    public QuestionnaireDataDto Data { get; set; } = new();
}

public sealed class UpdateQuestionnaireRequest
{
    public QuestionnaireDataDto Data { get; set; } = new();
}

public sealed record BatchSummary(int Processed, int Successful, int Failed);

public sealed record BatchItemResult(string Id, string Status, string? Error = null)
{
    public bool Success => Status == "success";
}

public record BatchResult(BatchSummary Summary, List<BatchItemResult> Results);

public sealed class BatchCreateResult
{
    public BatchSummary Summary { get; set; } = new(0, 0, 0);
    public List<BatchItemResult> Results { get; set; } = new();
}