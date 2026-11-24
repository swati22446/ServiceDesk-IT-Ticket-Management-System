using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserInfo> GetProfileAsync(int userId);
}

public class AuthService(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IJwtTokenService jwt,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            throw new BusinessRuleException("An account with this email already exists.");

        if (request.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == request.DepartmentId))
            throw new BusinessRuleException("Selected department does not exist.");

        // Self-registration always creates a normal 'User'. Only an Admin can promote accounts.
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = UserRole.User,
            DepartmentId = request.DepartmentId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("User {UserId} registered", user.Id);
        return await BuildResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Email == email);

        // Same message for every failure so attackers cannot discover which emails exist.
        const string failure = "Invalid email or password.";
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Failed login attempt (unknown or inactive account)");
            throw new AuthenticationFailedException(failure);
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed login attempt for user {UserId}", user.Id);
            throw new AuthenticationFailedException(failure);
        }

        logger.LogInformation("User {UserId} logged in", user.Id);
        return await BuildResponseAsync(user);
    }

    public async Task<UserInfo> GetProfileAsync(int userId)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new NotFoundException("User not found.");
        return ToInfo(user);
    }

    private async Task<AuthResponse> BuildResponseAsync(User user)
    {
        if (user.Department is null && user.DepartmentId.HasValue)
            user.Department = await db.Departments.FindAsync(user.DepartmentId.Value);
        var (token, expires) = jwt.Create(user);
        return new AuthResponse(token, expires, ToInfo(user));
    }

    private static UserInfo ToInfo(User u) => new(u.Id, u.FullName, u.Email, u.Role, u.Department?.Name);
}
