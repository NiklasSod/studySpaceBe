using AutoMapper;
using lmsPortalBe.DTOs.Auth;
using lmsPortalBe.Models;
using lmsPortalBe.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace lmsPortalBe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IMapper mapper,
        IWebHostEnvironment environment,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<AuthController> logger) : ControllerBase
    {
        public const string PasswordResetRateLimitPolicy = "password-reset";

        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IMapper _mapper = mapper;
        private readonly IWebHostEnvironment _environment = environment;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<AuthController> _logger = logger;

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto dto)
        {
            var user = _mapper.Map<ApplicationUser>(dto);

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors.Select(e => e.Description));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "student");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return BadRequest(roleResult.Errors.Select(e => e.Description));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var tokens = await _tokenService.CreateTokensAsync(user, roles);

            return IssueTokens(tokens);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                return Unauthorized("Invalid email or password.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var tokens = await _tokenService.CreateTokensAsync(user, roles);

            return IssueTokens(tokens);
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting(PasswordResetRateLimitPolicy)]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is not null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = BuildPasswordResetLink(dto.Email, token);

                try
                {
                    await _emailSender.SendPasswordResetEmailAsync(dto.Email, resetLink);
                }
                catch (Exception ex)
                {
                    // Never surface delivery failures: a different response for known
                    // and unknown emails would let an attacker enumerate accounts.
                    _logger.LogError(ex, "Failed to send password reset email for {Email}.", dto.Email);
                }
            }

            // Always the same response, whether or not the address is registered.
            return NoContent();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null)
            {
                return BadRequest("Invalid or expired password reset token.");
            }

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors.Select(e => e.Description));
            }

            // The reset may be an account-recovery response to a compromise, so
            // invalidate sessions that were established before it.
            await _tokenService.RevokeAllRefreshTokensForUserAsync(user.Id);

            return NoContent();
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> Refresh()
        {
            var refreshToken = Request.Cookies[JwtConstants.RefreshTokenCookie];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized("Missing refresh token.");
            }

            try
            {
                var tokens = await _tokenService.RefreshTokenAsync(refreshToken);
                return IssueTokens(tokens);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid or expired refresh token.");
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies[JwtConstants.RefreshTokenCookie];
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _tokenService.RevokeRefreshTokenAsync(refreshToken);
            }

            Response.Cookies.Delete(JwtConstants.RefreshTokenCookie, BuildCookieOptions());
            return NoContent();
        }

        private AuthResponseDto IssueTokens(AuthTokens tokens)
        {
            SetRefreshTokenCookie(tokens);

            return new AuthResponseDto
            {
                AccessToken = tokens.AccessToken,
                ExpiresAt = tokens.ExpiresAt
            };
        }

        private void SetRefreshTokenCookie(AuthTokens tokens)
        {
            Response.Cookies.Append(
                JwtConstants.RefreshTokenCookie,
                tokens.RefreshToken,
                BuildCookieOptions(tokens.RefreshTokenExpiresAt));
        }

        private string BuildPasswordResetLink(string email, string token)
        {
            // The frontend reset page. Falls back to the first allowed CORS origin so
            // local development works without extra configuration.
            var pageUrl = _configuration["PASSWORD_RESET_PAGE_URL"];
            if (string.IsNullOrWhiteSpace(pageUrl))
            {
                var origin = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault();
                pageUrl = string.IsNullOrWhiteSpace(origin)
                    ? "http://localhost:5173/reset-password"
                    : $"{origin.TrimEnd('/')}/reset-password";
            }

            var separator = pageUrl.Contains('?') ? '&' : '?';
            return $"{pageUrl}{separator}email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        }

        private CookieOptions BuildCookieOptions(DateTime? expires = null) => new()
        {
            HttpOnly = true,
            Secure = _environment.IsProduction(),
            // In production the SPA and API usually live on different origins, so the
            // cookie has to be sent cross-site (None). In development (Vite on
            // localhost) Strict keeps the same-site cookie tight.
            SameSite = _environment.IsProduction() ? SameSiteMode.None : SameSiteMode.Strict,
            Path = "/",
            Expires = expires.HasValue ? new DateTimeOffset(expires.Value) : null
        };
    }
}
