using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

/// <summary>Creates lookup data, demo accounts and a few sample tickets on first run.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher, bool includeDemoData)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Hardware", Description = "Laptops, desktops, printers, peripherals" },
                new Category { Name = "Software", Description = "Installation, licences, application errors" },
                new Category { Name = "Network", Description = "Wi-Fi, VPN, connectivity" },
                new Category { Name = "Access & Accounts", Description = "Password resets, permissions" },
                new Category { Name = "Email", Description = "Mailbox, calendar, distribution lists" },
                new Category { Name = "Other", Description = "Anything else" });
        }

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(
                new Department { Name = "IT" }, new Department { Name = "HR" },
                new Department { Name = "Finance" }, new Department { Name = "Operations" });
        }
        await db.SaveChangesAsync();

        if (!includeDemoData || await db.Users.AnyAsync()) return;

        User Make(string name, string email, string password, UserRole role, int? dept)
        {
            var u = new User { FullName = name, Email = email, Role = role, DepartmentId = dept, CreatedAt = DateTime.UtcNow };
            u.PasswordHash = hasher.HashPassword(u, password);
            return u;
        }

        var it = (await db.Departments.FirstAsync(d => d.Name == "IT")).Id;
        var hr = (await db.Departments.FirstAsync(d => d.Name == "HR")).Id;
        var admin = Make("System Admin", "admin@servicedesk.local", "Admin@123", UserRole.Admin, it);
        var agent = Make("Asha Agent", "agent@servicedesk.local", "Agent@123", UserRole.Agent, it);
        var user = Make("Uma User", "user@servicedesk.local", "User@1234", UserRole.User, hr);
        db.Users.AddRange(admin, agent, user);
        await db.SaveChangesAsync();

        var cats = await db.Categories.ToListAsync();
        int Cat(string n) => cats.First(c => c.Name == n).Id;
        var now = DateTime.UtcNow;

        Ticket T(string title, string desc, TicketStatus s, TicketPriority p, string cat, int daysAgo, bool assigned)
        {
            var created = now.AddDays(-daysAgo);
            var hours = p switch { TicketPriority.Critical => 4, TicketPriority.High => 24, TicketPriority.Medium => 48, _ => 72 };
            var t = new Ticket
            {
                Title = title, Description = desc, Status = s, Priority = p, CategoryId = Cat(cat),
                CreatedById = user.Id, AssignedToId = assigned ? agent.Id : null,
                CreatedAt = created, UpdatedAt = created, DueAt = created.AddHours(hours),
                ResolvedAt = s is TicketStatus.Resolved or TicketStatus.Closed ? created.AddHours(5) : null
            };
            t.History.Add(new TicketHistory { ChangedById = user.Id, Action = "Created", NewValue = "Open", ChangedAt = created });
            return t;
        }

        db.Tickets.AddRange(
            T("Laptop will not boot", "Black screen after the Windows logo since this morning.", TicketStatus.Open, TicketPriority.High, "Hardware", 0, false),
            T("VPN disconnects every 10 minutes", "Working from home, VPN drops repeatedly.", TicketStatus.InProgress, TicketPriority.Medium, "Network", 1, true),
            T("Need Visual Studio licence", "New joiner needs Visual Studio Professional.", TicketStatus.Open, TicketPriority.Low, "Software", 2, false),
            T("Cannot access shared finance drive", "Access denied on the finance reports share.", TicketStatus.OnHold, TicketPriority.Medium, "Access & Accounts", 3, true),
            T("Outlook keeps crashing", "Outlook closes when opening calendar invites.", TicketStatus.Resolved, TicketPriority.Medium, "Email", 4, true),
            T("Production server unreachable", "Intranet portal returns timeouts for all users.", TicketStatus.Closed, TicketPriority.Critical, "Network", 6, true),
            T("Printer on floor 2 offline", "Shared printer shows offline status.", TicketStatus.Resolved, TicketPriority.Low, "Hardware", 5, true));
        await db.SaveChangesAsync();
    }
}
