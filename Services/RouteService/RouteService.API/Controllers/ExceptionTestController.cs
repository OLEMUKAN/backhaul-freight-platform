using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Required for ValidationException

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExceptionTestController : ControllerBase
    {
        [HttpGet("argument")]
        public IActionResult GetArgumentException() => throw new ArgumentException("Test ArgumentException");

        [HttpGet("unauthorized")]
        public IActionResult GetUnauthorizedAccessException() => throw new UnauthorizedAccessException("Test UnauthorizedAccessException");

        [HttpGet("notfound")]
        public IActionResult GetKeyNotFoundException() => throw new KeyNotFoundException("Test KeyNotFoundException");

        [HttpGet("generic")]
        public IActionResult GetGenericException() => throw new InvalidOperationException("Test GenericException"); // Or any other Exception

        [HttpGet("validation")]
        public IActionResult GetValidationException() => throw new ValidationException("Test ValidationException");
        
        [HttpGet("operationcanceled")]
        public IActionResult GetOperationCanceledException() => throw new OperationCanceledException("Test OperationCanceledException");
    }
}
