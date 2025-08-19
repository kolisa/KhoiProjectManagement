using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
        {
            var isValid = await _userService.ValidateUserCredentialsAsync(request.Email, request.Password);
            if (!isValid)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var user = await _userService.GetUserByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            await _userService.UpdateLastLoginAsync(user.Id);

            var token = GenerateJwtToken(user);

            return Ok(new LoginResponseDto
            {
                Token = token,
                User = user,
                ExpiresAt = DateTime.UtcNow.AddHours(int.Parse(_configuration["Jwt:ExpiryInHours"] ?? "24"))
            });
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
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!);

                var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false, // We don't validate lifetime for refresh
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized();
                }

                var user = await _userService.GetUserByIdAsync(int.Parse(userIdClaim.Value));
                if (user == null)
                {
                    return Unauthorized();
                }

                var newToken = GenerateJwtToken(user);

                return Ok(new LoginResponseDto
                {
                    Token = newToken,
                    User = user,
                    ExpiresAt = DateTime.UtcNow.AddHours(int.Parse(_configuration["Jwt:ExpiryInHours"] ?? "24"))
                });
            }
            catch
            {
                return Unauthorized();
            }
        }

        private string GenerateJwtToken(TeamMemberDto user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddHours(int.Parse(_configuration["Jwt:ExpiryInHours"] ?? "24")),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
