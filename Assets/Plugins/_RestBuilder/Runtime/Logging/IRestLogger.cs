namespace RestBuilder.Runtime
{
    public interface IRestLogger
    {
        void LogRequest(string method, string url, int status, long elapsedMs, string reqBody, string resBody);
        void LogError(string method, string url, int status, long elapsedMs, string reqBody, string resBody, string error);
    }
}