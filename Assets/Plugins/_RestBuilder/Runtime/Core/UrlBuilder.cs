using System;
using System.Text;
using UnityEngine.Networking;

namespace RestBuilder.Runtime
{
    internal static class UrlBuilder
    {
        public static string Build(RequestContext ctx)
        {
            string url = ctx.HasEndPoint
                ? Combine(ctx.BaseUrl, ctx.EndPoint)
                : ctx.BaseUrl;

            if (ctx.PathParams != null && ctx.PathParams.Count > 0)
            {
                foreach (var kv in ctx.PathParams)
                {
                    // Escape so values like "a/b c" don't corrupt the path.
                    url = url.Replace($"{{{kv.Key}}}",
                        UnityWebRequest.EscapeURL(kv.Value ?? string.Empty));
                }
            }

            if (ctx.QueryParams != null && ctx.QueryParams.Count > 0)
            {
                var sb = new StringBuilder(url);
                sb.Append(url.Contains('?') ? '&' : '?');
                bool first = true;
                foreach (var kv in ctx.QueryParams)
                {
                    if (!first) sb.Append('&');
                    first = false;
                    sb.Append(UnityWebRequest.EscapeURL(kv.Key));
                    sb.Append('=');
                    sb.Append(UnityWebRequest.EscapeURL(kv.Value ?? string.Empty));
                }
                url = sb.ToString();
            }

            return url;
        }

        private static string Combine(string baseUrl, string endPoint)
        {
            if (string.IsNullOrEmpty(baseUrl)) return endPoint;
            if (Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, endPoint.TrimStart('/'), out var fullUri))
            {
                return fullUri.ToString();
            }
            return baseUrl.TrimEnd('/') + "/" + endPoint.TrimStart('/');
        }
    }
}