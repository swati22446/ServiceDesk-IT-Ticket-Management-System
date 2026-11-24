using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain an upper-case letter, a lower-case letter and a digit.")]
    public string Password { get; set; } = "";

    public int? DepartmentId { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class CreateTicketRequest
{
    [Required, StringLength(150, MinimumLength = 5)]
    public string Title { get; set; } = "";

    [Required, StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a category.")]
    public int CategoryId { get; set; }

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public class UpdateTicketRequest : CreateTicketRequest
{
    /// <summary>Base64 RowVersion returned by GET; used for optimistic concurrency.</summary>
    public string? RowVersion { get; set; }
}

public class ChangeStatusRequest
{
    [EnumDataType(typeof(TicketStatus))] public TicketStatus Status { get; set; }
}

public class AssignTicketRequest
{
    [Range(1, int.MaxValue)] public int AgentId { get; set; }
}

public class AddCommentRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(250)] public string? Description { get; set; }
}

public class ChangeRoleRequest
{
    [EnumDataType(typeof(UserRole))] public UserRole Role { get; set; }
}

public class ChangeActiveRequest
{
    public bool IsActive { get; set; }
}

public class TicketQuery
{
    public string? Search { get; set; }
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public int? AssignedToId { get; set; }
    public bool? OverdueOnly { get; set; }
    /// <summary>createdAt | dueAt | priority | status | title</summary>
    public string SortBy { get; set; } = "createdAt";
    public bool Desc { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
