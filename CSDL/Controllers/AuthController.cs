using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Microsoft.Extensions.Configuration;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                // Log detailed model state errors for debugging
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Register validation failed for {Email}. Errors: {Errors}", registerDto?.Email, string.Join("; ", errors));
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data", errors));
            }

            var user = await _authService.RegisterAsync(registerDto);
            if (user == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Registration failed. Email may already be in use."));

            return Ok(ApiResponse<UserDto>.SuccessResponse(user, "Registration successful"));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var response = await _authService.LoginAsync(loginDto);
            if (response == null)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid email or password"));

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Login successful"));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            var response = await _authService.RefreshTokenAsync(refreshTokenDto);
            if (response == null)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid token"));

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Token refreshed"));
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.IdToken))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            // Read client id from configuration if not provided
            var clientId = dto.ClientId ?? _configuration["Authentication:Google:ClientId"];
            var response = await _authService.LoginWithGoogleAsync(dto.IdToken, clientId!);
            if (response == null)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Google login failed"));

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Login successful"));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _authService.LogoutAsync(userId);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Logout successful"));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var user = await _authService.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return Ok(ApiResponse<object>.SuccessResponse(null!, "If an account with that email exists, a password reset link has been sent."));
            }

            var token = await _authService.GeneratePasswordResetTokenAsync(user);
            // In a real application, send email with reset link
            // For now, just return success

            return Ok(ApiResponse<object>.SuccessResponse(null!, "If an account with that email exists, a password reset link has been sent."));
        }

        // Two-Factor Authentication endpoints
        [Authorize]
        [HttpPost("2fa/enable")]
        public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _authService.EnableTwoFactorAsync(userId, dto.Password);
            if (result == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to enable 2FA. Check your password."));

            return Ok(ApiResponse<TwoFactorSetupDto>.SuccessResponse(result, "2FA enabled successfully"));
        }

        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _authService.DisableTwoFactorAsync(userId, dto.Password);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to disable 2FA. Check your password."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "2FA disabled successfully"));
        }

        [Authorize]
        [HttpPost("2fa/enable-email")]
        public async Task<IActionResult> EnableTwoFactorByEmail()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _authService.EnableTwoFactorByEmailAsync(userId);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to enable email 2FA"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Email 2FA enabled"));
        }

        [Authorize]
        [HttpGet("2fa/status")]
        public async Task<IActionResult> GetTwoFactorStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var isEnabled = await _authService.IsTwoFactorEnabledAsync(userId);
            return Ok(ApiResponse<object>.SuccessResponse(new { TwoFactorEnabled = isEnabled }, "2FA status retrieved"));
        }

        [HttpPost("login-2fa")]
        public async Task<IActionResult> LoginWithTwoFactor([FromBody] TwoFactorLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var response = await _authService.LoginWithTwoFactorAsync(dto);
            if (response == null)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid credentials or 2FA code"));

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Login successful"));
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var ok = await _authService.SendLoginOtpAsync(dto.Email);
            if (!ok)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to send OTP"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "OTP sent (check email)."));
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var response = await _authService.VerifyLoginOtpAsync(dto.Email, dto.Code);
            if (response == null)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid or expired OTP"));

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Login successful"));
        }
    }
}
