using System.Net.Http.Headers;
using System.Text;
using Hangfire.Dashboard;

namespace ELIBAPI.API.Infrastructure;

public class HangfireAuthFilter(string user, string password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            SetUnauthorized(httpContext);
            return false;
        }

        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
            var parts = credentials.Split(':', 2);
            if (parts.Length == 2 && parts[0] == user && parts[1] == password)
                return true;
        }
        catch { }

        SetUnauthorized(httpContext);
        return false;
    }

    private static void SetUnauthorized(HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
    }
}
