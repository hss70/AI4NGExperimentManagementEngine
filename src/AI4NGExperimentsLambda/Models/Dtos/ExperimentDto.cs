namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public string? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
}
public class ExperimentListDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UpdatedAt { get; set; }
}