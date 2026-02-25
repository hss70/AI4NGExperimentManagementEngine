namespace AI4NGExperimentsLambda.Models;

public class MemberBatchItem
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "participant";
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;
}
