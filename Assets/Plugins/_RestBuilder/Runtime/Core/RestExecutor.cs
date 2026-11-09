using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using RestBuilder.Samples;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace RestBuilder.Runtime
{
    internal static class RestExecutor
    {
        // Reused serializer settings — tweak if you use custom converters.
        public static JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        private static readonly IRestLogger FallbackLogger = NullRestLogger.Instance;

        // ---------------- JSON / text ----------------

        public static async UniTask<Result<TResponse>> SendAsync<TResponse>(
            HttpMethod method, RequestContext ctx, object body)
        {
            var logger = ResolveLogger(ctx);
            var url = UrlBuilder.Build(ctx);
            var bodyJson = body != null ? JsonConvert.SerializeObject(body, JsonSettings) : null;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            Exception lastEx = null;
            int lastStatus = 0;
            string lastBody = null;

            for (int attempt = 0; attempt <= ctx.Retries; attempt++)
            {
                if (ctx.Token.IsCancellationRequested)
                    return Result<TResponse>.Fail("Canceled", 0);

                using var request = Build(method, url, bodyJson, ctx);

                try
                {
                    await request.SendWebRequest().WithCancellation(ctx.Token);

                    var responseText = request.downloadHandler?.text;
                    lastStatus = (int)request.responseCode;

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        lastBody = responseText;
                        lastEx = new RestException(
                            request.error ?? $"HTTP {lastStatus}",
                            lastStatus, responseText, url);

                        if (IsRetryable(lastStatus) && attempt < ctx.Retries)
                        {
                            await DelayBackoff(attempt, ctx.Token);
                            continue;
                        }
                        break;
                    }

                    sw.Stop();
                    LogSuccess(logger, ctx, method, url, lastStatus, sw.ElapsedMilliseconds, bodyJson, responseText);

                    // 204 / empty body
                    if (typeof(TResponse) == typeof(Unit))
                        return Result<TResponse>.Ok((TResponse)(object)Unit.Value);

                    if (string.IsNullOrWhiteSpace(responseText))
                        return Result<TResponse>.Ok(default);

                    try
                    {
                        var data = JsonConvert.DeserializeObject<TResponse>(responseText, JsonSettings);
                        return Result<TResponse>.Ok(data);
                    }
                    catch (Exception deser)
                    {
                        return Result<TResponse>.Fail(
                            $"Deserialize failed: {deser.Message}", lastStatus, responseText);
                    }
                }
                catch (UnityWebRequestException uwex)
                {
                    var status = (int)(uwex.ResponseCode);
                    var b = uwex.Text;
                    return Result<TResponse>.Fail(uwex.Message, status, b);
                }
                catch (OperationCanceledException)
                {
                    return Result<TResponse>.Fail("Canceled", 0);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (attempt < ctx.Retries)
                    {
                        await DelayBackoff(attempt, ctx.Token);
                        continue;
                    }
                    break;
                }
            }

            sw.Stop();
            LogFailure(logger, ctx, method, url, lastStatus, sw.ElapsedMilliseconds, bodyJson, lastBody, lastEx?.Message);
            return Result<TResponse>.Fail(lastEx?.Message ?? "Unknown error", lastStatus, lastBody);
        }

        // ---------------- bytes / download ----------------

        public static async UniTask<Result<byte[]>> DownloadAsync(
     RequestContext ctx, Action<float> onProgress = null)
        {
            var logger = ResolveLogger(ctx);
            var url = UrlBuilder.Build(ctx);
            var sw = Stopwatch.StartNew();

            var request = Build(HttpMethod.Get, url, null, ctx);
            RequestTracker.NotifyCreated();

            using var reg = ctx.Token.Register(() => { try { request.Abort(); } catch { } });

            try
            {
                var op = request.SendWebRequest();

                const float MinDelta = 0.01f;
                const long MinIntervalMs = 50;

                float lastReported = -1f;
                long lastReportedMs = -MinIntervalMs;

                while (!op.isDone)
                {
                    if (onProgress != null)
                    {
                        float p = request.downloadProgress;
                        long nowMs = sw.ElapsedMilliseconds;

                        bool deltaOk = p - lastReported >= MinDelta;
                        bool timeOk = nowMs - lastReportedMs >= MinIntervalMs;

                        if (deltaOk && timeOk)
                        {
                            lastReported = p;
                            lastReportedMs = nowMs;
                            onProgress(p);
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ctx.Token);
                }
                // ────────────────────────────────────────────────

                await op.WithCancellation(ctx.Token);

                if (onProgress != null && lastReported < 1f)
                {
                    onProgress(1f);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var status = (int)request.responseCode;
                    var errBody = request.downloadHandler?.text;
                    return Result<byte[]>.Fail(request.error ?? $"HTTP {status}", status, errBody);
                }

                return Result<byte[]>.Ok(request.downloadHandler?.data);
            }
            catch (OperationCanceledException)
            {
                return Result<byte[]>.Fail("Canceled");
            }
            catch (Exception ex)
            {
                return Result<byte[]>.Fail(ex.Message,
                    (int)request.responseCode,
                    request.downloadHandler?.text);
            }
            finally
            {
                RequestTracker.NotifyDisposed(request);
                request.Dispose();
            }
        }

        public static async UniTask<Result<FileDownloadResult>> DownloadToFileAsync(
    RequestContext ctx,
    string filePath,
    Action<float> onProgress = null,
    bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Result<FileDownloadResult>.Fail("filePath is null or empty");

            var logger = ResolveLogger(ctx);
            var url = UrlBuilder.Build(ctx);
            var sw = Stopwatch.StartNew();

            // Атомарная запись: качаем в <file>.part, при успехе — File.Move.
            var targetDir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(targetDir))
                System.IO.Directory.CreateDirectory(targetDir);

            if (System.IO.File.Exists(filePath) && !overwrite)
                return Result<FileDownloadResult>.Fail($"File already exists: {filePath}");

            var partPath = filePath + ".part";

            // На случай, если предыдущий прогон упал до удаления.
            TryDelete(partPath);

            var request = Build(HttpMethod.Get, url, null, ctx);
            RequestTracker.NotifyCreated();

            // 🔴 Ключевое: DownloadHandlerFile + removeFileOnAbort.
            // removeFileOnAbort = true → Unity сам удалит part-файл при Abort().
            // Это защищает от «повисшего» .part при отмене через токен.
            request.downloadHandler = new DownloadHandlerFile(partPath) { removeFileOnAbort = true };

            using var reg = ctx.Token.Register(() => { try { request.Abort(); } catch { } });

            try
            {
                var op = request.SendWebRequest();

                const float MinDelta = 0.01f;
                const long MinIntervalMs = 50;

                float lastReported = -1f;
                long lastReportedMs = -MinIntervalMs;

                while (!op.isDone)
                {
                    if (onProgress != null)
                    {
                        float p = request.downloadProgress;
                        long nowMs = sw.ElapsedMilliseconds;

                        if (p - lastReported >= MinDelta && nowMs - lastReportedMs >= MinIntervalMs)
                        {
                            lastReported = p;
                            lastReportedMs = nowMs;
                            onProgress(p);
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ctx.Token);
                }

                await op.WithCancellation(ctx.Token);

                if (onProgress != null && lastReported < 1f)
                    onProgress(1f);

                // 🔴 Проверка результата ДО File.Move.
                // DownloadHandlerFile пишет тело ответа в файл даже при 4xx/5xx,
                // поэтому без этой проверки вы бы «успешно» сохранили HTML-страницу 404.
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var status = (int)request.responseCode;
                    var errBody = TryReadAllText(partPath);   // тело ошибки — из .part
                    TryDelete(partPath);

                    LogFailure(logger, ctx, HttpMethod.Get, url, status,
                        sw.ElapsedMilliseconds, null, errBody, request.error);

                    return Result<FileDownloadResult>.Fail(
                        request.error ?? $"HTTP {status}", status, errBody);
                }

                // 🔴 Атомарная замена. File.Move на Windows не перезаписывает,
                // поэтому delete + move; на Unix — Move(overwrite=true) делается
                // через File.Replace или предварительный delete. Делаем delete+move
                // единообразно.
                if (System.IO.File.Exists(filePath))
                {
                    if (!overwrite)
                    {
                        TryDelete(partPath);
                        return Result<FileDownloadResult>.Fail($"File already exists: {filePath}");
                    }
                    System.IO.File.Delete(filePath);
                }

                System.IO.File.Move(partPath, filePath);

                long bytes;
                try { bytes = new System.IO.FileInfo(filePath).Length; }
                catch { bytes = -1; }

                sw.Stop();
                LogSuccess(logger, ctx, HttpMethod.Get, url, (int)request.responseCode,
                    sw.ElapsedMilliseconds, null, $"<saved {bytes} bytes to {filePath}>");

                return Result<FileDownloadResult>.Ok(
                    new FileDownloadResult(filePath, bytes));
            }
            catch (OperationCanceledException)
            {
                TryDelete(partPath);
                return Result<FileDownloadResult>.Fail("Canceled");
            }
            catch (Exception ex)
            {
                TryDelete(partPath);
                return Result<FileDownloadResult>.Fail(ex.Message,
                    (int)request.responseCode,
                    null);
            }
            finally
            {
                RequestTracker.NotifyDisposed(request);
                request.Dispose();
            }
        }

        // ---------------- helpers ----------------
        private static void TryDelete(string path)
        {
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch { /* best effort */ }
        }

        private static string TryReadAllText(string path)
        {
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static UnityWebRequest Build(HttpMethod method, string url, string bodyJson, RequestContext ctx)
        {
            var verb = method.ToVerb();
            var request = new UnityWebRequest(url, verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = (int)Math.Max(1, ctx.TimeoutSeconds)
            };

            if (bodyJson != null)
            {
                var raw = Encoding.UTF8.GetBytes(bodyJson);
                request.uploadHandler = new UploadHandlerRaw(raw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (ctx.Headers != null)
            {
                foreach (var kv in ctx.Headers)
                {
                    if (!string.IsNullOrEmpty(kv.Value))
                        request.SetRequestHeader(kv.Key, kv.Value);
                }
            }

            return request;
        }

        private static bool IsRetryable(int status) =>
            status == 0 || status == 408 || status == 429 || (status >= 500 && status <= 599);

        private static UniTask DelayBackoff(int attempt, CancellationToken token)
        {
            // 0.25s, 0.5s, 1s, ...
            int ms = (int)(250 * Math.Pow(2, attempt));
            return UniTask.Delay(ms, cancellationToken: token);
        }

        private static IRestLogger ResolveLogger(RequestContext ctx) =>
            ctx.LogEnabled ? (SharedLogger.Default ?? FallbackLogger) : (IRestLogger)NullRestLogger.Instance;

        private static void LogSuccess(IRestLogger logger, RequestContext ctx, HttpMethod m, string url,
            int status, long ms, string reqBody, string resBody)
        {
            if (ctx.LogEnabled)
                logger?.LogRequest(m.ToVerb(), url, status, ms, reqBody, resBody);
        }

        private static void LogFailure(IRestLogger logger, RequestContext ctx, HttpMethod m, string url,
            int status, long ms, string reqBody, string resBody, string error)
        {
            if (ctx.LogEnabled)
                logger?.LogError(m.ToVerb(), url, status, ms, reqBody, resBody, error);
        }
    }
}