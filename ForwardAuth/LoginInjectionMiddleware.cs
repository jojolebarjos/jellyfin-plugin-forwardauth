using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.ForwardAuth;

public class LoginInjectionMiddleware
{
    private static readonly Regex HeadOpenTag = new(@"<head\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RequestDelegate _next;

    public LoginInjectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsIndexHtmlRequest(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // The static file handler must return raw, uncompressed bytes for
        // us to patch, and never a 304 that skips the body entirely.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("If-None-Match");
        context.Request.Headers.Remove("If-Modified-Since");

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;

        var contentType = context.Response.ContentType ?? string.Empty;
        if (context.Response.StatusCode != StatusCodes.Status200OK
            || !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            return;
        }

        var html = await new StreamReader(buffer).ReadToEndAsync().ConfigureAwait(false);
        var headOpen = HeadOpenTag.Match(html);
        if (!headOpen.Success)
        {
            await originalBody.WriteAsync(Encoding.UTF8.GetBytes(html)).ConfigureAwait(false);
            return;
        }

        // Inject our custom script, before Jellyfin's own bundle runs
        var scriptTag = $"""<script src="{context.Request.PathBase}/ForwardAuth/inject.js"></script>""";
        var patched = html.Insert(headOpen.Index + headOpen.Length, scriptTag);
        var patchedBytes = Encoding.UTF8.GetBytes(patched);

        // Do not cache, as this would get stale in case of plugin change
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.ContentLength = patchedBytes.Length;
        await originalBody.WriteAsync(patchedBytes).ConfigureAwait(false);
    }

    private static bool IsIndexHtmlRequest(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        return normalized.EndsWith("/web", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }
}
