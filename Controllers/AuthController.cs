using System.Security.Claims;
using Google.Apis.Auth;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Models;
using HouseOfAlchemists.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, IConfiguration config) : ControllerBase
{
    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest req)
    {
        // Verify Google ID token
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { config["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(req.Credential, settings);
        }
        catch
        {
            return Unauthorized(new { message = "Invalid Google token" });
        }

        var googleId = payload.Subject;
        var email    = payload.Email;
        var name     = payload.Name ?? email;

        // Find by GoogleId first, then by email
        var user = await db.Users.Include(u => u.Batch)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId || u.Email == email);

        bool isNew = false;

        if (user == null)
        {
            // New Google user — create student account
            user = new User
            {
                Name       = name,
                Email      = email,
                GoogleId   = googleId,
                Role       = "student",
                Language   = "bangla",
                IsActive   = true
            };
            db.Users.Add(user);
            isNew = true;
        }
        else
        {
            // Only students can sign in via Google on the student portal
            if (user.Role != "student")
                return Conflict(new { message = $"This Google account is registered as a {user.Role}. Please use the {user.Role} portal to sign in." });

            // Link GoogleId if not already set
            if (user.GoogleId != googleId)
                user.GoogleId = googleId;

            if (!user.IsActive)
                return Unauthorized(new { message = "Your account has been deactivated. Contact admin." });
        }

        var accessToken  = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();
        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!));
        await db.SaveChangesAsync();

        return Ok(new
        {
            accessToken,
            refreshToken,
            role  = user.Role,
            isNew,
            user  = new { user.Id, user.Name, user.Email, user.Phone, user.Role,
                          user.Language, user.Theme,
                          batchId   = user.BatchId,
                          batchName = user.Batch?.Name }
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "Email already in use" });

        var user = new User { Name = req.Name, Email = req.Email, Phone = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "student", Language = req.Language ?? "bangla" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { message = "Registered successfully" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.Include(u => u.Batch).FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });
        if (user.PasswordHash == null)
            return Unauthorized(new { message = "This account was created with Google Sign-In and has no password. Please use Google to sign in, or contact admin to set a password." });
        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });
        if (!user.IsActive)
            return Unauthorized(new { message = "Your account has been deactivated. Contact admin." });

        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"]!));
        await db.SaveChangesAsync();

        return Ok(new { accessToken, refreshToken, role = user.Role,
            mustChangePassword = user.MustChangePassword,
            user = new { user.Id, user.Name, user.Email, user.Role, user.Language, user.Theme,
                         batchId = user.BatchId, batchName = user.Batch?.Name } });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == req.RefreshToken && u.RefreshTokenExpiry > DateTime.UtcNow);
        if (user == null) return Unauthorized(new { message = "Invalid or expired refresh token" });

        var accessToken = tokens.CreateAccessToken(user);
        var newRefresh = tokens.CreateRefreshToken();
        user.RefreshToken = newRefresh;
        await db.SaveChangesAsync();
        return Ok(new { accessToken, refreshToken = newRefresh });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == req.RefreshToken);
        if (user != null) { user.RefreshToken = null; await db.SaveChangesAsync(); }
        return Ok();
    }

    [Authorize]
    [HttpPatch("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect" });
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "Password changed successfully" });
    }
}
