using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserSummaryDto>> ListAsync();
    Task<IReadOnlyList<UserSummaryDto>> ListAgentsAsync();
    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync();
    Task ChangeRoleAsync(UserContext ctx, int userId, UserRole role);
    Task SetActiveAsync(UserContext ctx, int userId, bool isActive);
}

public class UserService(AppDbContext db, ILogger<UserService> logger) : IUserService
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListAsync() =>
        await db.Users.AsNoTracking().OrderBy(u => u.FullName)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Role,
                u.Department == null ? null : u.Department.Name, u.IsActive, u.CreatedAt))
            .ToListAsync();

    public async Task<IReadOnlyList<UserSummaryDto>> ListAgentsAsync() =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Role == UserRole.Agent || u.Role == UserRole.Admin))
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Role,
                u.Department == null ? null : u.Department.Name, u.IsActive, u.CreatedAt))
            .ToListAsync();

    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync() =>
        await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new DepartmentDto(d.Id, d.Name)).ToListAsync();

    public async Task ChangeRoleAsync(UserContext ctx, int userId, UserRole role)
    {
        if (!ctx.IsAdmin) throw new ForbiddenException("Only an admin can change roles.");
        if (ctx.Id == userId) throw new BusinessRuleException("You cannot change your own role.");
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        user.Role = role;
        await db.SaveChangesAsync();
        logger.LogWarning("Admin {AdminId} set role of user {UserId} to {Role}", ctx.Id, userId, role);
    }

    public async Task SetActiveAsync(UserContext ctx, int userId, bool isActive)
    {
        if (!ctx.IsAdmin) throw new ForbiddenException("Only an admin can activate or deactivate users.");
        if (ctx.Id == userId) throw new BusinessRuleException("You cannot deactivate your own account.");
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        user.IsActive = isActive;
        await db.SaveChangesAsync();
        logger.LogWarning("Admin {AdminId} set active={Active} for user {UserId}", ctx.Id, isActive, userId);
    }
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync();
    Task<CategoryDto> CreateAsync(CategoryRequest request);
    Task<CategoryDto> UpdateAsync(int id, CategoryRequest request);
    Task DeleteAsync(int id);
}

public class CategoryService(AppDbContext db, ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync() =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToListAsync();

    public async Task<CategoryDto> CreateAsync(CategoryRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name))
            throw new BusinessRuleException("A category with this name already exists.");
        var category = new Category { Name = name, Description = request.Description?.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        logger.LogInformation("Category {CategoryId} created", category.Id);
        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryRequest request)
    {
        var category = await db.Categories.FindAsync(id) ?? throw new NotFoundException("Category not found.");
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id))
            throw new BusinessRuleException("A category with this name already exists.");
        category.Name = name;
        category.Description = request.Description?.Trim();
        await db.SaveChangesAsync();
        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await db.Categories.FindAsync(id) ?? throw new NotFoundException("Category not found.");
        if (await db.Tickets.AnyAsync(t => t.CategoryId == id))
            throw new BusinessRuleException("This category is used by existing tickets and cannot be deleted.");
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        logger.LogInformation("Category {CategoryId} deleted", id);
    }
}
