using System;
using System.Collections.Generic;

namespace RestBuilder.Runtime
{
    /// <summary>
    /// Thin factory with shared defaults. Not tied to a single request.
    /// </summary>
    public sealed class RestClient
    {
        private readonly Dictionary<string, string> _defaultHeaders = new();

        public string BaseUrl { get; }
        public IRestLogger Logger { get; }
        public bool LogEnabled { get; }
        public float DefaultTimeout { get; set; } = 30f;
        public int DefaultRetries { get; set; }

        public RestClient(string baseUrl, bool logEnabled = true, IRestLogger logger = null)
        {
            BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            LogEnabled = logEnabled;
            Logger = logger ?? (logEnabled ? new UnityRestLogger() : (IRestLogger)NullRestLogger.Instance);
        }

        public RestClient AddDefaultHeader(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException(nameof(key));
            _defaultHeaders[key] = value;
            return this;
        }

        public RestRequest Request(string endPoint = null)
        {
            var req = new RestRequest()
                .BaseUrl(BaseUrl)
                .Timeout(DefaultTimeout)
                .Retry(DefaultRetries)
                .WithLogger(Logger);

            if (!string.IsNullOrEmpty(endPoint))
                req.EndPoint(endPoint);

            if (!LogEnabled)
                req.NoLog();

            foreach (var kv in _defaultHeaders)
                req.Header(kv.Key, kv.Value);

            return req;
        }
    }
}