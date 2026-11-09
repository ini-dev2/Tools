using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RestBuilder.Runtime
{
    /// <summary>
    /// Fluent, single-use request builder. Create one per request.
    /// </summary>
    public sealed class RestRequest
    {
        private string _baseUrl;
        private string _endPoint;
        private readonly Dictionary<string, string> _path = new();
        private readonly Dictionary<string, string> _query = new();
        private readonly Dictionary<string, string> _headers = new();
        private CancellationToken _token = CancellationToken.None;
        private float _timeout = 30f;
        private int _retries;
        private bool _log = true;
        private IRestLogger _logger;

        // ---------- Builder ----------

        public RestRequest BaseUrl(string url) { _baseUrl = url; return this; }
        public RestRequest EndPoint(string endPoint) { _endPoint = endPoint; return this; }
        public RestRequest Path(string key, object value) { _path[key] = value?.ToString() ?? ""; return this; }
        public RestRequest Query(string key, object value) { _query[key] = value?.ToString() ?? ""; return this; }
        public RestRequest Header(string key, string value) { _headers[key] = value; return this; }
        public RestRequest Token(CancellationToken ct) { _token = ct; return this; }
        public RestRequest Timeout(float seconds) { _timeout = seconds; return this; }
        public RestRequest Retry(int count) { _retries = Math.Max(0, count); return this; }
        public RestRequest WithLogger(IRestLogger logger) { _logger = logger; return this; }
        public RestRequest NoLog() { _log = false; return this; }

        // ---------- Execution ----------

        public UniTask<Result<TResponse>> GetAsync<TResponse>() =>
            RestExecutor.SendAsync<TResponse>(HttpMethod.Get, Snapshot(), null);

        public UniTask<Result<TResponse>> PostAsync<TRequest, TResponse>(TRequest body) =>
            RestExecutor.SendAsync<TResponse>(HttpMethod.Post, Snapshot(), body);

        public UniTask<Result<TResponse>> PutAsync<TRequest, TResponse>(TRequest body) =>
            RestExecutor.SendAsync<TResponse>(HttpMethod.Put, Snapshot(), body);

        public UniTask<Result<TResponse>> PatchAsync<TRequest, TResponse>(TRequest body) =>
            RestExecutor.SendAsync<TResponse>(HttpMethod.Patch, Snapshot(), body);

        public UniTask<Result<TResponse>> DeleteAsync<TResponse>() =>
            RestExecutor.SendAsync<TResponse>(HttpMethod.Delete, Snapshot(), null);

        public UniTask<Result<Unit>> PostAsync<TRequest>(TRequest body) =>
            RestExecutor.SendAsync<Unit>(HttpMethod.Post, Snapshot(), body);

        public UniTask<Result<Unit>> DeleteAsync() =>
            RestExecutor.SendAsync<Unit>(HttpMethod.Delete, Snapshot(), null);

        public UniTask<Result<byte[]>> DownloadAsync(Action<float> onProgress = null) =>
            RestExecutor.DownloadAsync(Snapshot(), onProgress);

        public UniTask<Result<FileDownloadResult>> DownloadToFileAsync(
            string filePath,
            Action<float> onProgress = null,
            bool overwrite = true) =>
            RestExecutor.DownloadToFileAsync(Snapshot(), filePath, onProgress, overwrite);

        // ---------- Internals ----------

        private RequestContext Snapshot() => new()
        {
            BaseUrl = _baseUrl,
            EndPoint = _endPoint,
            PathParams = new Dictionary<string, string>(_path),
            QueryParams = new Dictionary<string, string>(_query),
            Headers = new Dictionary<string, string>(_headers),
            Token = _token,
            TimeoutSeconds = _timeout,
            Retries = _retries,
            LogEnabled = _log,
        };

        internal IRestLogger ResolveLogger(IRestLogger fallback) => _logger ?? fallback;
    }
}