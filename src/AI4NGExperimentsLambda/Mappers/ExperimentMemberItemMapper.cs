using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Mappers;

public static class ExperimentMemberItemMapper
{
    private const string ExperimentPkPrefix = "EXPERIMENT#";
    private const string MemberSkPrefix = "MEMBER#";
    private const string MembershipType = "Membership";

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

    public static bool IsActiveParticipantMembership(Dictionary<string, AttributeValue> item)
    {
        var type = item.GetValueOrDefault("type")?.S ?? string.Empty;
        var role = item.GetValueOrDefault("role")?.S ?? string.Empty;
        var status = item.GetValueOrDefault("status")?.S ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, MembershipType, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(role, "participant", StringComparison.OrdinalIgnoreCase)
            && string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsParticipantMembership(Dictionary<string, AttributeValue> item)
    {
        var type = item.GetValueOrDefault("type")?.S ?? string.Empty;
        var role = item.GetValueOrDefault("role")?.S ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, MembershipType, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(role, "participant", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetExperimentId(Dictionary<string, AttributeValue> item)
    {
        var gsi1sk = item.GetValueOrDefault("GSI1SK")?.S ?? string.Empty;
        if (gsi1sk.StartsWith(ExperimentPkPrefix, StringComparison.OrdinalIgnoreCase))
            return gsi1sk.Substring(ExperimentPkPrefix.Length);

        var pk = item.GetValueOrDefault("PK")?.S ?? string.Empty;
        if (pk.StartsWith(ExperimentPkPrefix, StringComparison.OrdinalIgnoreCase))
            return pk.Substring(ExperimentPkPrefix.Length);

        return string.Empty;
    }

    public static string GetParticipantId(Dictionary<string, AttributeValue> item)
    {
        var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
        if (sk.StartsWith(MemberSkPrefix, StringComparison.OrdinalIgnoreCase))
            return sk.Substring(MemberSkPrefix.Length);

        return string.Empty;
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    public static ParticipantMembershipDto MapMembership(
    Dictionary<string, AttributeValue> item,
    string experimentId,
    string participantId)
    {
        return new ParticipantMembershipDto
        {
            ExperimentId = experimentId,
            ParticipantId = participantId,
            Status = item.GetValueOrDefault("status")?.S ?? string.Empty,

            Timezone = item.GetValueOrDefault("timezone")?.S,
            StudyStartDate = item.GetValueOrDefault("studyStartDate")?.S,
            StudyEndDate = item.GetValueOrDefault("studyEndDate")?.S,

            ParticipantDurationDays = TryGetNullableInt(item, "participantDurationDays"),

            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
        };
    }

    private static int? TryGetNullableInt(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr))
            return null;

        if (string.IsNullOrWhiteSpace(attr.N))
            return null;

        return int.TryParse(attr.N, out var value) ? value : null;
    }
}