namespace AI4NGExperimentsLambda.Models;

public class Experiment
{
    public string Id { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ExperimentData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Optional study window (global)
    public string? StudyStartDate { get; set; }  // YYYY-MM-DD
    public string? StudyEndDate { get; set; }

    // Default participant duration (e.g., 56 days)
    public int? ParticipantDurationDays { get; set; }

    public Dictionary<string, SessionType> SessionTypes { get; set; } = new();
}
public class SessionType
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Ordered list — this matters
    public List<string> Tasks { get; set; } = new();

    public int EstimatedDurationMinutes { get; set; }

    // Optional scheduling hint (e.g. DAILY, WEEKLY, CRON, etc.)
    public string? Schedule { get; set; }
}