namespace AI4NGExperimentsLambda.Models.Dtos;

public sealed class ProtocolSessionDto
{
    public string ExperimentId { get; set; } = string.Empty;

    // e.g. FIRST / DAILY / WEEKLY / DEMO
    public string ProtocolKey { get; set; } = string.Empty;

    // key into Experiment.data.sessionTypes
    public string SessionTypeKey { get; set; } = string.Empty;

    // ordering for UI and protocol flows (FIRST before DAILY etc.)
    public int Order { get; set; }

    // ONCE | DAILY | WEEKLY | ADHOC
    public string CadenceType { get; set; } = "ONCE";

    // scheduled sessions typically 1; demos may be > 1; null = unlimited (if you want)
    public int? MaxPerDay { get; set; }

    // optional windowing in participant local time, e.g. "09:00", "21:00"
    public string? WindowStartLocal { get; set; }
    public string? WindowEndLocal { get; set; }

    // optional weekly params (choose one pattern)
    public int? Weekday { get; set; } // 1..7 ISO (Mon=1) or 0..6; document which you use

    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}