using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Department).WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Category>(e => e.HasIndex(c => c.Name).IsUnique());
        b.Entity<Department>(e => e.HasIndex(d => d.Name).IsUnique());

        b.Entity<Ticket>(e =>
        {
            e.Property(t => t.RowVersion).IsRowVersion();
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.CreatedById);
            e.HasIndex(t => t.AssignedToId);
            e.HasOne(t => t.Category).WithMany(c => c.Tickets)
                .HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CreatedBy).WithMany(u => u.CreatedTickets)
                .HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedTo).WithMany(u => u.AssignedTickets)
                .HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TicketComment>(e =>
        {
            e.HasOne(c => c.Ticket).WithMany(t => t.Comments)
                .HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany()
                .HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TicketAttachment>(e =>
        {
            e.HasOne(a => a.Ticket).WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.UploadedBy).WithMany()
                .HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TicketHistory>(e =>
        {
            e.HasOne(h => h.Ticket).WithMany(t => t.History)
                .HasForeignKey(h => h.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedBy).WithMany()
                .HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
