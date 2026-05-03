using Microsoft.AspNetCore.Mvc;
using TravelManagement.Core.Common;

namespace TravelManagement.API.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult ApiOk<T>(T data, string? message = null) =>
            Ok(ApiResponse<T>.Ok(data, message));

        protected IActionResult ApiOk(string? message = null) =>
            Ok(ApiResponse.Ok(message));

        protected IActionResult ApiCreated<T>(T data, string? message = null) =>
            StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Created(data, message));

        protected IActionResult ApiNotFound(string message = "Resource not found") =>
            NotFound(ApiResponse.Fail(message));

        protected IActionResult ApiBadRequest(string message, IEnumerable<string>? errors = null) =>
            BadRequest(ApiResponse.Fail(message, errors));

        protected IActionResult ApiUnauthorized(string message = "Unauthorized") =>
            Unauthorized(ApiResponse.Fail(message));

        protected IActionResult ApiServerError(string message = "An unexpected error occurred") =>
            StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail(message));

        protected IActionResult ApiNoContent() =>
            NoContent();

        protected IActionResult ApiFile(byte[] bytes, string contentType, string fileName) =>
            File(bytes, contentType, fileName);
    }
}