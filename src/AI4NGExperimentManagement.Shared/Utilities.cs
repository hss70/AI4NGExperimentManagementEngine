using System.Globalization;

namespace AI4NGExperimentManagement.Shared;

public static class Utilities
{
    public static string GetCurrentTimeStampIso() =>
        ToIso(NowUtc());
    private static DateTimeOffset NowUtc() => DateTimeOffset.UtcNow;

    private static string ToIso(DateTimeOffset dto) =>
        dto.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseIsoOrMin(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DateTimeOffset.MinValue;

        return DateTimeOffset.TryParseExact(
            s,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dto)
            ? dto
            : DateTimeOffset.MinValue;
    }

    public static DateTime ParseIsoUtcDateTimeOrMin(string? s) =>
        ParseIsoOrMin(s).UtcDateTime;
}