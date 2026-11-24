using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Services;

/// <summary>Shared filtering/sorting so the list endpoint and the Excel export always agree.</summary>
public static class TicketQueryExtensions
{
    public static IQueryable<Ticket> ApplyFilters(this IQueryable<Ticket> q, TicketQuery f, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            q = q.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }
        if (f.Status.HasValue)
        {
            var status = f.Status.Value;
            q = q.Where(t => t.Status == status);
        }
        if (f.Priority.HasValue)
        {
            var priority = f.Priority.Value;
            q = q.Where(t => t.Priority == priority);
        }
        if (f.CategoryId.HasValue)
        {
            var categoryId = f.CategoryId.Value;
            q = q.Where(t => t.CategoryId == categoryId);
        }
        if (f.AssignedToId.HasValue)
        {
            var assignedId = f.AssignedToId.Value;
            q = q.Where(t => t.AssignedToId == assignedId);
        }
        if (f.OverdueOnly == true)
            q = q.Where(t => t.DueAt < now && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        return q;
    }

    public static IQueryable<Ticket> ApplySort(this IQueryable<Ticket> q, string? sortBy, bool desc)
    {
        IOrderedQueryable<Ticket> ordered = sortBy?.ToLowerInvariant() switch
        {
            "dueat" => desc ? q.OrderByDescending(t => t.DueAt) : q.OrderBy(t => t.DueAt),
            "priority" => desc ? q.OrderByDescending(t => t.Priority) : q.OrderBy(t => t.Priority),
            "status" => desc ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            "title" => desc ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title),
            _ => desc ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt)
        };
        return ordered.ThenBy(t => t.Id);
    }
}
