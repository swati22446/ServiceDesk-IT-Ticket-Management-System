using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attachments")]
public class AttachmentsController(IAttachmentService attachments) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var file = await attachments.GetAsync(User.ToUserContext(), id);
        return PhysicalFile(file.Path, file.ContentType, file.FileName);
    }
}

[ApiController]
[Authorize(Policy = "Staff")]
[Route("api/v1/dashboard")]
public class DashboardController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get() =>
        Ok(await tickets.GetDashboardAsync(User.ToUserContext()));
}

[ApiController]
[Authorize(Policy = "Staff")]
[Route("api/v1/reports")]
public class ReportsController(IReportService reports) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Excel export of the tickets matching the same filters as the list endpoint.</summary>
    [HttpGet("tickets/excel")]
    public async Task<IActionResult> TicketsExcel([FromQuery] TicketQuery query)
    {
        var bytes = await reports.ExportTicketsAsync(User.ToUserContext(), query);
        return File(bytes, XlsxType, $"tickets-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }
}

[ApiController]
[Route("api/v1/lookups")]
public class LookupsController(ICategoryService categories, IUserService users) : ControllerBase
{
    [HttpGet("categories")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories() => Ok(await categories.ListAsync());

    /// <summary>Public so the registration form can show the list.</summary>
    [HttpGet("departments")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> Departments() => Ok(await users.ListDepartmentsAsync());

    [HttpGet("agents")]
    [Authorize(Policy = "Staff")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Agents() => Ok(await users.ListAgentsAsync());
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin")]
public class AdminController(IUserService users, ICategoryService categories) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Users() => Ok(await users.ListAsync());

    [HttpPatch("users/{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleRequest request)
    {
        await users.ChangeRoleAsync(User.ToUserContext(), id, request.Role);
        return NoContent();
    }

    [HttpPatch("users/{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ChangeActiveRequest request)
    {
        await users.SetActiveAsync(User.ToUserContext(), id, request.IsActive);
        return NoContent();
    }

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryRequest request) =>
        Ok(await categories.CreateAsync(request));

    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryRequest request) =>
        Ok(await categories.UpdateAsync(id, request));

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await categories.DeleteAsync(id);
        return NoContent();
    }
}
