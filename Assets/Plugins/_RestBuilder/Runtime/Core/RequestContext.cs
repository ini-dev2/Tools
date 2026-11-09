using System.Collections.Generic;
using System.Threading;

namespace RestBuilder.Runtime
{
    /// <summary>
    /// Immutable snapshot of everything needed to build a request.
    /// </summary>
    internal sealed class RequestContext
    {
        public string BaseUrl { get; set; }
        public string EndPoint { get; set; }
        public IReadOnlyDictionary<string, string> PathParams { get; set; }
        public IReadOnlyDictionary<string, string> QueryParams { get; set; }
        public IReadOnlyDictionary<string, string> Headers { get; set; }
        public CancellationToken Token { get; set; }
        public float TimeoutSeconds { get; set; }
        public int Retries { get; set; }
        public bool LogEnabled { get; set; }
        public bool HasEndPoint => !string.IsNullOrEmpty(EndPoint);
    }
}