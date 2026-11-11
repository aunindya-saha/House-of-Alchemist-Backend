using System.Security.Claims;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "teacher")]
public class TeacherController(AppDbContext db, TeacherService svc, FileUploadService uploads, EmailService email) : ControllerBase
{
    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.Users.FindAsync(TeacherId);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.Language, user.Theme, user.MustChangePassword });
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(TeacherId);
        if (user == null) return NotFound();
        if (req.Name != null) user.Name = req.Name;
        if (req.Phone != null) user.Phone = req.Phone;
        if (req.Language != null) user.Language = req.Language;
        if (req.Theme != null) user.Theme = req.Theme;
        await db.SaveChangesAsync();
        return Ok(new { user.Name, user.Language, user.Theme });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await svc.GetDashboardAsync(TeacherId));

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches()
        => Ok(await svc.GetBatchesAsync(TeacherId));

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var levels = await db.BatchTeachers.Include(bt => bt.Batch)
            .Where(bt => bt.TeacherId == TeacherId).Select(bt => bt.Batch.Level).Distinct().ToListAsync();
        var subjects = await db.Subjects
            .Include(s => s.Papers).ThenInclude(p => p.Chapters).ThenInclude(c => c.Topics)
            .Include(s => s.Chapters).ThenInclude(c => c.Topics)
            .Where(s => levels.Contains(s.Level))
            .Select(s => new {
                s.Id, s.Name, s.Level,
                papers = s.Papers.OrderBy(p => p.Order).Select(p => new {
                    p.Id, p.Name, p.Order,
                    chapters = p.Chapters.OrderBy(c => c.Number).Select(c => new {
                        c.Id, c.Number, c.Name, c.PaperId,
                        topics = c.Topics.OrderBy(t => t.Order).Select(t => new { t.Id, t.Name, t.Order })
                    })
                }),
                chapters = s.Chapters.Where(c => c.PaperId == null).OrderBy(c => c.Number).Select(c => new {
                    c.Id, c.Number, c.Name, c.PaperId,
                    topics = c.Topics.OrderBy(t => t.Order).Select(t => new { t.Id, t.Name, t.Order })
                })
            })
            .ToListAsync();
        return Ok(subjects);
    }

    // ── Resources ──────────────────────────────────────────────────────────────
    [HttpGet("resources")]
    public async Task<IActionResult> GetResources([FromQuery] Guid? subjectId, [FromQuery] Guid? chapterId)
        => Ok(await svc.GetResourcesAsync(TeacherId, subjectId, chapterId));

    [HttpPost("resources")]
    public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest req)
    {
        var resource = await svc.CreateResourceAsync(TeacherId, req);
        return Ok(new { resource.Id, resource.Title, resource.Type });
    }

    [HttpPut("resources/{id}")]
    public async Task<IActionResult> UpdateResource(Guid id, [FromBody] UpdateResourceRequest req)
    {
        var result = await svc.UpdateResourceAsync(TeacherId, id, req);
        return result == null ? NotFound() : Ok(new { result.Id, result.Title });
    }

    [HttpDelete("resources/{id}")]
    public async Task<IActionResult> DeleteResource(Guid id)
        => await svc.DeleteResourceAsync(TeacherId, id) ? Ok() : NotFound();

    [HttpPost("resources/upload")]
    public async Task<IActionResult> UploadPdf(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "No file provided" });
        if (!file.ContentType.Contains("pdf")) return BadRequest(new { message = "Only PDF files allowed" });
        var url = await uploads.UploadAsync(file, "pdfs");
        return url == null ? StatusCode(503, new { message = "File storage not configured" }) : Ok(new { url });
    }

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic([FromBody] CreateTopicRequest req)
    {
        var topic = await svc.CreateTopicAsync(TeacherId, req);
        return Ok(new { topic.Id, topic.Name, topic.Order, topic.ChapterId });
    }

    // ── Questions ──────────────────────────────────────────────────────────────
    [HttpGet("questions")]
    public async Task<IActionResult> GetQuestions([FromQuery] Guid? subjectId, [FromQuery] Guid? chapterId, [FromQuery] string? q)
        => Ok(await svc.GetQuestionsAsync(TeacherId, subjectId, chapterId, q));

    [HttpPost("questions/parse-csv")]
    public async Task<IActionResult> ParseCsv(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "No file provided" });
        using var stream = file.OpenReadStream();
        var rows = svc.ParseCsv(stream);
        return Ok(new { count = rows.Count, preview = rows.Take(5), rows });
    }

    [HttpPost("questions/import")]
    public async Task<IActionResult> ImportQuestions([FromBody] ImportQuestionsRequest req)
    {
        var count = await svc.ImportQuestionsAsync(TeacherId, req);
        return Ok(new { imported = count });
    }

    [HttpPatch("questions/{id}")]
    public async Task<IActionResult> UpdateQuestion(Guid id, [FromBody] UpdateQuestionRequest req)
        => await svc.UpdateQuestionAsync(TeacherId, id, req) ? Ok() : NotFound();

    [HttpDelete("questions/{id}")]
    public async Task<IActionResult> DeleteQuestion(Guid id)
        => await svc.DeleteQuestionAsync(TeacherId, id) ? Ok() : NotFound();

    [HttpPost("questions/upload-diagram")]
    public async Task<IActionResult> UploadDiagram(IFormFile file)
    {
        if (file == null) return BadRequest();
        var url = await uploads.UploadAsync(file, "diagrams");
        return url == null ? StatusCode(503, new { message = "File storage not configured" }) : Ok(new { url });
    }

    // ── Exams ──────────────────────────────────────────────────────────────────
    [HttpGet("exams")]
    public async Task<IActionResult> GetExams()
        => Ok(await svc.GetExamsAsync(TeacherId));

    [HttpPost("exams")]
    public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest req)
    {
        var exam = await svc.CreateExamAsync(TeacherId, req);
        return Ok(new { exam.Id, exam.Title, exam.Status });
    }

    [HttpPut("exams/{id}")]
    public async Task<IActionResult> UpdateExam(Guid id, [FromBody] UpdateExamRequest req)
        => await svc.UpdateExamAsync(TeacherId, id, req) ? Ok() : NotFound();

    [HttpDelete("exams/{id}")]
    public async Task<IActionResult> DeleteExam(Guid id)
        => await svc.DeleteExamAsync(TeacherId, id) ? Ok() : NotFound();

    [HttpPatch("exams/{id}/publish")]
    public async Task<IActionResult> PublishExam(Guid id)
        => await svc.PublishExamAsync(TeacherId, id) ? Ok(new { message = "Exam published" }) : NotFound();

    [HttpPatch("exams/{id}/close")]
    public async Task<IActionResult> CloseExam(Guid id)
        => await svc.CloseExamAsync(TeacherId, id) ? Ok(new { message = "Exam closed" }) : NotFound();

    [HttpPatch("exams/{id}/reopen")]
    public async Task<IActionResult> ReopenExam(Guid id)
        => await svc.ReopenExamAsync(TeacherId, id) ? Ok(new { message = "Exam reopened" }) : NotFound();

    [HttpGet("exams/{id}/results")]
    public async Task<IActionResult> GetExamResults(Guid id)
        => Ok(await svc.GetExamResultsAsync(id));

    // ── Students ──────────────────────────────────────────────────────────────
    [HttpGet("batches/{batchId}/students")]
    public async Task<IActionResult> GetStudents(Guid batchId)
    {
        try { return Ok(await svc.GetBatchStudentsAsync(TeacherId, batchId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── Comments ──────────────────────────────────────────────────────────────
    [HttpGet("comments")]
    public async Task<IActionResult> GetComments([FromQuery] string? status)
        => Ok(await svc.GetCommentsAsync(TeacherId, status));

    [HttpPatch("comments/{id}/reply")]
    public async Task<IActionResult> EditReply(Guid id, [FromBody] EditReplyRequest req)
    {
        var ids = await db.Resources.Where(r => r.TeacherId == TeacherId).Select(r => r.Id).ToListAsync();
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id && ids.Contains(c.ResourceId));
        if (comment == null) return NotFound();
        comment.ReplyText = req.ReplyText;
        comment.RepliedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { comment.Id, comment.ReplyText });
    }

    [HttpPost("comments/{id}/reply")]
    public async Task<IActionResult> ReplyToComment(Guid id, [FromBody] ReplyCommentRequest req)
    {
        var comment = await db.Comments.Include(c => c.Student).FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null) return NotFound();
        var (ok, studentEmail, studentName) = await svc.ReplyToCommentAsync(TeacherId, id, req.ReplyText);
        if (!ok) return NotFound();
        await email.SendCommentReplyAsync(studentEmail, studentName, comment.QuestionText, req.ReplyText);
        return Ok(new { message = "Reply sent" });
    }
}
