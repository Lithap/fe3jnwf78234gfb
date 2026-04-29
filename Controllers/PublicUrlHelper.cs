using Microsoft.AspNetCore.Http;

namespace RetroRec_Server.Controllers;

/// <summary>
/// Builds the base URL the game should use for API/WebSocket calls. When the
/// server sits behind ngrok or another reverse proxy, forwarded headers carry
/// the public hostname; hardcoding a stale ngrok domain breaks every client
/// (404 on relationships, chat, hub negotiate) once the tunnel URL changes.
/// </summary>
public static class PublicUrlHelper
{
    public static string GetPublicBaseUrl(HttpRequest request)
    {
        var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var forwardedProto = request.Headers["X-Forwarded-Proto"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedHost))
        {
            var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? "https" : forwardedProto!;
            return $"{scheme}://{forwardedHost.Trim()}";
        }

        return $"{request.Scheme}://{request.Host.Value}";
    }

    public static string GetPublicBaseUrlWithTrailingSlash(HttpRequest request)
    {
        var baseUrl = GetPublicBaseUrl(request).TrimEnd('/');
        return baseUrl + "/";
    }
}
