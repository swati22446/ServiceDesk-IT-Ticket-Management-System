using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tickets")]
public class TicketsController(ITicketService tickets, IAttachmentService attachments) : ControllerBase
{
    /// <summary>Search, filter, sort and page tickets. Normal users only ever see their own.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketDto>>> List([FromQuery] TicketQuery query) =>
        Ok(await tickets.ListAsync(User.ToUserContext(), query));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> Get(int id) =>
        Ok(await tickets.GetAsync(User.ToUserContext(), id));

    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(CreateTicketRequest request)
    {
        var created = await tickets.CreateAsync(User.ToUserContext(), request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> Update(int id, UpdateTicketRequest request) =>
        Ok(await tickets.UpdateAsync(User.ToUserContext(), id, request));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TicketDetailDto>> ChangeStatus(int id, ChangeStatusRequest request) =>
        Ok(await tickets.ChangeStatusAsync(User.ToUserContext(), id, request.Status));

    [HttpPatch("{id:int}/assign")]
    [Authorize(Policy = "Staff")]
    public async Task<ActionResult<TicketDetailDto>> Assign(int id, AssignTicketRequest request) =>
        Ok(await tickets.AssignAsync(User.ToUserContext(), id, request.AgentId));

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(int id, AddCommentRequest request) =>
        Ok(await tickets.AddCommentAsync(User.ToUserContext(), id, request));

    [HttpPost("{id:int}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(int id, IFormFile file) =>
        Ok(await attachments.UploadAsync(User.ToUserContext(), id, file));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        await tickets.DeleteAsync(User.ToUserContext(), id);
        return NoContent();
    }
}
