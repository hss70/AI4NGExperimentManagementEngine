namespace AI4NGExperimentsLambda.Models;

public class MemberRequest
{
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;
}
