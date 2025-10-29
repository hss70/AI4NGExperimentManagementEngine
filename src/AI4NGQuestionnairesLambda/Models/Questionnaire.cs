namespace AI4NGQuestionnairesLambda.Models;

public class Questionnaire
{
    public string Id { get; set; } = string.Empty;
    public QuestionnaireData Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class QuestionnaireData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedTime { get; set; }
    public string Version { get; set; } = "1.0";
    public List<Question> Questions { get; set; } = new();
}

public class Question
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public bool Required { get; set; } = true;
    public Scale? Scale { get; set; } = null;
}
public class Scale
{
    public int Min { get; set; }
    public int Max { get; set; }
}

public class CreateQuestionnaireRequest
{
    public string Id { get; set; } = string.Empty;
    public QuestionnaireData Data { get; set; } = new();
}

public record BatchSummary(int Processed, int Successful, int Failed);

public record BatchItemResult(string Id, string Status, string? Error = null);

public record BatchResult(BatchSummary Summary, List<BatchItemResult> Results);