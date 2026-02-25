namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentListDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Role { get; set; }
}
