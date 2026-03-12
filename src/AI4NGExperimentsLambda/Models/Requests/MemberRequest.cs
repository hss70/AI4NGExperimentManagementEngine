namespace AI4NGExperimentsLambda.Models;

public class ExperimentMemberRequest
{
    public string Role { get; set; } = "participant";
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;

    public string? ParticipantStartDate { get; set; }
    public string? ParticipantEndDate { get; set; }
    public int? ParticipantDurationDaysOverride { get; set; }
    public string? Timezone { get; set; }
    public string? PseudoId { get; set; }
}