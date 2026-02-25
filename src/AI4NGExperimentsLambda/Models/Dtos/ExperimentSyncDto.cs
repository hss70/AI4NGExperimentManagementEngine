namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentSyncDto
{
    public ExperimentDto? Experiment { get; set; }
    public List<SessionDto> Sessions { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = new();
    public List<string> Questionnaires { get; set; } = new();
    public List<string> SessionNames { get; set; } = new();
    public List<string> SessionTypes { get; set; } = new();
    public string SyncTimestamp { get; set; } = string.Empty;
}