using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // Log-in for the app: email + password (+ authenticator code if switched on) gives a short-lived access token
    // and a refresh token that gets a new pair when the access token runs out
    [ApiController]
    [Route("api/v1/auth")]
    [AllowAnonymous]
    public class AuthApiController : ControllerBase
    {
        private readonly IAccountService _accounts;
        private readonly IApiTokenService _tokens;

        public AuthApiController(IAccountService accounts, IApiTokenService tokens)
        {
            _accounts = accounts;
            _tokens = tokens;
        }

        [HttpPost("login")]
        [EnableRateLimiting(AccountController.LoginRateLimitPolicy)]
        [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
                return ApiProblems.Result(StatusCodes.Status400BadRequest, "missing_fields", "Send your college email address and password.");

            var account = await _accounts.SignInAsync(request.Login.Trim(), request.Password);
            if (account == null)
                return ApiProblems.Result(StatusCodes.Status401Unauthorized, "invalid_login", "Your email or password is wrong.");
            if (account.Entity is not Student student)
                return ApiProblems.Result(StatusCodes.Status403Forbidden, "students_only", "The app is for students. Lecturers and admins use the website.");
            if (student.MustChangePassword)
                return ApiProblems.Result(StatusCodes.Status403Forbidden, "password_change_required", "Your password was reset. Log in on the website once to choose a new one.");

            if (student.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.Code))
                    return ApiProblems.Result(StatusCodes.Status401Unauthorized, "two_factor_required", "Enter the 6-digit code from your authenticator app.");
                if (await _accounts.CheckTwoFactorAsync(account, request.Code) == TwoFactorCheck.Wrong)
                    return ApiProblems.Result(StatusCodes.Status401Unauthorized, "invalid_code", "That code didn't work. Use the newest code from your app, or a recovery code.");
            }

            return Ok(ToResponse(await _tokens.IssueAsync(account), student));
        }

        // Swap the refresh token for a new pair; each refresh token works once
        [HttpPost("refresh")]
        [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh(RefreshRequest request)
        {
            var result = await _tokens.RefreshAsync(request.RefreshToken);
            if (result?.Account.Entity is not Student student)
                return ApiProblems.Result(StatusCodes.Status401Unauthorized, "invalid_refresh_token", "Log in again.");
            return Ok(ToResponse(result.Tokens, student));
        }

        // Ends this app session; the refresh token stops working
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout(RefreshRequest request)
        {
            await _tokens.RevokeAsync(request.RefreshToken);
            return NoContent();
        }

        private static TokenResponse ToResponse(ApiTokenPair tokens, Student student) =>
            new(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt, ApiMap.Student(student));
    }
}
