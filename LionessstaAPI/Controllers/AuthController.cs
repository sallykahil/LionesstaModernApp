using LionessstaAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace LionessstaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly JwtHelper _jwt;

        public AuthController(IConfiguration config, JwtHelper jwt)
        {
            _config = config;
            _jwt = jwt;
        }

        // POST /api/auth/login
        // Body: { "username": "admin", "password": "yourpassword" }
        // Returns: { "token": "eyJ..." }
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var adminUsername = _config["Admin:Username"];
            var adminPassword = _config["Admin:Password"];

            if (request.Username != adminUsername || request.Password != adminPassword)
                return Unauthorized(new { message = "Invalid credentials." });

            var token = _jwt.GenerateToken(request.Username);
            return Ok(new { token });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}