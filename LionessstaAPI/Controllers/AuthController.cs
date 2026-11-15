using LionessstaAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
        //
        // Limited to a few attempts per minute per visitor (the "login" policy in
        // Program.cs) so the password can't be guessed by trying thousands of them.
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var adminUsername = _config["Admin:Username"];
            var passwordHash = _config["Admin:PasswordHash"];
            var plainPassword = _config["Admin:Password"];

            var username = request.Username ?? string.Empty;
            var password = request.Password ?? string.Empty;

            var usernameOk = !string.IsNullOrEmpty(adminUsername)
                && PasswordHelper.FixedTimeEquals(username, adminUsername);

            // Preferred: Admin:PasswordHash (see PasswordHelper for how to make one).
            // Admin:Password (plain text) is only read when no hash is configured,
            // so existing setups keep working until the hash is added.
            bool passwordOk;
            if (!string.IsNullOrEmpty(passwordHash))
                passwordOk = PasswordHelper.Verify(password, passwordHash);
            else
                passwordOk = !string.IsNullOrEmpty(plainPassword)
                    && PasswordHelper.FixedTimeEquals(password, plainPassword);

            if (!usernameOk || !passwordOk)
                return Unauthorized(new { message = "Invalid credentials." });

            var token = _jwt.GenerateToken(username);
            return Ok(new { token });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
