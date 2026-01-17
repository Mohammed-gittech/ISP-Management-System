
namespace ISP.Application.DTOs
{
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponseDto<T> SuccessResult(T data, string? message = null)
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data,
            };
        }

        public static ApiResponseDto<T> FailureResult(string message, List<string>? errors = null)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Errors = errors,
            };
        }
    }
}