using UnityEngine;

namespace RestBuilder.Runtime
{
    public sealed class UnityRestLogger : IRestLogger
    {
        private const int MaxBodyChars = 2000;

        public void LogRequest(string method, string url, int status, long elapsedMs, string reqBody, string resBody)
        {
            Debug.Log(
                $"[REST] {method} {url} ? {status} ({elapsedMs}ms)" +
                FormatBody("\n  req: ", reqBody) +
                FormatBody("\n  res: ", resBody));
        }

        public void LogError(string method, string url, int status, long elapsedMs, string reqBody, string resBody, string error)
        {
            Debug.LogError(
                $"[REST] {method} {url} ? {status} ({elapsedMs}ms) ERROR: {error}" +
                FormatBody("\n  req: ", reqBody) +
                FormatBody("\n  res: ", resBody));
        }

        private static string FormatBody(string prefix, string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            if (body.Length > MaxBodyChars) body = body.Substring(0, MaxBodyChars) + "…";
            return prefix + body;
        }
    }
}