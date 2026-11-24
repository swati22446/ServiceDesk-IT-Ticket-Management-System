using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests;

/// <summary>Builds an isolated in-memory database with one admin, one agent, two users and two categories.</summary>
public sealed class TestEnv
{
    public AppDbContext Db { get; }
    public Mock<IEmailService> Mail { get; } = new();
    public Mock<IFileStorage> Storage { get; } = new();
    public TicketService Tickets { get; }

    public UserContext Admin { get; }
    public UserContext Agent { get; }
    public UserContext Alice { get; }   // normal user
    public UserContext Bob { get; }     // another normal user

    public TestEnv()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new AppDbContext(options);

        var hasher = new PasswordHasher<User>();
        User Make(string name, string email, UserRole role, string password = "Passw0rd!")
        {
            var u = new User { FullName = name, Email = email, Role = role };
            u.PasswordHash = hasher.HashPassword(u, password);
            return u;
        }

        var admin = Make("Admin", "admin@test.local", UserRole.Admin);
        var agent = Make("Agent", "agent@test.local", UserRole.Agent);
        var alice = Make("Alice", "alice@test.local", UserRole.User);
        var bob = Make("Bob", "bob@test.local", UserRole.User);
        Db.Users.AddRange(admin, agent, alice, bob);
        Db.Categories.AddRange(new Category { Name = "Hardware" }, new Category { Name = "Software" });
        Db.SaveChanges();

        Admin = new UserContext(admin.Id, UserRole.Admin);
        Agent = new UserContext(agent.Id, UserRole.Agent);
        Alice = new UserContext(alice.Id, UserRole.User);
        Bob = new UserContext(bob.Id, UserRole.User);

        Tickets = new TicketService(Db, Mail.Object, Storage.Object, NullLogger<TicketService>.Instance);
    }

    public int CategoryId => Db.Categories.First().Id;

    public async Task<int> CreateTicketAsync(UserContext owner, ServiceDesk.Api.Dtos.CreateTicketRequest? request = null)
    {
        request ??= new ServiceDesk.Api.Dtos.CreateTicketRequest
        {
            Title = "Laptop is broken",
            Description = "The screen flickers and then goes black.",
            CategoryId = CategoryId,
            Priority = TicketPriority.High
        };
        var created = await Tickets.CreateAsync(owner, request);
        return created.Id;
    }
}
