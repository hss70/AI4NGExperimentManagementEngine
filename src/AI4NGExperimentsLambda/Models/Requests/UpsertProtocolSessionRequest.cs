namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class UpsertProtocolSessionRequest
{
    public string SessionTypeKey { get; set; } = string.Empty;

    public int Order { get; set; }

    // ONCE | DAILY | WEEKLY | ADHOC
    public string CadenceType { get; set; } = "ONCE";

    public int? MaxPerDay { get; set; }

    public string? WindowStartLocal { get; set; }
    public string? WindowEndLocal { get; set; }

    public int? Weekday { get; set; }
}