namespace SadBot.Modules.Birthdays;

public sealed class TimeZoneCatalog
{
    private static readonly string[] Popular =
    [
        "Europe/Berlin", "Europe/London", "Europe/Paris", "Europe/Vienna", "Europe/Zurich", "Europe/Amsterdam",
        "Europe/Madrid", "Europe/Rome", "Europe/Warsaw", "Europe/Istanbul", "Europe/Moscow",
        "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "America/Toronto",
        "America/Sao_Paulo", "America/Mexico_City", "Asia/Tokyo", "Asia/Seoul", "Asia/Shanghai",
        "Asia/Kolkata", "Asia/Dubai", "Australia/Sydney", "UTC"
    ];

    private readonly string[] _ids;

    public TimeZoneCatalog()
    {
        _ids = TimeZoneInfo.GetSystemTimeZones()
            .Select(ToIanaId)
            .OfType<string>()
            .Append("UTC")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> Search(string? query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Popular.Where(id => TryResolve(id, out _, out _)).Take(limit).ToArray();

        var needle = query.Trim().Replace(' ', '_');

        return _ids
            .Where(id => id.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => Rank(id, needle))
            .ThenBy(id => id.Length)
            .Take(limit)
            .ToArray();
    }

    public bool TryResolve(string? id, out string canonicalId, out TimeZoneInfo zone)
    {
        canonicalId = string.Empty;
        zone = TimeZoneInfo.Utc;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        var trimmed = id.Trim().Replace(' ', '_');
        var known = _ids.FirstOrDefault(candidate => candidate.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(known ?? trimmed, out var found))
            return false;

        canonicalId = known ?? trimmed;
        zone = found;

        return true;
    }

    private static int Rank(string id, string needle)
    {
        if (id.Equals(needle, StringComparison.OrdinalIgnoreCase))
            return 0;

        var city = id[(id.LastIndexOf('/') + 1)..];

        if (city.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            return 1;

        return id.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    }

    private static string? ToIanaId(TimeZoneInfo zone) =>
        zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null;
}
