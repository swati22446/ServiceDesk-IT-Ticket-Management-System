using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Dtos;

public record UserInfo(int Id, string FullName, string Email, UserRole Role, string? Department);
public record AuthResponse(string Token, DateTime ExpiresAt, UserInfo User);
public record CategoryDto(int Id, string Name, string? Description);
public record DepartmentDto(int Id, string Name);
public record UserSummaryDto(int Id, string FullName, string Email, UserRole Role, string? Department, bool IsActive, DateTime CreatedAt);

public record TicketDto(
    int Id, string Title, TicketStatus Status, TicketPriority Priority, string Category,
    string CreatedBy, string? AssignedTo, DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, bool IsOverdue);

public record CommentDto(int Id, string Author, UserRole AuthorRole, string Body, bool IsInternal, DateTime CreatedAt);
public record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, string UploadedBy, DateTime UploadedAt);
public record HistoryDto(string Action, string? OldValue, string? NewValue, string ChangedBy, DateTime ChangedAt);

public record TicketDetailDto(
    int Id, string Title, string Description, TicketStatus Status, TicketPriority Priority,
    int CategoryId, string Category, int CreatedById, string CreatedBy, int? AssignedToId, string? AssignedTo,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, DateTime? ResolvedAt, bool IsOverdue, string RowVersion,
    IReadOnlyList<CommentDto> Comments, IReadOnlyList<AttachmentDto> Attachments, IReadOnlyList<HistoryDto> History);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public record CountItem(string Label, int Count);

public record DashboardDto(
    int Total, int Open, int InProgress, int Resolved, int Overdue, int Unassigned, double AvgResolutionHours,
    IReadOnlyList<CountItem> ByStatus, IReadOnlyList<CountItem> ByPriority,
    IReadOnlyList<CountItem> ByCategory, IReadOnlyList<CountItem> Last7Days);

public record DownloadResult(string Path, string FileName, string ContentType);
