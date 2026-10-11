
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    SupportDbContext db,
    IPasswordHasher<Agent> passwordHasher,
    IConfiguration configuration) : ControllerBase
{
    public sealed record LoginRequest(string Email, string Password);

    public sealed record AgentResponse(
        int Id,
        string FullName,
        string Email);

    public sealed record LoginResponse(
        string Token,
        DateTime ExpiresAtUtc,
        AgentResponse Agent);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        const string invalidCredentials = "Invalid email or password.";

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = invalidCredentials });
        }

        var email = request.Email.Trim();

        var agent = await db.Set<Agent>()
            .FirstOrDefaultAsync(a => a.Email == email, ct);

        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash))
        {
            return Unauthorized(new { message = invalidCredentials });
        }

        var result = passwordHasher.VerifyHashedPassword(
            agent,
            agent.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = invalidCredentials });
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            agent.SetPasswordHash(
                passwordHasher.HashPassword(agent, request.Password));

            await db.SaveChangesAsync(ct);
        }

        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");

        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes.");
        }

        var issuer = configuration["Jwt:Issuer"] ?? "NorthwindSupport";
        var expiresAt = DateTime.UtcNow.AddMinutes(60);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email)
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new AgentResponse(agent.Id, agent.FullName, agent.Email)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AgentResponse>> Me(
        CancellationToken ct)
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idText, out var id))
        {
            return Unauthorized();
        }

        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.Id == id && a.IsActive)
            .Select(a => new AgentResponse(a.Id, a.FullName, a.Email))
            .FirstOrDefaultAsync(ct);

        return agent is null ? Unauthorized() : Ok(agent);
    }
}
