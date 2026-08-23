using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public AuthController(IUserService userService, IAuthService authService)
        {
            _userService = userService;
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request.Email, request.Password);
            if (response == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<ActionResult<TeamMemberDto>> Register(RegisterRequestDto request)
        {
            try
            {
                var createUserDto = new CreateUserDto
                {
                    Name = request.Name,
                    Email = request.Email,
                    Position = request.Position,
                    Role = "member", // Default role for registration
                    Password = request.Password
                };

                var user = await _userService.CreateUserAsync(createUserDto);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken(RefreshTokenRequestDto request)
        {
            var response = await _authService.RefreshAsync(request.Token);
            if (response == null)
            {
                return Unauthorized();
            }

            return Ok(response);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request)
        {
            // Always 204, whether or not the email exists - the response must not leak which emails
            // are registered.
            await _authService.RequestPasswordResetAsync(request.Email);
            return NoContent();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request)
        {
            var success = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            if (!success)
            {
                return BadRequest(new { message = "Invalid or expired reset link" });
            }

            return NoContent();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(RefreshTokenRequestDto request)
        {
            await _authService.LogoutAsync(request.Token);
            return NoContent();
        }

        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized();
            }

            await _authService.LogoutAllAsync(int.Parse(userIdClaim.Value));
            return NoContent();
        }

        // Returns the current user + their permissions straight from the ClaimsPrincipal - the
        // permissions are already JWT claims (see AuthService.GenerateAccessToken), so this needs no
        // database round-trip for authorization data, only one lookup for the full user record.
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<MeResponseDto>> Me()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserByIdAsync(int.Parse(userIdClaim.Value));
            if (user == null)
            {
                return Unauthorized();
            }

            var permissions = User.FindAll("permission").Select(c => c.Value).ToList();

            return Ok(new MeResponseDto { User = user, Permissions = permissions });
        }
    }
}
