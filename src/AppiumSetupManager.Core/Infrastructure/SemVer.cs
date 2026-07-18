namespace AppiumSetupManager.Core.Infrastructure;

/// <summary>
/// Minimal semantic-version comparison for the Updates screen: is a candidate version strictly
/// newer than what's installed? Deliberately conservative — any input that can't be confidently
/// parsed as a version returns false rather than risk telling the user an update is available when
/// it might not be.
/// </summary>
public static class SemVer
{
    public static bool IsNewer(string? installed, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(installed) || string.IsNullOrWhiteSpace(candidate))
            return false;

        if (!TryParse(installed, out var installedVersion) || !TryParse(candidate, out var candidateVersion))
            return false;

        return candidateVersion > installedVersion;
    }

    private static bool TryParse(string raw, out Version version)
    {
        // Strip a leading 'v' ("v18.2.0" → "18.2.0") and any pre-release/build suffix from the
        // first '-' onward ("2.1.0-beta.3" → "2.1.0") — System.Version has no concept of either.
        var s = raw.Trim().TrimStart('v', 'V');

        var dashIdx = s.IndexOf('-');
        if (dashIdx >= 0)
            s = s[..dashIdx];

        return Version.TryParse(s, out version!);
    }
}
