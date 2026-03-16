public static class Guard
{
    public static string RequireExperimentId(string experimentId)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");
        return experimentId;
    }

    public static string RequireParticipantId(string participantId)
    {
        participantId = (participantId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(participantId))
            throw new ArgumentException("Participant ID is required");
        return participantId;
    }

    public static string RequirePerformedBy(string performedBy)
    {
        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");
        return performedBy;
    }

    public static string RequireOccurrenceKey(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Occurrence key is required");
        return trimmed;
    }

    public static string RequireSessionTypeKey(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("SessionTypeKey is required");
        return trimmed;
    }
}