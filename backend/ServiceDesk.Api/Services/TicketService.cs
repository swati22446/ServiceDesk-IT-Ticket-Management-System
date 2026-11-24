using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketDto>> ListAsync(UserContext ctx, TicketQuery query);
    Task<TicketDetailDto> GetAsync(UserContext ctx, int id);
    Task<TicketDetailDto> CreateAsync(UserContext ctx, CreateTicketRequest request);
    Task<TicketDetailDto> UpdateAsync(UserContext ctx, int id, UpdateTicketRequest request);
    Task<TicketDetailDto> ChangeStatusAsync(UserContext ctx, int id, TicketStatus status);
    Task<TicketDetailDto> AssignAsync(UserContext ctx, int id, int agentId);
    Task<CommentDto> AddCommentAsync(UserContext ctx, int id, AddCommentRequest request);
    Task DeleteAsync(UserContext ctx, int id);
    Task<DashboardDto> GetDashboardAsync(UserContext ctx);
}

public class TicketService(
    AppDbContext db,
    IEmailService mail,
    IFileStorage storage,
    ILogger<TicketService> logger) : ITicketService
{
    /// <summary>Hours allowed to resolve a ticket (SLA) per priority.</summary>
    public static int SlaHours(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => 4,
        TicketPriority.High => 24,
        TicketPriority.Medium => 48,
        _ => 72
    };

    public async Task<PagedResult<TicketDto>> ListAsync(UserContext ctx, TicketQuery query)
    {
        var now = DateTime.UtcNow;
        IQueryable<Ticket> q = db.Tickets.AsNoTracking();
        if (!ctx.IsStaff) q = q.Where(t => t.CreatedById == ctx.Id); // normal users only see their own tickets
        q = q.ApplyFilters(query, now);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var total = await q.CountAsync();

        var items = await q.ApplySort(query.SortBy, query.Desc)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new TicketDto(
                t.Id, t.Title, t.Status, t.Priority, t.Category.Name, t.CreatedBy.FullName,
                t.AssignedTo == null ? null : t.AssignedTo.FullName,
                t.CreatedAt, t.UpdatedAt, t.DueAt,
                t.DueAt < now && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed))
            .ToListAsync();

        return new PagedResult<TicketDto>(items, total, page, size);
    }

    public async Task<TicketDetailDto> GetAsync(UserContext ctx, int id)
    {
        var ticket = await db.Tickets.AsNoTracking().AsSplitQuery()
            .Include(t => t.Category).Include(t => t.CreatedBy).Include(t => t.AssignedTo)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(t => t.History).ThenInclude(h => h.ChangedBy)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Ticket {id} was not found.");

        EnsureCanAccess(ctx, ticket);
        return ToDetail(ticket, ctx);
    }

    public async Task<TicketDetailDto> CreateAsync(UserContext ctx, CreateTicketRequest request)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            throw new BusinessRuleException("Selected category does not exist.");

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = TicketStatus.Open,
            CategoryId = request.CategoryId,
            CreatedById = ctx.Id,
            CreatedAt = now,
            UpdatedAt = now,
            DueAt = now.AddHours(SlaHours(request.Priority))
        };
        ticket.History.Add(new TicketHistory
        {
            ChangedById = ctx.Id, Action = "Created", NewValue = nameof(TicketStatus.Open), ChangedAt = now
        });

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        logger.LogInformation("Ticket {TicketId} created by user {UserId} (priority {Priority})",
            ticket.Id, ctx.Id, ticket.Priority);

        await NotifyAsync(ctx.Id, $"[Ticket #{ticket.Id}] We received your request",
            $"Your ticket \"{ticket.Title}\" was created. Priority: {ticket.Priority}. Target resolution: {ticket.DueAt:u}.");

        return await GetAsync(ctx, ticket.Id);
    }

    public async Task<TicketDetailDto> UpdateAsync(UserContext ctx, int id, UpdateTicketRequest request)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id)
                     ?? throw new NotFoundException($"Ticket {id} was not found.");
        EnsureCanAccess(ctx, ticket);

        if (!ctx.IsStaff && ticket.Status != TicketStatus.Open)
            throw new ForbiddenException("You can only edit a ticket while it is still Open.");
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            throw new BusinessRuleException("Selected category does not exist.");

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                // Tell EF which version the client edited; SaveChanges fails with a concurrency error if it changed.
                db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException)
            {
                throw new BusinessRuleException("The supplied row version is not valid.");
            }
        }

        var now = DateTime.UtcNow;
        if (ticket.Priority != request.Priority)
        {
            AddHistory(ticket.Id, ctx.Id, "Priority changed", ticket.Priority.ToString(), request.Priority.ToString(), now);
            ticket.Priority = request.Priority;
            ticket.DueAt = ticket.CreatedAt.AddHours(SlaHours(request.Priority));
        }
        if (ticket.Title != request.Title.Trim() || ticket.Description != request.Description.Trim()
            || ticket.CategoryId != request.CategoryId)
            AddHistory(ticket.Id, ctx.Id, "Details edited", null, null, now);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.CategoryId = request.CategoryId;
        ticket.UpdatedAt = now;

        await db.SaveChangesAsync();
        logger.LogInformation("Ticket {TicketId} updated by user {UserId}", id, ctx.Id);
        return await GetAsync(ctx, id);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(UserContext ctx, int id, TicketStatus status)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id)
                     ?? throw new NotFoundException($"Ticket {id} was not found.");
        EnsureCanAccess(ctx, ticket);

        if (ticket.Status == status)
            throw new BusinessRuleException($"The ticket is already {status}.");

        if (!ctx.IsStaff)
        {
            // Requesters may only confirm (close) or reject (reopen) a resolution.
            var allowed = ticket.Status == TicketStatus.Resolved && status is TicketStatus.Closed or TicketStatus.Open;
            if (!allowed) throw new ForbiddenException("You can only close or reopen a resolved ticket.");
        }

        var now = DateTime.UtcNow;
        var old = ticket.Status;
        ticket.Status = status;
        ticket.UpdatedAt = now;
        if (status is TicketStatus.Resolved or TicketStatus.Closed) ticket.ResolvedAt ??= now;
        else ticket.ResolvedAt = null;

        AddHistory(ticket.Id, ctx.Id, "Status changed", old.ToString(), status.ToString(), now);
        await db.SaveChangesAsync();
        logger.LogInformation("Ticket {TicketId} status {Old} -> {New} by user {UserId}", id, old, status, ctx.Id);

        if (ctx.Id != ticket.CreatedById)
            await NotifyAsync(ticket.CreatedById, $"[Ticket #{ticket.Id}] Status is now {status}",
                $"Your ticket \"{ticket.Title}\" moved from {old} to {status}.");

        return await GetAsync(ctx, id);
    }

    public async Task<TicketDetailDto> AssignAsync(UserContext ctx, int id, int agentId)
    {
        if (!ctx.IsStaff) throw new ForbiddenException("Only agents and admins can assign tickets.");

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id)
                     ?? throw new NotFoundException($"Ticket {id} was not found.");
        var agent = await db.Users.FirstOrDefaultAsync(u => u.Id == agentId && u.IsActive
                        && (u.Role == UserRole.Agent || u.Role == UserRole.Admin))
                    ?? throw new BusinessRuleException("The selected assignee is not an active agent.");

        var now = DateTime.UtcNow;
        var previousName = ticket.AssignedToId.HasValue
            ? await db.Users.Where(u => u.Id == ticket.AssignedToId).Select(u => u.FullName).FirstOrDefaultAsync()
            : null;
        ticket.AssignedToId = agent.Id;
        ticket.UpdatedAt = now;
        if (ticket.Status == TicketStatus.Open) ticket.Status = TicketStatus.InProgress;

        AddHistory(ticket.Id, ctx.Id, "Assigned", previousName, agent.FullName, now);
        await db.SaveChangesAsync();
        logger.LogInformation("Ticket {TicketId} assigned to user {AssigneeId} by user {UserId}", id, agent.Id, ctx.Id);

        await NotifyAsync(agent.Id, $"[Ticket #{ticket.Id}] Assigned to you", $"Ticket \"{ticket.Title}\" has been assigned to you.");
        return await GetAsync(ctx, id);
    }

    public async Task<CommentDto> AddCommentAsync(UserContext ctx, int id, AddCommentRequest request)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id)
                     ?? throw new NotFoundException($"Ticket {id} was not found.");
        EnsureCanAccess(ctx, ticket);

        var author = await db.Users.FirstAsync(u => u.Id == ctx.Id);
        var now = DateTime.UtcNow;
        var isInternal = request.IsInternal && ctx.IsStaff; // requesters cannot write internal notes
        var comment = new TicketComment
        {
            TicketId = id, AuthorId = ctx.Id, Body = request.Body.Trim(), IsInternal = isInternal, CreatedAt = now
        };
        ticket.UpdatedAt = now;
        db.TicketComments.Add(comment);
        await db.SaveChangesAsync();
        logger.LogInformation("Comment {CommentId} added to ticket {TicketId}", comment.Id, id);

        if (!isInternal)
        {
            if (ctx.IsStaff && ticket.CreatedById != ctx.Id)
                await NotifyAsync(ticket.CreatedById, $"[Ticket #{id}] New reply", "A support agent replied to your ticket.");
            else if (!ctx.IsStaff && ticket.AssignedToId.HasValue)
                await NotifyAsync(ticket.AssignedToId.Value, $"[Ticket #{id}] Customer replied", "The requester added a comment.");
        }

        return new CommentDto(comment.Id, author.FullName, author.Role, comment.Body, comment.IsInternal, comment.CreatedAt);
    }

    public async Task DeleteAsync(UserContext ctx, int id)
    {
        if (!ctx.IsAdmin) throw new ForbiddenException("Only an admin can delete tickets.");

        var ticket = await db.Tickets.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.Id == id)
                     ?? throw new NotFoundException($"Ticket {id} was not found.");
        foreach (var attachment in ticket.Attachments) storage.Delete(attachment.StoredFileName);

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync();
        logger.LogWarning("Ticket {TicketId} deleted by admin {UserId}", id, ctx.Id);
    }

    public async Task<DashboardDto> GetDashboardAsync(UserContext ctx)
    {
        if (!ctx.IsStaff) throw new ForbiddenException("Dashboard is available to agents and admins only.");

        var now = DateTime.UtcNow;
        var tickets = db.Tickets.AsNoTracking();

        var total = await tickets.CountAsync();
        var overdue = await tickets.CountAsync(t => t.DueAt < now && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        var unassigned = await tickets.CountAsync(t => t.AssignedToId == null && t.Status != TicketStatus.Closed);

        var byStatus = (await tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
            .ToDictionary(x => x.Key, x => x.Count);
        var byPriority = (await tickets.GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
            .ToDictionary(x => x.Key, x => x.Count);
        var byCategory = await tickets.GroupBy(t => t.Category.Name)
            .Select(g => new CountItem(g.Key, g.Count())).ToListAsync();

        var resolved = await tickets.Where(t => t.ResolvedAt != null)
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value }).ToListAsync();
        var avgHours = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(r => (r.ResolvedAt - r.CreatedAt).TotalHours), 1);

        var since = now.Date.AddDays(-6);
        var recent = await tickets.Where(t => t.CreatedAt >= since).Select(t => t.CreatedAt).ToListAsync();
        var last7 = Enumerable.Range(0, 7).Select(i => since.AddDays(i))
            .Select(day => new CountItem(day.ToString("dd MMM"), recent.Count(c => c.Date == day))).ToList();

        int Count(Dictionary<TicketStatus, int> d, TicketStatus s) => d.GetValueOrDefault(s);

        return new DashboardDto(
            total, Count(byStatus, TicketStatus.Open), Count(byStatus, TicketStatus.InProgress),
            Count(byStatus, TicketStatus.Resolved) + Count(byStatus, TicketStatus.Closed), overdue, unassigned, avgHours,
            Enum.GetValues<TicketStatus>().Select(s => new CountItem(s.ToString(), byStatus.GetValueOrDefault(s))).ToList(),
            Enum.GetValues<TicketPriority>().Select(p => new CountItem(p.ToString(), byPriority.GetValueOrDefault(p))).ToList(),
            byCategory.OrderByDescending(c => c.Count).ToList(), last7);
    }

    // ---------- helpers ----------

    private static void EnsureCanAccess(UserContext ctx, Ticket ticket)
    {
        if (!ctx.IsStaff && ticket.CreatedById != ctx.Id)
            throw new ForbiddenException("You do not have access to this ticket.");
    }

    private void AddHistory(int ticketId, int userId, string action, string? oldValue, string? newValue, DateTime at) =>
        db.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticketId, ChangedById = userId, Action = action, OldValue = oldValue, NewValue = newValue, ChangedAt = at
        });

    private async Task NotifyAsync(int userId, string subject, string body)
    {
        var email = await db.Users.Where(u => u.Id == userId && u.IsActive).Select(u => u.Email).FirstOrDefaultAsync();
        if (email is not null) await mail.SendAsync(email, subject, body);
    }

    private static TicketDetailDto ToDetail(Ticket t, UserContext ctx)
    {
        var overdue = t.DueAt < DateTime.UtcNow && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed;
        return new TicketDetailDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority, t.CategoryId, t.Category.Name,
            t.CreatedById, t.CreatedBy.FullName, t.AssignedToId, t.AssignedTo?.FullName,
            t.CreatedAt, t.UpdatedAt, t.DueAt, t.ResolvedAt, overdue, Convert.ToBase64String(t.RowVersion),
            t.Comments.Where(c => ctx.IsStaff || !c.IsInternal).OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto(c.Id, c.Author.FullName, c.Author.Role, c.Body, c.IsInternal, c.CreatedAt)).ToList(),
            t.Attachments.OrderBy(a => a.UploadedAt)
                .Select(a => new AttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.UploadedBy.FullName, a.UploadedAt)).ToList(),
            t.History.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
                .Select(h => new HistoryDto(h.Action, h.OldValue, h.NewValue, h.ChangedBy.FullName, h.ChangedAt)).ToList());
    }
}
