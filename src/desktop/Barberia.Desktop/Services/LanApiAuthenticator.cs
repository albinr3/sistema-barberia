using Microsoft.AspNetCore.Http;

namespace Barberia.Desktop.Services;

internal static class LanApiAuthenticator
{
    public static bool IsAuthorized(HttpRequest request, string? sharedSecret)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            return true;
        }

        if (!request.Headers.TryGetValue(LanApiContract.DeviceSecretHeader, out var providedSecret))
        {
            return false;
        }

        return string.Equals(providedSecret.ToString(), sharedSecret, StringComparison.Ordinal);
    }
}
