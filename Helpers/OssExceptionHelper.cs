using OssClientPro.Services;

namespace OssClientPro.Helpers;

/// <summary>
/// Maps low-level OSS / network exceptions to user-friendly, localized messages.
/// </summary>
public static class OssExceptionHelper
{
    /// <summary>
    /// Returns a localized, user-friendly message for the given exception.
    /// Falls back to the exception's own message if no mapping is found.
    /// </summary>
    public static string GetFriendlyMessage(Exception ex, LanguageService lang)
    {
        // Drill into aggregate / inner exceptions
        var msg = ex.ToString();

        if (IsMatch(msg, "AccessDenied", "Access Denied", "403"))
            return lang["msg_oss_access_denied"];
        if (IsMatch(msg, "InvalidAccessKeyId", "InvalidAccess", "SignatureDoesNotMatch", "security token"))
            return lang["msg_oss_auth_failure"];
        if (IsMatch(msg, "NoSuchBucket", "NoSuchKey"))
            return lang["msg_oss_no_such_bucket"];

        // A cancelled request is how HttpClient reports its own timeout. Release builds
        // replace resource strings with keys, so match "timedout" as well as "timed out".
        if (ex is OperationCanceledException
            || IsMatch(msg, "timed out", "timedout", "timeout", "net_http_request_timedout"))
            return lang["msg_oss_timeout"];
        if (ex is System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException
            || IsMatch(msg, "timed out", "timeout", "NameResolution", "connection"))
            return lang["msg_oss_network_error"];

        // Return the original error text if nothing matches
        return $"{lang["msg_operation_failed"]} {ex.Message}";
    }

    private static bool IsMatch(string haystack, params string[] needles)
    {
        return needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
