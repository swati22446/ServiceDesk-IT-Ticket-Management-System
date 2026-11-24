using System.Security.Claims;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Common;

/// <summary>The caller's identity, passed from controllers to services (keeps services free of HttpContext).</summary>
public record UserContext(int Id, UserRole Role)
{
    public bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
    public bool IsAdmin => Role == UserRole.Admin;
}

public static class ClaimsPrincipalExtensions
{
    public static UserContext ToUserContext(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(id, out var userId) || !Enum.TryParse<UserRole>(role, out var userRole))
            throw new ForbiddenException("Invalid or missing identity claims.");
        return new UserContext(userId, userRole);
    }
}
