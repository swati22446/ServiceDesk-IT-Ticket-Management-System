using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Api.Models;

public class Department
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    [Required, MaxLength(150)] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
}

public class Category
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(250)] public string? Description { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}

public class Ticket
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [Required, MaxLength(4000)] public string Description { get; set; } = "";
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>SLA deadline, calculated from priority when the ticket is created.</summary>
    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
}

public class TicketComment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
    [Required, MaxLength(2000)] public string Body { get; set; } = "";
    /// <summary>Internal notes are visible to staff only.</summary>
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    [Required, MaxLength(255)] public string OriginalFileName { get; set; } = "";
    [Required, MaxLength(100)] public string StoredFileName { get; set; } = "";
    [Required, MaxLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}

/// <summary>Audit trail: every important change to a ticket is recorded here.</summary>
public class TicketHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public int ChangedById { get; set; }
    public User ChangedBy { get; set; } = null!;
    [Required, MaxLength(50)] public string Action { get; set; } = "";
    [MaxLength(200)] public string? OldValue { get; set; }
    [MaxLength(200)] public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
}
