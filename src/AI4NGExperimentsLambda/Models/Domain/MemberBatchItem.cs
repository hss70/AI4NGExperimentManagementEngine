namespace AI4NGExperimentsLambda.Models;

public class MemberBatchItem
{
    public string UserSub { get; set; } = string.Empty;
    public string Role { get; set; } = "participant";
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;

    public string? ParticipantStartDate { get; set; }  // YYYY-MM-DD
    public string? ParticipantEndDate { get; set; }    // YYYY-MM-DD
    public int? ParticipantDurationDaysOverride { get; set; }
    public string? Timezone { get; set; }
    public string? PseudoId { get; set; }
}