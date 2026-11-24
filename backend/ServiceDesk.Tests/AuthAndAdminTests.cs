using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests;

public class AuthAndAdminTests
{
    private static JwtTokenService CreateJwt() => new(Options.Create(new JwtSettings
    {
        Key = "unit-test-key-that-is-at-least-32-characters-long!",
        Issuer = "test-issuer", Audience = "test-audience", ExpiryMinutes = 30
    }));

    private static AuthService CreateAuth(TestEnv env) =>
        new(env.Db, new PasswordHasher<User>(), CreateJwt(), NullLogger<AuthService>.Instance);

    [Fact]
    public void Jwt_ContainsIdentityAndRoleClaims_AndExpiry()
    {
        var user = new User { Id = 7, FullName = "Test", Email = "t@test.local", Role = UserRole.Agent };
        var (token, expires) = CreateJwt().Create(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("test-issuer", parsed.Issuer);
        Assert.Contains(parsed.Claims, c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "Agent");
        Assert.Contains(parsed.Claims, c => c.Value == "7");
        Assert.True(expires > DateTime.UtcNow.AddMinutes(29));
    }

    [Fact]
    public async Task Register_CreatesNormalUser_WithHashedPassword()
    {
        var env = new TestEnv();
        var response = await CreateAuth(env).RegisterAsync(new RegisterRequest
        {
            FullName = "New Person", Email = "  NEW@Test.local ", Password = "Strong1pass"
        });

        Assert.Equal(UserRole.User, response.User.Role);
        var saved = await env.Db.Users.SingleAsync(u => u.Email == "new@test.local");
        Assert.NotEqual("Strong1pass", saved.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        var env = new TestEnv();
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAuth(env).RegisterAsync(new RegisterRequest
        {
            FullName = "Dup", Email = "alice@test.local", Password = "Strong1pass"
        }));
    }

    [Fact]
    public async Task Login_Succeeds_WithCorrectPassword()
    {
        var env = new TestEnv();
        var response = await CreateAuth(env).LoginAsync(new LoginRequest { Email = "ALICE@test.local", Password = "Passw0rd!" });
        Assert.Equal("Alice", response.User.FullName);
    }

    [Theory]
    [InlineData("alice@test.local", "wrong-password")]
    [InlineData("nobody@test.local", "Passw0rd!")]
    public async Task Login_Fails_WithSameErrorForBadPasswordOrUnknownUser(string email, string password)
    {
        var env = new TestEnv();
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateAuth(env).LoginAsync(new LoginRequest { Email = email, Password = password }));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task Login_InactiveUser_IsRejected()
    {
        var env = new TestEnv();
        var alice = await env.Db.Users.SingleAsync(u => u.Email == "alice@test.local");
        alice.IsActive = false;
        await env.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateAuth(env).LoginAsync(new LoginRequest { Email = "alice@test.local", Password = "Passw0rd!" }));
    }

    [Fact]
    public void ClaimsPrincipal_ToUserContext_ParsesIdAndRole()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "Admin")
        }, "test"));

        var ctx = principal.ToUserContext();

        Assert.Equal(42, ctx.Id);
        Assert.True(ctx.IsAdmin);
        Assert.True(ctx.IsStaff);
    }

    [Fact]
    public void ClaimsPrincipal_WithoutClaims_IsForbidden() =>
        Assert.Throws<ForbiddenException>(() => new ClaimsPrincipal(new ClaimsIdentity()).ToUserContext());

    [Fact]
    public async Task UserService_AdminCanChangeRole_ButNotOwn_AndNonAdminCannot()
    {
        var env = new TestEnv();
        var service = new UserService(env.Db, NullLogger<UserService>.Instance);

        await service.ChangeRoleAsync(env.Admin, env.Alice.Id, UserRole.Agent);
        Assert.Equal(UserRole.Agent, (await env.Db.Users.FindAsync(env.Alice.Id))!.Role);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeRoleAsync(env.Admin, env.Admin.Id, UserRole.User));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ChangeRoleAsync(env.Agent, env.Bob.Id, UserRole.Admin));
    }

    [Fact]
    public async Task UserService_DeactivateUser_HidesFromAgentList()
    {
        var env = new TestEnv();
        var service = new UserService(env.Db, NullLogger<UserService>.Instance);

        Assert.Single(await service.ListAgentsAsync(), u => u.Role == UserRole.Agent);
        await service.SetActiveAsync(env.Admin, env.Agent.Id, false);
        Assert.DoesNotContain(await service.ListAgentsAsync(), u => u.Id == env.Agent.Id);
    }

    [Fact]
    public async Task CategoryService_Crud_AndCannotDeleteCategoryInUse()
    {
        var env = new TestEnv();
        var service = new CategoryService(env.Db, NullLogger<CategoryService>.Instance);

        var created = await service.CreateAsync(new CategoryRequest { Name = "Security" });
        var renamed = await service.UpdateAsync(created.Id, new CategoryRequest { Name = "InfoSec", Description = "Security incidents" });
        Assert.Equal("InfoSec", renamed.Name);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new CategoryRequest { Name = "Hardware" }));

        await service.DeleteAsync(created.Id);
        Assert.DoesNotContain(await service.ListAsync(), c => c.Id == created.Id);

        await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(env.CategoryId));
    }

    [Fact]
    public async Task ReportService_ProducesXlsx_ForStaff_AndRejectsNormalUsers()
    {
        var env = new TestEnv();
        await env.CreateTicketAsync(env.Alice);
        var service = new ReportService(env.Db, NullLogger<ReportService>.Instance);

        var bytes = await service.ExportTicketsAsync(env.Agent, new TicketQuery());

        Assert.True(bytes.Length > 1000);
        Assert.Equal((byte)'P', bytes[0]); // xlsx is a zip archive ("PK")
        Assert.Equal((byte)'K', bytes[1]);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ExportTicketsAsync(env.Alice, new TicketQuery()));
    }
}
