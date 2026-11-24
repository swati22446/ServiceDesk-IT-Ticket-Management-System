using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Services;

public interface IAttachmentService
{
    Task<AttachmentDto> UploadAsync(UserContext ctx, int ticketId, IFormFile file);
    Task<DownloadResult> GetAsync(UserContext ctx, int attachmentId);
}

public class AttachmentService(
    AppDbContext db,
    IFileStorage storage,
    IOptions<StorageSettings> options,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    private const int MaxFilesPerTicket = 10;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".pdf", ".txt", ".log", ".docx", ".xlsx", ".csv"
    };

    public async Task<AttachmentDto> UploadAsync(UserContext ctx, int ticketId, IFormFile file)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new NotFoundException($"Ticket {ticketId} was not found.");
        if (!ctx.IsStaff && ticket.CreatedById != ctx.Id)
            throw new ForbiddenException("You do not have access to this ticket.");

        if (file is null || file.Length == 0) throw new BusinessRuleException("The file is empty.");
        var maxBytes = options.Value.MaxFileSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes) throw new BusinessRuleException($"File is larger than {options.Value.MaxFileSizeMb} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new BusinessRuleException($"File type is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        if (await db.TicketAttachments.CountAsync(a => a.TicketId == ticketId) >= MaxFilesPerTicket)
            throw new BusinessRuleException($"A ticket can have at most {MaxFilesPerTicket} attachments.");

        string storedName;
        await using (var stream = file.OpenReadStream())
            storedName = await storage.SaveAsync(stream, extension);

        var uploader = await db.Users.FirstAsync(u => u.Id == ctx.Id);
        var attachment = new TicketAttachment
        {
            TicketId = ticketId,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            UploadedById = ctx.Id,
            UploadedAt = DateTime.UtcNow
        };
        db.TicketAttachments.Add(attachment);
        await db.SaveChangesAsync();
        logger.LogInformation("Attachment {AttachmentId} ({Size} bytes) uploaded to ticket {TicketId}",
            attachment.Id, attachment.SizeBytes, ticketId);

        return new AttachmentDto(attachment.Id, attachment.OriginalFileName, attachment.ContentType,
            attachment.SizeBytes, uploader.FullName, attachment.UploadedAt);
    }

    public async Task<DownloadResult> GetAsync(UserContext ctx, int attachmentId)
    {
        var attachment = await db.TicketAttachments.AsNoTracking().Include(a => a.Ticket)
                             .FirstOrDefaultAsync(a => a.Id == attachmentId)
                         ?? throw new NotFoundException("Attachment not found.");
        if (!ctx.IsStaff && attachment.Ticket.CreatedById != ctx.Id)
            throw new ForbiddenException("You do not have access to this attachment.");

        var path = storage.GetFullPath(attachment.StoredFileName);
        if (!File.Exists(path)) throw new NotFoundException("The file is no longer available.");

        // Always served as a download (octet-stream) so uploaded HTML/SVG can never run in the browser.
        return new DownloadResult(path, attachment.OriginalFileName, "application/octet-stream");
    }
}
