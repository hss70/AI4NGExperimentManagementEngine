namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentMemberDto
{
    public string UserSub { get; set; } = string.Empty;
    public string Role { get; set; } = "participant";
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;

    public string? ParticipantStartDate { get; set; }
    public string? ParticipantEndDate { get; set; }
    public int? ParticipantDurationDaysOverride { get; set; }
    public string? Timezone { get; set; }
    public string? PseudoId { get; set; }

    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}