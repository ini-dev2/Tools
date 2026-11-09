namespace RestBuilder.Runtime
{
    public sealed class NullRestLogger : IRestLogger
    {
        public static readonly NullRestLogger Instance = new();
        public void LogRequest(string method, string url, int status, long elapsedMs, string reqBody, string resBody) { }
        public void LogError(string method, string url, int status, long elapsedMs, string reqBody, string resBody, string error) { }
    }
}