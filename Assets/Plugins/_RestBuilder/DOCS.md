# RestBuilder

A lightweight, allocation-friendly REST client for Unity built on **UnityWebRequest** + **UniTask**. Fluent request builder, typed `Result<T>`, cancellation, retries with backoff, JSON serialization via Newtonsoft, interceptors, structured logging, byte/file downloads with progress — all in a small surface area.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Core Concepts](#core-concepts)
  - [RestClient](#restclient)
  - [RestRequest](#restrequest)
  - [Result\<T\>](#resultt)
  - [Unit](#unit)
  - [RequestContext](#requestcontext)
- [Requests](#requests)
  - [GET / POST / PUT / PATCH / DELETE](#get--post--put--patch--delete)
  - [Path & Query Parameters](#path--query-parameters)
  - [Headers & Timeouts](#headers--timeouts)
  - [Retries](#retries)
  - [Cancellation](#cancellation)
- [Downloads](#downloads)
  - [Download to bytes](#download-to-bytes)
  - [Download to file](#download-to-file)
- [Endpoints Config](#endpoints-config)
- [Interceptors](#interceptors)
- [Logging](#logging)
- [Errors](#errors)
- [URL Building](#url-building)
- [Limitations](#limitations)
- [Dependencies](#dependencies)
- [License](#license)

---

## Features

- 🧱 **Fluent API** — `client.Request("/users/{id}").Path("id", 42).Query("x", 1).GetAsync<User>()`
- 📦 **Typed results** — `Result<T>` with `IsSuccess`, `StatusCode`, `Error`, `ErrorBody`, `Data`
- ⚡ **UniTask-based** — allocation-friendly async with cancellation support
- 🔁 **Retries with backoff** — configurable per request, only for retryable statuses (0, 408, 429, 5xx)
- 🧵 **Cancellation** — `CancellationToken` is propagated through `SendWebRequest` and `Abort()`
- 🧩 **Interceptors** — `IRestInterceptor` for auth refresh, metrics, retries, mocks
- 📝 **Pluggable logging** — `IRestLogger` with a Unity implementation and a null fallback
- 💾 **Downloads** — to `byte[]` or directly to disk (atomic via `.part` + `File.Move`), with throttled progress
- 🔗 **URL builder** — path templating (`{id}`), query encoding, safe `Uri` combination
- 📚 **Endpoint config** — `EndPointsConfig` `ScriptableObject` to keep base URLs out of code
- 🧪 **Test harness** — `RestTester` MonoBehaviour to exercise 12 scenarios in Play Mode

---

## Requirements

| Component | Version |
|---|---|
| Unity | 2021.3 LTS or newer |
| [UniTask (Cysharp)](https://github.com/Cysharp/UniTask) | 2.x |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) (via `com.unity.nuget.newtonsoft-json`) | 3.x |

---

## Installation

1. Install dependencies via UPM:
   ```json
   {
     "dependencies": {
       "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
       "com.unity.nuget.newtonsoft-json": "3.2.1"
     }
   }
   ```
2. Clone or copy the repository into your project:
   ```bash
   git clone https://github.com/ini-dev2/WebRTCWrapper.git
   ```
3. The `RestBuilder.Runtime` assembly is picked up automatically.

---

## Quick Start

```csharp
using RestBuilder.Runtime;
using Cysharp.Threading.Tasks;

public class UsersApi
{
    private readonly RestClient _client = new RestClient("https://api.example.com", logEnabled: true);

    public UniTask<Result<User>> GetUserAsync(int id) =>
        _client.Request("/users/{id}")
               .Path("id", id)
               .Query("expand", "profile")
               .Timeout(10f)
               .GetAsync<User>();

    public UniTask<Result<User>> CreateUserAsync(User user) =>
        _client.Request("/users")
               .PostAsync<User, User>(user);
}

public class Example
{
    public async UniTask Run(UsersApi api)
    {
        var result = await api.GetUserAsync(42);

        if (result.IsSuccess)
            UnityEngine.Debug.Log($"Got {result.Data}");
        else
            UnityEngine.Debug.LogError($"[{result.StatusCode}] {result.Error}");
    }
}
```

---

## Architecture

```
┌───────────────┐        ┌───────────────┐        ┌────────────────┐
│  RestClient   │───────►│  RestRequest  │───────►│  RestExecutor  │
│ (defaults)    │        │ (fluent build)│        │ (send+retry)   │
└───────────────┘        └───────┬───────┘        └───────┬────────┘
                                 │                        │
                                 ▼                        ▼
                         ┌───────────────┐        ┌────────────────┐
                         │ RequestContext│        │  UrlBuilder    │
                         │  (snapshot)   │        │  Build(ctx)    │
                         └───────────────┘        └────────────────┘
                                 │
                                 ▼
                         ┌───────────────┐
                         │  Result<T>    │
                         │  (Ok / Fail)  │
                         └───────────────┘
```

- `RestClient` holds shared defaults (base URL, headers, timeout, retries, logger).
- `RestRequest` builds a single-use request; `Snapshot()` freezes state into an internal `RequestContext`.
- `RestExecutor` performs the `UnityWebRequest`, applies retries, and returns `Result<T>`.

---

## Core Concepts

### RestClient

A thin factory holding shared defaults. Not tied to a single request.

```csharp
var client = new RestClient("https://api.example.com", logEnabled: true)
    .AddDefaultHeader("X-Api-Key", "secret");

client.DefaultTimeout = 15f;   // seconds
client.DefaultRetries = 2;
```

| Member | Description |
|---|---|
| `BaseUrl` | Base URL for all requests from this client |
| `Logger` | `IRestLogger` (defaults to `UnityRestLogger` or `NullRestLogger`) |
| `LogEnabled` | Global log toggle |
| `DefaultTimeout` | Seconds, default `30` |
| `DefaultRetries` | Retry count for retryable failures, default `0` |
| `AddDefaultHeader(key, value)` | Adds a header applied to every request |
| `Request(endPoint = null)` | Creates a new `RestRequest` with defaults applied |

### RestRequest

Fluent, single-use request builder. Each call to a terminal method (`GetAsync`, `PostAsync`, ...) creates a fresh `RequestContext` snapshot.

| Method | Description |
|---|---|
| `BaseUrl(string)` | Overrides the base URL |
| `EndPoint(string)` | Appends an endpoint path |
| `Path(key, value)` | Replaces `{key}` in the URL with `value` (escaped) |
| `Query(key, value)` | Appends a query string parameter |
| `Header(key, value)` | Adds a header |
| `Token(CancellationToken)` | Attaches a cancellation token |
| `Timeout(seconds)` | Sets per-request timeout |
| `Retry(count)` | Number of retries for retryable failures |
| `WithLogger(IRestLogger)` | Overrides the logger |
| `NoLog()` | Disables logging for this request |

**Terminal methods:**

| Method | Returns |
|---|---|
| `GetAsync<TResponse>()` | `UniTask<Result<TResponse>>` |
| `PostAsync<TRequest, TResponse>(body)` | `UniTask<Result<TResponse>>` |
| `PutAsync<TRequest, TResponse>(body)` | `UniTask<Result<TResponse>>` |
| `PatchAsync<TRequest, TResponse>(body)` | `UniTask<Result<TResponse>>` |
| `DeleteAsync<TResponse>()` | `UniTask<Result<TResponse>>` |
| `PostAsync<TRequest>(body)` | `UniTask<Result<Unit>>` |
| `DeleteAsync()` | `UniTask<Result<Unit>>` |
| `DownloadAsync(onProgress?)` | `UniTask<Result<byte[]>>` |
| `DownloadToFileAsync(path, onProgress?, overwrite?)` | `UniTask<Result<FileDownloadResult>>` |

### Result<T>

A `readonly struct` returned by every terminal method. Never throws for HTTP errors.

```csharp
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public string Error { get; }
    public int StatusCode { get; }
    public string ErrorBody { get; }
    public T Data { get; }

    public static Result<T> Ok(T data);
    public static Result<T> Fail(string error, int status = 0, string errorBody = null);
}
```

Usage:

```csharp
var r = await client.Request("/users/42").GetAsync<User>();

if (r.IsSuccess)
    Use(r.Data);
else
    Debug.LogError($"[{r.StatusCode}] {r.Error}\n{r.ErrorBody}");
```

### Unit

A `readonly struct` used as a placeholder when there is no meaningful response body (e.g., `204 No Content`).

```csharp
var r = await client.Request("/ping").PostAsync<Unit>(body);
if (r.IsSuccess) Debug.Log("OK");
```

### RequestContext

An internal immutable snapshot of everything needed to build a request: `BaseUrl`, `EndPoint`, `PathParams`, `QueryParams`, `Headers`, `Token`, `TimeoutSeconds`, `Retries`, `LogEnabled`. Created by `RestRequest.Snapshot()` and consumed by `RestExecutor`.

---

## Requests

### GET / POST / PUT / PATCH / DELETE

```csharp
// GET
var user = await client.Request("/users/1").GetAsync<User>();

// POST
var created = await client.Request("/users").PostAsync<User, User>(newUser);

// PUT
var updated = await client.Request("/users/1").PutAsync<User, User>(update);

// PATCH
var patched = await client.Request("/users/1").PatchAsync<PartialUser, User>(patch);

// DELETE
var del = await client.Request("/users/1").DeleteAsync();
```

### Path & Query Parameters

Path parameters use `{key}` templates and are escaped with `UnityWebRequest.EscapeURL`. Query parameters are appended with `?` or `&` as needed.

```csharp
var r = await client.Request("/users/{id}/posts")
    .Path("id", 42)
    .Query("page", 1)
    .Query("search", "hello world")
    .GetAsync<Post[]>();
```

### Headers & Timeouts

```csharp
var r = await client.Request("/secure")
    .Header("Authorization", "Bearer token")
    .Timeout(5f)          // seconds
    .GetAsync<Me>();
```

`Timeout` sets `UnityWebRequest.timeout` (in whole seconds, clamped to a minimum of 1).

### Retries

Retries only kick in for **retryable** failures:

- `StatusCode == 0` (network error / timeout)
- `408 Request Timeout`
- `429 Too Many Requests`
- `5xx` server errors

Backoff sequence: `250ms → 500ms → 1000ms → ...` (exponential, attempt-indexed). The total number of attempts is `Retries + 1`.

```csharp
var r = await client.Request("/unstable")
    .Retry(3)
    .GetAsync<Data>();
```

### Cancellation

```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(2));

var r = await client.Request("/slow")
    .Token(cts.Token)
    .GetAsync<Data>();

if (!r.IsSuccess && r.Error?.Contains("Canceled") == true)
    Debug.Log("Canceled");
```

Cancellation propagates via `WithCancellation` and `request.Abort()`. In downloads, cancellation also removes partial files via `DownloadHandlerFile { removeFileOnAbort = true }`.

---

## Downloads

### Download to bytes

```csharp
var r = await client.Request("/file.bin")
    .DownloadAsync(progress => Debug.Log($"Progress {progress:P0}"));

if (r.IsSuccess)
    Debug.Log($"Received {r.Data.Length} bytes");
```

Progress callbacks are throttled: at least **1% change** and **50 ms** between events.

### Download to file

Atomic write: data is streamed to `<file>.part`, then `File.Move`d to the final path on success. On cancellation or failure, the `.part` file is removed.

```csharp
var r = await client.Request("/video.mp4")
    .DownloadToFileAsync(
        filePath: "/tmp/video.mp4",
        onProgress: p => Debug.Log($"{p:P0}"),
        overwrite: true);

if (r.IsSuccess)
    Debug.Log($"Saved {r.Data.Bytes} bytes to {r.Data.FilePath}");
```

`FileDownloadResult`:

```csharp
public readonly struct FileDownloadResult
{
    public string FilePath { get; }
    public long Bytes { get; }
}
```

> **Note:** `DownloadHandlerFile` writes the response body to disk even for 4xx/5xx. `RestExecutor` checks `result != Success` **before** `File.Move`, reads the error body from `.part`, deletes it, and returns `Result.Fail(...)`. This prevents accidentally "saving" an HTML 404 page.

---

## Endpoints Config

`EndPointsConfig` is a `ScriptableObject` that stores named endpoints (base URL + optional path) in the inspector.

```csharp
[CreateAssetMenu(menuName = "RestBuilder/Endpoints Config", fileName = "EndPointsConfig")]
public sealed class EndPointsConfig : ScriptableObject
{
    [Serializable]
    public sealed class Endpoint
    {
        public string Key;
        public string BaseUrl;
        public string EndPoint;
    }

    public IReadOnlyList<Endpoint> Endpoints { get; }
    public Endpoint Get(string key);
    public RestClient CreateClient(string key, bool logEnabled = true);
}
```

Usage:

```csharp
[SerializeField] private EndPointsConfig _endpoints;

void Awake()
{
    var client = _endpoints.CreateClient("Users");
    // client.BaseUrl == endpoint.BaseUrl
}
```

---

## Interceptors

`IRestInterceptor` allows hooking around requests for auth refresh, metrics, retries, mocks, etc.

```csharp
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
```

`AuthRefreshInterceptor` is an example that demonstrates the pattern: on `401`, ask an auth service for a fresh token. **The current implementation only demonstrates the pattern; it does not automatically retry** — the caller is expected to re-issue the request with the refreshed token.

> **Note:** the interceptor plumbing is defined at the type level, but `RestExecutor` does not yet invoke `IRestInterceptor` instances. Wire-up is left to future integration.

---

## Logging

`IRestLogger` is a small, pluggable interface:

```csharp
public interface IRestLogger
{
    void LogRequest(string method, string url, int status, long elapsedMs, string reqBody, string resBody);
    void LogError(string method, string url, int status, long elapsedMs, string reqBody, string resBody, string error);
}
```

| Implementation | Behavior |
|---|---|
| `UnityRestLogger` | Logs to `Debug.Log` / `Debug.LogError` with body truncation at 2000 chars |
| `NullRestLogger.Instance` | No-op singleton |
| `SharedLogger.Default` | Optional global override, consulted by `RestExecutor` |

Priority: `RestRequest.WithLogger(...)` → `RestClient.Logger` → `SharedLogger.Default` → `NullRestLogger.Instance`.

---

## Errors

All failures (HTTP, network, timeout, cancellation, deserialization) are returned as `Result<T>.Fail(...)` — **no exceptions cross the API boundary**. The one exception is `RestException`, an internal type used by `RestExecutor` for retry decisions.

| Scenario | `StatusCode` | `Error` |
|---|---|---|
| Non-success HTTP | `responseCode` | `request.error` or `HTTP <code>` |
| Network error | `0` | `request.error` |
| Timeout | `0` | `Request timeout` / Unity error |
| Canceled | `0` | `Canceled` |
| Deserialize failure | `responseCode` | `Deserialize failed: <message>` |

`RestException` carries `StatusCode`, `ResponseBody`, `Url`, and optional `InnerException` for callers who need details:

```csharp
public sealed class RestException : Exception
{
    public int StatusCode { get; }
    public string ResponseBody { get; }
    public string Url { get; }
}
```

---

## URL Building

`UrlBuilder` combines `BaseUrl` and `EndPoint` safely and applies path/query parameters.

```csharp
string url = UrlBuilder.Build(ctx);
```

Rules:

1. If `EndPoint` is set, `Combine(BaseUrl, EndPoint)` is used; otherwise `BaseUrl` as-is.
2. `Combine` prefers `Uri` combination; falls back to string concatenation with `/` trimming.
3. Path parameters replace `{key}` with `UnityWebRequest.EscapeURL(value)`.
4. Query parameters are appended with `?` or `&`, key and value both escaped.

---

## Limitations

- **Single-use requests.** After calling a terminal method, `RestRequest` should be discarded. Reusing it will reuse the same `CancellationToken` and headers.
- **Interceptors are not wired into the executor yet.** The interface and `AuthRefreshInterceptor` example exist, but `RestExecutor` does not iterate over `IRestInterceptor[]`.
- **`SharedLogger` is a static global.** No lifecycle management; set once at bootstrap.
- **Timeouts are whole seconds.** `UnityWebRequest.timeout` is an `int`; fractional seconds are rounded up to 1s.
- **`DownloadHandlerFile` writes to disk even for 4xx/5xx.** The executor mitigates this by deleting `.part` on failure, but there is a brief window where the file exists.
- **`RestTester` is a sample.** It targets `httpbin.org` and an example image URL. It is meant to be attached to a GameObject in Play Mode, not shipped in production.
- **No request body retry preservation for large uploads.** The body is re-serialized per attempt, but streaming upload handlers are not re-wound.

---

## Dependencies

- [UnityWebRequest](https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html) — underlying HTTP client
- [UniTask](https://github.com/Cysharp/UniTask) — `UniTask`, `UniTask.Delay`, `UniTask.WhenAll`, `WithCancellation`
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — JSON serialization, configurable via `RestExecutor.JsonSettings`

---
