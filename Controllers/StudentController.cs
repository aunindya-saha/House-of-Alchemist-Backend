using System.Security.Claims;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Models;
using HouseOfAlchemists.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Controllers;

[ApiController]
[Route("api/student")]
[Authorize(Roles = "student")]
public class StudentController(AppDbContext db, StudentService svc) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.Users.Include(u => u.Batch).FirstOrDefaultAsync(u => u.Id == UserId);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.Language, user.Theme,
                        batchId = user.BatchId, batchName = user.Batch?.Name });
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(UserId);
        if (user == null) return NotFound();
        if (req.Name != null) user.Name = req.Name;
        if (req.Phone != null) user.Phone = req.Phone;
        if (req.Language != null) user.Language = req.Language;
        if (req.Theme != null) user.Theme = req.Theme;
        await db.SaveChangesAsync();
        return Ok(new { user.Name, user.Phone, user.Language, user.Theme });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await svc.GetDashboardAsync(UserId));

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var user = await db.Users.Include(u => u.Batch).FirstOrDefaultAsync(u => u.Id == UserId);
        if (user?.Batch == null) return Ok(new List<object>());
        var subjects = await db.Subjects
            .Include(s => s.Papers).ThenInclude(p => p.Chapters).ThenInclude(c => c.Topics)
            .Include(s => s.Chapters).ThenInclude(c => c.Topics)
            .Where(s => s.Level == user.Batch.Level)
            .Select(s => new {
                s.Id, s.Name,
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

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(
        [FromQuery] Guid? subjectId, [FromQuery] Guid? chapterId, [FromQuery] Guid? topicId,
        [FromQuery] string? type, [FromQuery] string? q)
        => Ok(await svc.GetResourcesAsync(UserId, subjectId, chapterId, topicId, type, q));

    [HttpGet("exams")]
    public async Task<IActionResult> GetExams([FromQuery] string? status)
        => Ok(await svc.GetExamsAsync(UserId, status));

    [HttpPost("exams/{examId}/start")]
    public async Task<IActionResult> StartExam(Guid examId)
    {
        try { return Ok(await svc.StartExamAsync(UserId, examId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("exams/{examId}/submit")]
    public async Task<IActionResult> SubmitExam(Guid examId, [FromBody] SubmitExamRequest req)
    {
        try { return Ok(await svc.SubmitExamAsync(UserId, examId, req.Answers)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("exams/{examId}/result")]
    public async Task<IActionResult> GetResult(Guid examId)
    {
        try { return Ok(await svc.GetResultAsync(UserId, examId)); }
        catch (Exception ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] Guid? examId)
        => Ok(await svc.GetLeaderboardAsync(examId, UserId));

    [HttpGet("resources/{resourceId}/comments")]
    public async Task<IActionResult> GetComments(Guid resourceId)
    {
        var comments = await db.Comments.Include(c => c.Student)
            .Where(c => c.ResourceId == resourceId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new {
                c.Id, c.QuestionText, c.ReplyText, c.Status, c.CreatedAt, c.RepliedAt,
                studentId = c.StudentId,
                studentName = c.Student.Name,
                isOwn = c.StudentId == UserId
            })
            .ToListAsync();
        return Ok(comments);
    }

    [HttpPost("resources/{resourceId}/comments")]
    public async Task<IActionResult> PostComment(Guid resourceId, [FromBody] CreateCommentRequest req)
    {
        if (!await db.Resources.AnyAsync(r => r.Id == resourceId)) return NotFound();
        var comment = new Comment { ResourceId = resourceId, StudentId = UserId, QuestionText = req.QuestionText };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return Ok(new { comment.Id, comment.QuestionText, comment.Status, comment.CreatedAt, studentId = UserId, isOwn = true });
    }

    [HttpPatch("comments/{id}")]
    public async Task<IActionResult> EditComment(Guid id, [FromBody] EditCommentRequest req)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.StudentId == UserId);
        if (comment == null) return NotFound();
        comment.QuestionText = req.QuestionText;
        await db.SaveChangesAsync();
        return Ok(new { comment.Id, comment.QuestionText });
    }
}
