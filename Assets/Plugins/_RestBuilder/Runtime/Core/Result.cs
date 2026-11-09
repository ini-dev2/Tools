namespace RestBuilder.Runtime
{
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public string Error { get; }
        public int StatusCode { get; }
        public string ErrorBody { get; }
        public T Data { get; }

        private Result(bool ok, T data, string error, int status, string errorBody)
        {
            IsSuccess = ok;
            Data = data;
            Error = error;
            StatusCode = status;
            ErrorBody = errorBody;
        }

        public static Result<T> Ok(T data) => new(true, data, null, 0, null);

        public static Result<T> Fail(string error, int status = 0, string errorBody = null) =>
            new(false, default, error, status, errorBody);

        public override string ToString() =>
            IsSuccess ? $"OK: {Data}" : $"FAIL({StatusCode}): {Error}";
    }

    public static class Result
    {
        public static Result<Unit> Ok() => Result<Unit>.Ok(Unit.Value);
        public static Result<Unit> Fail(string error, int status = 0, string body = null) =>
            Result<Unit>.Fail(error, status, body);
    }
}