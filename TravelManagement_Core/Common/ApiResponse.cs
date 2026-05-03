namespace TravelManagement.Core.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public IEnumerable<string>? Errors { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> Ok(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            Message = message
        };

        public static ApiResponse<T> Created(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            Message = message ?? "Resource created successfully"
        };

        public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }

    // Non-generic convenience for responses with no data payload
    public static class ApiResponse
    {
        public static ApiResponse<object> Ok(string? message = null) => new()
        {
            Success = true,
            Message = message
        };

        public static ApiResponse<object> Fail(string message, IEnumerable<string>? errors = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}