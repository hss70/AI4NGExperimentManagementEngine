using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Models.Helpers;

public static class ExperimentMemberItemMapper
{
    private const string MemberSkPrefix = "MEMBER#";

    public static ExperimentMemberDto MapMemberDto(Dictionary<string, AttributeValue> item)
    {
        var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
        var participantId = sk.StartsWith(MemberSkPrefix, StringComparison.OrdinalIgnoreCase)
            ? sk.Substring(MemberSkPrefix.Length)
            : sk;

        return new ExperimentMemberDto
        {
            UserSub = participantId,
            Role = item.GetValueOrDefault("role")?.S ?? "participant",
            Status = item.GetValueOrDefault("status")?.S ?? "active",
            Cohort = item.GetValueOrDefault("cohort")?.S ?? string.Empty,
            ParticipantStartDate = item.GetValueOrDefault("participantStartDate")?.S,
            ParticipantEndDate = item.GetValueOrDefault("participantEndDate")?.S,
            ParticipantDurationDaysOverride = TryParseInt(item.GetValueOrDefault("participantDurationDaysOverride")?.N),
            Timezone = item.GetValueOrDefault("timezone")?.S,
            PseudoId = item.GetValueOrDefault("pseudoId")?.S,
            CreatedBy = item.GetValueOrDefault("createdBy")?.S,
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            UpdatedBy = item.GetValueOrDefault("updatedBy")?.S,
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
        };
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }
}