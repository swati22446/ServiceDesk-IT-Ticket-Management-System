using Microsoft.EntityFrameworkCore;
using Moq;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests;

public class TicketServiceTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 24)]
    [InlineData(TicketPriority.Medium, 48)]
    [InlineData(TicketPriority.Low, 72)]
    public void SlaHours_MatchesPriority(TicketPriority priority, int expected) =>
        Assert.Equal(expected, TicketService.SlaHours(priority));

    [Fact]
    public async Task Create_SetsOpenStatus_DueDate_AndAuditEntry()
    {
        var env = new TestEnv();
        var request = new CreateTicketRequest
        {
            Title = "  VPN not working  ", Description = "Cannot connect from home network.",
            CategoryId = env.CategoryId, Priority = TicketPriority.Critical
        };

        var ticket = await env.Tickets.CreateAsync(env.Alice, request);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal("VPN not working", ticket.Title);                       // trimmed
        Assert.InRange((ticket.DueAt - ticket.CreatedAt).TotalHours, 3.99, 4.01); // Critical SLA = 4h
        Assert.Single(ticket.History);
        Assert.Equal("Created", ticket.History[0].Action);
        env.Mail.Verify(m => m.SendAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("received")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_Throws()
    {
        var env = new TestEnv();
        var request = new CreateTicketRequest { Title = "Valid title", Description = "Valid description text", CategoryId = 9999 };
        await Assert.ThrowsAsync<BusinessRuleException>(() => env.Tickets.CreateAsync(env.Alice, request));
    }

    [Fact]
    public async Task Get_AnotherUsersTicket_IsForbidden()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.GetAsync(env.Bob, id));
    }

    [Fact]
    public async Task Get_Missing_ThrowsNotFound()
    {
        var env = new TestEnv();
        await Assert.ThrowsAsync<NotFoundException>(() => env.Tickets.GetAsync(env.Admin, 12345));
    }

    [Fact]
    public async Task Get_StaffCanSeeAnyTicket()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        var ticket = await env.Tickets.GetAsync(env.Agent, id);
        Assert.Equal(id, ticket.Id);
    }

    [Fact]
    public async Task List_NormalUserOnlySeesOwnTickets()
    {
        var env = new TestEnv();
        await env.CreateTicketAsync(env.Alice);
        await env.CreateTicketAsync(env.Alice);
        await env.CreateTicketAsync(env.Bob);

        var aliceList = await env.Tickets.ListAsync(env.Alice, new TicketQuery());
        var staffList = await env.Tickets.ListAsync(env.Agent, new TicketQuery());

        Assert.Equal(2, aliceList.Total);
        Assert.Equal(3, staffList.Total);
    }

    [Fact]
    public async Task List_FiltersSearchesSortsAndPages()
    {
        var env = new TestEnv();
        await env.CreateTicketAsync(env.Alice, new CreateTicketRequest { Title = "Printer jam", Description = "Paper stuck inside printer", CategoryId = env.CategoryId, Priority = TicketPriority.Low });
        await env.CreateTicketAsync(env.Alice, new CreateTicketRequest { Title = "Server down", Description = "Production is unreachable", CategoryId = env.CategoryId, Priority = TicketPriority.Critical });
        await env.CreateTicketAsync(env.Alice, new CreateTicketRequest { Title = "Mouse broken", Description = "Left click does not work", CategoryId = env.CategoryId, Priority = TicketPriority.Medium });

        var search = await env.Tickets.ListAsync(env.Agent, new TicketQuery { Search = "printer" });
        Assert.Single(search.Items);

        var critical = await env.Tickets.ListAsync(env.Agent, new TicketQuery { Priority = TicketPriority.Critical });
        Assert.Single(critical.Items);
        Assert.Equal("Server down", critical.Items[0].Title);

        var sorted = await env.Tickets.ListAsync(env.Agent, new TicketQuery { SortBy = "priority", Desc = true });
        Assert.Equal(TicketPriority.Critical, sorted.Items[0].Priority);

        var paged = await env.Tickets.ListAsync(env.Agent, new TicketQuery { Page = 2, PageSize = 2 });
        Assert.Single(paged.Items);
        Assert.Equal(3, paged.Total);
        Assert.Equal(2, paged.TotalPages);
    }

    [Fact]
    public async Task Update_OwnerCanEditOpenTicket_AndPriorityChangeRecalculatesSla()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);

        var updated = await env.Tickets.UpdateAsync(env.Alice, id, new UpdateTicketRequest
        {
            Title = "Laptop is really broken", Description = "Now it will not even turn on.",
            CategoryId = env.CategoryId, Priority = TicketPriority.Low
        });

        Assert.Equal("Laptop is really broken", updated.Title);
        Assert.Equal(TicketPriority.Low, updated.Priority);
        Assert.InRange((updated.DueAt - updated.CreatedAt).TotalHours, 71.99, 72.01);
        Assert.Contains(updated.History, h => h.Action == "Priority changed");
    }

    [Fact]
    public async Task Update_OwnerCannotEditOnceInProgress()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await env.Tickets.ChangeStatusAsync(env.Agent, id, TicketStatus.InProgress);

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.UpdateAsync(env.Alice, id, new UpdateTicketRequest
        {
            Title = "New title here", Description = "New description here", CategoryId = env.CategoryId
        }));
    }

    [Fact]
    public async Task Update_WithInvalidRowVersion_Throws()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<BusinessRuleException>(() => env.Tickets.UpdateAsync(env.Alice, id, new UpdateTicketRequest
        {
            Title = "Some title", Description = "Some description", CategoryId = env.CategoryId, RowVersion = "***not-base64***"
        }));
    }

    [Fact]
    public async Task ChangeStatus_StaffResolve_SetsResolvedAt_AndNotifiesRequester()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        env.Mail.Invocations.Clear();

        var ticket = await env.Tickets.ChangeStatusAsync(env.Agent, id, TicketStatus.Resolved);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.NotNull(ticket.ResolvedAt);
        env.Mail.Verify(m => m.SendAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Resolved")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_Reopen_ClearsResolvedAt()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await env.Tickets.ChangeStatusAsync(env.Agent, id, TicketStatus.Resolved);

        var reopened = await env.Tickets.ChangeStatusAsync(env.Alice, id, TicketStatus.Open);

        Assert.Equal(TicketStatus.Open, reopened.Status);
        Assert.Null(reopened.ResolvedAt);
    }

    [Fact]
    public async Task ChangeStatus_RequesterCannotMoveOpenTicketToInProgress()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.ChangeStatusAsync(env.Alice, id, TicketStatus.InProgress));
    }

    [Fact]
    public async Task ChangeStatus_RequesterCanCloseResolvedTicket()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await env.Tickets.ChangeStatusAsync(env.Agent, id, TicketStatus.Resolved);
        var closed = await env.Tickets.ChangeStatusAsync(env.Alice, id, TicketStatus.Closed);
        Assert.Equal(TicketStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_Throws()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<BusinessRuleException>(() => env.Tickets.ChangeStatusAsync(env.Agent, id, TicketStatus.Open));
    }

    [Fact]
    public async Task Assign_SetsAssignee_MovesToInProgress_AndLogsHistory()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);

        var ticket = await env.Tickets.AssignAsync(env.Admin, id, env.Agent.Id);

        Assert.Equal(env.Agent.Id, ticket.AssignedToId);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Contains(ticket.History, h => h.Action == "Assigned");
    }

    [Fact]
    public async Task Assign_ByNormalUser_IsForbidden()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.AssignAsync(env.Alice, id, env.Agent.Id));
    }

    [Fact]
    public async Task Assign_ToNormalUser_IsRejected()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);
        await Assert.ThrowsAsync<BusinessRuleException>(() => env.Tickets.AssignAsync(env.Agent, id, env.Bob.Id));
    }

    [Fact]
    public async Task Comments_InternalNotesAreHiddenFromRequester()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);

        await env.Tickets.AddCommentAsync(env.Agent, id, new AddCommentRequest { Body = "Public reply", IsInternal = false });
        await env.Tickets.AddCommentAsync(env.Agent, id, new AddCommentRequest { Body = "Secret note", IsInternal = true });

        var asRequester = await env.Tickets.GetAsync(env.Alice, id);
        var asStaff = await env.Tickets.GetAsync(env.Agent, id);

        Assert.Single(asRequester.Comments);
        Assert.Equal(2, asStaff.Comments.Count);
    }

    [Fact]
    public async Task Comments_RequesterCannotCreateInternalNote()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);

        var comment = await env.Tickets.AddCommentAsync(env.Alice, id, new AddCommentRequest { Body = "Trying a sneaky note", IsInternal = true });

        Assert.False(comment.IsInternal);
    }

    [Fact]
    public async Task Delete_OnlyAdmin_AndRemovesTicketAndFiles()
    {
        var env = new TestEnv();
        var id = await env.CreateTicketAsync(env.Alice);

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.DeleteAsync(env.Agent, id));

        env.Db.TicketAttachments.Add(new TicketAttachment
        {
            TicketId = id, OriginalFileName = "log.txt", StoredFileName = "abc.txt", ContentType = "text/plain",
            SizeBytes = 10, UploadedById = env.Alice.Id, UploadedAt = DateTime.UtcNow
        });
        await env.Db.SaveChangesAsync();

        await env.Tickets.DeleteAsync(env.Admin, id);

        Assert.False(await env.Db.Tickets.AnyAsync(t => t.Id == id));
        env.Storage.Verify(s => s.Delete("abc.txt"), Times.Once);
    }

    [Fact]
    public async Task Dashboard_AggregatesCounts_AndIsStaffOnly()
    {
        var env = new TestEnv();
        var a = await env.CreateTicketAsync(env.Alice);
        await env.CreateTicketAsync(env.Bob);
        await env.Tickets.ChangeStatusAsync(env.Agent, a, TicketStatus.Resolved);

        var dashboard = await env.Tickets.GetDashboardAsync(env.Admin);

        Assert.Equal(2, dashboard.Total);
        Assert.Equal(1, dashboard.Open);
        Assert.Equal(1, dashboard.Resolved);
        Assert.Equal(2, dashboard.Unassigned); // neither ticket has been assigned
        Assert.Equal(7, dashboard.Last7Days.Count);
        Assert.Equal(5, dashboard.ByStatus.Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Tickets.GetDashboardAsync(env.Alice));
    }
}
