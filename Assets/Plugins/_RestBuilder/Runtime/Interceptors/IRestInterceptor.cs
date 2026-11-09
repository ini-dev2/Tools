using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;

namespace RestBuilder.Runtime
{
    /// <summary>
    /// Hook chain around a UnityWebRequest. Enables auth refresh, metrics, retries, mocks.
    /// </summary>
    public interface IRestInterceptor
    {
        UniTask OnBeforeSendAsync(
            UnityWebRequest request,
            IDictionary<string, string> mutableHeaders,
            CancellationToken token);

        UniTask OnAfterReceiveAsync(
            UnityWebRequest request,
            CancellationToken token);
    }
}