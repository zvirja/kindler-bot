using System.Diagnostics.CodeAnalysis;

namespace KindlerBot.Conversion
{
    internal class CalibreResult
    {
        public static CalibreResult Successful { get; } = new(null);

        public static CalibreResult Failed(string error) => new(error);

        private CalibreResult(string? error)
        {
            this.Error = error;
        }

        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccessful => Error == null;

        public string? Error { get; init; }
    }

    internal class CalibreResult<T>
    {
        public static CalibreResult<T> Successful(T value) => new(value, error: null);

        public static CalibreResult<T> Failed(string error) => new(value: default, error);

        private CalibreResult(T? value, string? error)
        {
            Value = value;
            Error = error;
        }

        [MemberNotNullWhen(false, nameof(Error))]
        [MemberNotNullWhen(true, nameof(Value))]
        public bool IsSuccessful => Error == null;

        public string? Error { get; }

        public T? Value { get; }
    }
}
