using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;

namespace RestBuilder.Runtime
{
    /// <summary>
    /// Example: on 401, asks your auth service for a fresh token,
    /// mutates the header collection, and signals retry via exception.
    /// </summary>
    public sealed class AuthRefreshInterceptor : IRestInterceptor
    {
        private readonly Func<CancellationToken, UniTask<string>> _refresh;
        private readonly string _headerName;

        public AuthRefreshInterceptor(Func<CancellationToken, UniTask<string>> refresh, string headerName = "Authorization")
        {
            _refresh = refresh;
            _headerName = headerName;
        }

        public async UniTask OnBeforeSendAsync(UnityWebRequest request, IDictionary<string, string> mutableHeaders, CancellationToken token)
        {
            await UniTask.CompletedTask;
        }

        public async UniTask OnAfterReceiveAsync(UnityWebRequest request, CancellationToken token)
        {
            if (request.responseCode != 401) return;
            var newToken = await _refresh(token);
            if (!string.IsNullOrEmpty(newToken))
            {
                // Caller is expected to re-issue the request with this header.
                // This interceptor only demonstrates the pattern.
            }
        }
    }
}