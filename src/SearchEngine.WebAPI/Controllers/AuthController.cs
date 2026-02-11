using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SearchEngine.Application.DTOs;
using SearchEngine.Infrastructure.Configuration;

namespace SearchEngine.WebAPI.Controllers;

/// <summary>
/// Kimlik doğrulama endpoint'leri. Demo amaçlı sabit kullanıcı bilgileri kullanır.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;
    private readonly IValidator<LoginRequest> _validator;

    /// <summary>Demo kullanıcı adı.</summary>
    private const string DemoUsername = "admin";

    /// <summary>Demo şifre.</summary>
    private const string DemoPassword = "admin123";

    public AuthController(IOptions<JwtSettings> jwtSettings, IValidator<LoginRequest> validator)
    {
        _jwtSettings = jwtSettings.Value;
        _validator = validator;
    }

    /// <summary>
    /// Kullanıcı bilgileriyle giriş yap ve JWT token al.
    /// Demo kimlik bilgileri: admin / admin123
    /// </summary>
    /// <param name="request">Kullanıcı adı ve şifre.</param>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // FluentValidation ile girdi doğrulama
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { errors });
        }

        if (request.Username != DemoUsername || request.Password != DemoPassword)
        {
            return Unauthorized(new { message = "Geçersiz kullanıcı adı veya şifre." });
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);
        var token = GenerateJwtToken(request.Username, expiresAt);

        return Ok(new LoginResponse(token, expiresAt));
    }

    /// <summary>JWT token oluşturur (HS256 imzalama).</summary>
    private string GenerateJwtToken(string username, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
