#region USINGS

using Cysharp.Threading.Tasks;
using RestBuilder.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

#endregion USINGS

namespace RestBuilder.Samples
{
    // ============================================================
    //  RequestTracker — счётчик живых UnityWebRequest для проверки утечек.
    //  Хуки: NotifyCreated() / NotifyDisposed(req).
    //  Чтобы получать реальные цифры, добавьте в RestExecutor:
    //      var request = Build(...);
    //      RequestTracker.NotifyCreated();
    //      try { ... } finally { RequestTracker.NotifyDisposed(request); request.Dispose(); }
    // ============================================================
    public static class RequestTracker
    {
        public static bool Enabled { get; set; }

        public static int Created;
        public static int Disposed;
        public static int Live => Created - Disposed;

        public static long BytesUploaded;
        public static long BytesDownloaded;

        public static void Reset()
        {
            Created = 0;
            Disposed = 0;
            BytesUploaded = 0;
            BytesDownloaded = 0;
        }

        public static void NotifyCreated()
        {
            if (!Enabled) return;
            Interlocked.Increment(ref Created);
        }

        public static void NotifyDisposed(UnityWebRequest req)
        {
            if (!Enabled || req == null) return;
            Interlocked.Increment(ref Disposed);
            Interlocked.Add(ref BytesUploaded, unchecked((long)req.uploadedBytes));
            Interlocked.Add(ref BytesDownloaded, unchecked((long)req.downloadedBytes));
        }
    }

    // ============================================================
    //  RestTester — MonoBehaviour.
    //  ПКМ по компоненту → "Run All Tests".
    // ============================================================
    public sealed class RestTester : MonoBehaviour
    {
        [Header("Server")]
        [Tooltip("Базовый URL тестового сервера (httpbin, local и т.п.)")]
        public string baseUrl = "https://httpbin.org";

        [Header("Options")]
        public bool enableTracking = true;
        public bool runOnStart;
        public bool verboseLog = true;

        [Header("Timeouts")]
        public float requestTimeoutSeconds = 15f;
        public float cancelAfterSeconds = 0.5f;

        [Header("Scenario Toggles")]
        public bool testGetOk = true;
        public bool testGetArray = true;
        public bool testPostOk = true;
        public bool test4xx = true;
        public bool test5xxRetry = true;
        public bool testTimeout = true;
        public bool testCancellation = true;
        public bool testBadJson = true;
        public bool testDownloadBytes = true;
        public bool testNoEndPoint = true;
        public bool testParallelBurst = true;

        // ---------------- runtime state ----------------

        private readonly List<TestReport> _reports = new();
        private RestClient _client;
        private RestClient _clientBytes;

        private void Start()
        {
            if (runOnStart) RunAllTests().Forget();
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTestsEditor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[RestTester] Play mode is required.");
                return;
            }
            RunAllTests().Forget();
        }

        // ---------------- main runner ----------------

        public async UniTask RunAllTests()
        {
            _reports.Clear();

            if (enableTracking) RequestTracker.Enabled = true;
            RequestTracker.Reset();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long gcBefore = GC.GetTotalMemory(true);

            _client = new RestClient(baseUrl, logEnabled: verboseLog);
            _clientBytes = new RestClient("https://encrypted-tbn0.gstatic.com/images", logEnabled: verboseLog);

            var sw = Stopwatch.StartNew();

            if (testGetOk) await Run("GET 200", GetOkAsync);
            if (testGetArray) await Run("GET array", GetArrayAsync);
            if (testPostOk) await Run("POST 200", PostOkAsync);
            if (test4xx) await Run("GET 404", Get404Async);
            if (test5xxRetry) await Run("GET 500 retry", Get500RetryAsync);
            if (testTimeout) await Run("Timeout", TimeoutAsync);
            if (testCancellation) await Run("Cancellation", CancellationAsync);
            if (testBadJson) await Run("Bad JSON", BadJsonAsync);
            if (testDownloadBytes) await Run("Download bytes", DownloadAsync);
            if (testNoEndPoint) await Run("No endpoint (base URL)", NoEndpointAsync);
            if (testParallelBurst) await Run("Parallel burst x8", ParallelBurstAsync);

            sw.Stop();

            // Дать отложенным Dispose'ам отработать на следующем кадре.
            await UniTask.Delay(200);
            await UniTask.Yield();
            await UniTask.DelayFrame(2);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long gcAfter = GC.GetTotalMemory(true);
            int trackerLive = RequestTracker.Enabled ? RequestTracker.Live : -1;

            PrintSummary(
                totalMs: sw.Elapsed.TotalMilliseconds,
                gcDeltaBytes: gcAfter - gcBefore,
                trackerLive: trackerLive,
                up: RequestTracker.Enabled ? RequestTracker.BytesUploaded : -1,
                down: RequestTracker.Enabled ? RequestTracker.BytesDownloaded : -1);

            if (enableTracking) RequestTracker.Enabled = false;
        }

        private async UniTask Run(string name, Func<UniTask> body)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddReport(name, false, sw.ElapsedMilliseconds, $"EXCEPTION: {ex.Message}");
                return;
            }
            sw.Stop();
        }

        private void AddReport(string name, bool ok, long ms, string detail)
        {
            _reports.Add(new TestReport(name, ok, ms, detail));
            if (verboseLog)
                Debug.Log($"[RestTester] {(ok ? "PASS" : "FAIL")} {name} ({ms}ms) — {detail}");
        }

        // ---------------- scenarios ----------------

        private async UniTask GetOkAsync()
        {
            var sw = Stopwatch.StartNew();
            var r = await _client.Request("/get")
                .Query("q", "hello world")
                .Token(CancellationToken.None)
                .Timeout(requestTimeoutSeconds)
                .GetAsync<HttpBinResponse>();

            sw.Stop();
            bool ok = r.IsSuccess && r.Data?.url != null;
            AddReport("GET 200", ok, sw.ElapsedMilliseconds, ok ? r.Data.url : $"{r.StatusCode}:{r.Error}");
        }

        private async UniTask GetArrayAsync()
        {
            var sw = Stopwatch.StartNew();
            var r = await _client.Request("/json")
                .Timeout(requestTimeoutSeconds)
                .GetAsync<Newtonsoft.Json.Linq.JObject>();
            sw.Stop();
            AddReport("GET array", r.IsSuccess, sw.ElapsedMilliseconds, r.IsSuccess ? "ok" : r.Error);
        }

        private async UniTask PostOkAsync()
        {
            var sw = Stopwatch.StartNew();
            var body = new EchoBody { name = "Bob", age = 42 };
            var r = await _client.Request("/post")
                .Timeout(requestTimeoutSeconds)
                .PostAsync<EchoBody, HttpBinResponse>(body);
            sw.Stop();
            bool ok = r.IsSuccess;
            AddReport("POST 200", ok, sw.ElapsedMilliseconds, ok ? "ok" : $"{r.StatusCode}:{r.Error}");
        }

        private async UniTask Get404Async()
        {
            var sw = Stopwatch.StartNew();
            var r = await _client.Request("/status/404")
                .Retry(0)
                .Timeout(requestTimeoutSeconds)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            bool ok = !r.IsSuccess && r.StatusCode == 404;
            AddReport("GET 404", ok, sw.ElapsedMilliseconds, $"status={r.StatusCode}, bodyLen={r.ErrorBody?.Length ?? 0}");
        }

        private async UniTask Get500RetryAsync()
        {
            var sw = Stopwatch.StartNew();
            var r = await _client.Request("/status/500")
                .Retry(2)
                .Timeout(requestTimeoutSeconds)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            bool ok = !r.IsSuccess && r.StatusCode == 500;
            AddReport("GET 500 retry", ok, sw.ElapsedMilliseconds, $"status={r.StatusCode}, error={r.Error}");
        }

        private async UniTask TimeoutAsync()
        {
            var sw = Stopwatch.StartNew();
            // /delay/5 — httpbin спит 5 сек, timeout 1 сек → должен отвалиться.
            var r = await _client.Request("/delay/5")
                .Timeout(1f)
                .Retry(0)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            bool ok = !r.IsSuccess;
            AddReport("Timeout", ok, sw.ElapsedMilliseconds, $"status={r.StatusCode}, err={r.Error}");
        }

        private async UniTask CancellationAsync()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(cancelAfterSeconds));

            var sw = Stopwatch.StartNew();
            var r = await _client.Request("/delay/5")
                .Timeout(10f)
                .Retry(0)
                .Token(cts.Token)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            cts.Dispose();
            bool ok = !r.IsSuccess && (r.Error?.ToLowerInvariant().Contains("cancel") ?? false);
            AddReport("Cancellation", ok, sw.ElapsedMilliseconds, $"err={r.Error}");
        }

        private async UniTask BadJsonAsync()
        {
            var sw = Stopwatch.StartNew();
            // /xml отдаёт XML, парсим как JSON — ждём deserialize error.
            var r = await _client.Request("/xml")
                .Timeout(requestTimeoutSeconds)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            bool ok = !r.IsSuccess && (r.Error?.Contains("Deserialize") ?? false);
            AddReport("Bad JSON", ok, sw.ElapsedMilliseconds, $"err={r.Error}");
        }

        private async UniTask DownloadAsync()
        {
            float lastProgress = -1;
            int progressEvents = 0;

            var sw = Stopwatch.StartNew();
            var r = await _clientBytes.Request("?q=tbn:ANd9GcSq381npORdnf87TIh-UfavnSj9RwHdZnn3s95_w2ZGcBzSAJaAz6Gp0XXq&s=10") // 256 KB
                .Timeout(requestTimeoutSeconds)
                .DownloadAsync(p =>
                {
                    progressEvents++;
                    lastProgress = p;
                });
            sw.Stop();
            bool ok = r.IsSuccess && progressEvents > 0 && lastProgress > 0.99f;
            AddReport("Download bytes", ok, sw.ElapsedMilliseconds,
                ok ? $"{r.Data.Length} bytes, events={progressEvents}" : $"err={r.Error}");
        }

        private async UniTask NoEndpointAsync()
        {
            var sw = Stopwatch.StartNew();
            // baseUrl + "/get" без endpoint — проверяем, что BuildUrl не падает.
            var r = await _client.Request()
                .BaseUrl(baseUrl + "/get")
                .Timeout(requestTimeoutSeconds)
                .GetAsync<HttpBinResponse>();
            sw.Stop();
            AddReport("No endpoint (base URL)", r.IsSuccess, sw.ElapsedMilliseconds, r.IsSuccess ? "ok" : r.Error);
        }

        private async UniTask ParallelBurstAsync()
        {
            var sw = Stopwatch.StartNew();
            var tasks = new UniTask<Result<HttpBinResponse>>[8];
            for (int i = 0; i < tasks.Length; i++)
            {
                int idx = i;
                tasks[i] = _client.Request("/get")
                    .Query("i", idx)
                    .Timeout(requestTimeoutSeconds)
                    .GetAsync<HttpBinResponse>();
            }

            var results = await UniTask.WhenAll(tasks);
            sw.Stop();

            bool ok = results.All(r => r.IsSuccess);
            AddReport("Parallel burst x8", ok, sw.ElapsedMilliseconds,
                ok ? "all ok" : $"{results.Count(r => !r.IsSuccess)} failed");
        }

        // ---------------- diagnostics ----------------

        private void PrintSummary(
            double totalMs,
            long gcDeltaBytes,
            int trackerLive,
            long up,
            long down)
        {
            int passed = _reports.Count(r => r.Ok);
            int failed = _reports.Count - passed;

            Debug.Log("══════════════════════════════════════════");
            Debug.Log($"[RestTester] SUMMARY  {passed}/{_reports.Count} passed, {failed} failed");
            Debug.Log($"[RestTester] Total: {totalMs:F0} ms");
            Debug.Log($"[RestTester] GC delta: {gcDeltaBytes / 1024.0:F1} KB");
            if (trackerLive >= 0)
                Debug.Log($"[RestTester] Tracked live requests: {trackerLive} (expect 0)");
            if (up >= 0)
                Debug.Log($"[RestTester] Bytes up: {up}, down: {down}");

            foreach (var r in _reports)
                Debug.Log($"[RestTester]  {(r.Ok ? "✔" : "✘")} {r.Name,-24} {r.Ms,6}ms  {r.Detail}");

            if (trackerLive > 0)
                Debug.LogError($"[RestTester] LEAK DETECTED: {trackerLive} UnityWebRequest not disposed!");

            Debug.Log("══════════════════════════════════════════");
        }

        // ---------------- DTOs ----------------

        [Serializable]
        private sealed class EchoBody
        {
            public string name;
            public int age;
        }

        [Serializable]
        private sealed class HttpBinResponse
        {
            public string url;
            public string origin;
            public Newtonsoft.Json.Linq.JObject headers;
        }

        private readonly struct TestReport
        {
            public readonly string Name;
            public readonly bool Ok;
            public readonly long Ms;
            public readonly string Detail;

            public TestReport(string name, bool ok, long ms, string detail)
            {
                Name = name;
                Ok = ok;
                Ms = ms;
                Detail = detail;
            }
        }
    }
}