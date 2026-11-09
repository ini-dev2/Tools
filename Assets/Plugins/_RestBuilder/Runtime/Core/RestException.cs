using System;

namespace RestBuilder.Runtime
{
    public sealed class RestException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }
        public string Url { get; }

        public RestException(string message, int statusCode, string responseBody, string url, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
            Url = url;
        }
    }
}