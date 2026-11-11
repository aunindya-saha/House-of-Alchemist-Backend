using System.Security.Claims;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "superadmin")]
public class AdminController(AppDbContext db, AdminService svc, EmailService email) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.Users.FindAsync(AdminId);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.Name, user.Email, user.Language, user.Theme, user.Role });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await svc.GetDashboardAsync());

    // ── Batches ───────────────────────────────────────────────────────────────
    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches()
        => Ok(await svc.GetBatchesAsync());

    [HttpPost("batches")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest req)
    {
        var batch = await svc.CreateBatchAsync(req);
        return Ok(new { batch.Id, batch.Name, batch.Level, batch.Year, batch.Medium,
            studentCount = 0, teacherIds = req.TeacherIds ?? new List<Guid>() });
    }

    [HttpPut("batches/{id}")]
    public async Task<IActionResult> UpdateBatch(Guid id, [FromBody] UpdateBatchRequest req)
    {
        if (!await svc.UpdateBatchAsync(id, req)) return NotFound();
        var updated = await svc.GetBatchesAsync();
        var batch = (updated as System.Collections.IEnumerable)?.Cast<dynamic>().FirstOrDefault(b => b.Id == id);
        return Ok(batch);
    }

    [HttpDelete("batches/{id}")]
    public async Task<IActionResult> DeleteBatch(Guid id)
        => await svc.DeleteBatchAsync(id) ? Ok() : NotFound();

    [HttpGet("batches/{id}/students")]
    public async Task<IActionResult> GetBatchStudents(Guid id)
        => Ok(await svc.GetBatchStudentsAsync(id));

    // ── Teachers ──────────────────────────────────────────────────────────────
    [HttpGet("teachers")]
    public async Task<IActionResult> GetTeachers()
        => Ok(await svc.GetTeachersAsync());

    [HttpPost("teachers")]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherRequest req)
    {
        try
        {
            var (teacher, tempPassword) = await svc.CreateTeacherAsync(req);
            await email.SendTempPasswordAsync(teacher.Email, teacher.Name, tempPassword);
            return Ok(new { teacher.Id, teacher.Name, teacher.Email, teacher.Phone,
                teacher.Language, teacher.MustChangePassword, teacher.IsActive,
                batchCount = 0, tempPassword });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("teachers/{id}/reset-password")]
    public async Task<IActionResult> ResetTeacherPassword(Guid id)
    {
        var (ok, tempPassword) = await svc.ResetTeacherPasswordWithValueAsync(id);
        if (!ok) return NotFound();
        var teacher = await db.Users.FindAsync(id);
        if (teacher != null) await email.SendTempPasswordAsync(teacher.Email, teacher.Name, tempPassword);
        return Ok(new { tempPassword });
    }

    [HttpDelete("teachers/{id}")]
    public async Task<IActionResult> DeleteTeacher(Guid id)
        => await svc.DeleteTeacherAsync(id) ? Ok() : NotFound();

    // ── Students ──────────────────────────────────────────────────────────────
    [HttpGet("students")]
    public async Task<IActionResult> GetStudents()
        => Ok(await svc.GetStudentsAsync());

    [HttpPatch("students/{id}")]
    public async Task<IActionResult> AssignBatch(Guid id, [FromBody] AssignStudentBatchRequest req)
    {
        if (!await svc.AssignStudentBatchAsync(id, req.BatchId)) return NotFound();
        var student = await db.Users.Include(u => u.Batch).FirstOrDefaultAsync(u => u.Id == id);
        return Ok(new { student!.Id, student.Name, student.Email, student.Language,
            student.IsActive, batchId = student.BatchId, batchName = student.Batch?.Name });
    }

    // ── Access control ────────────────────────────────────────────────────────
    [HttpPatch("users/{id}/toggle-access")]
    public async Task<IActionResult> ToggleAccess(Guid id)
        => await svc.ToggleUserAccessAsync(id) ? Ok() : NotFound();

    // ── Subjects ──────────────────────────────────────────────────────────────
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
        => Ok(await svc.GetSubjectsAsync());

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest req)
    {
        var subject = await svc.CreateSubjectAsync(req);
        return Ok(new { subject.Id, subject.Name, subject.Level });
    }

    [HttpPut("subjects/{id}")]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequest req)
        => await svc.UpdateSubjectAsync(id, req) ? Ok() : NotFound();

    [HttpDelete("subjects/{id}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
        => await svc.DeleteSubjectAsync(id) ? Ok() : NotFound();

    // ── Papers ────────────────────────────────────────────────────────────────
    [HttpGet("papers")]
    public async Task<IActionResult> GetPapers([FromQuery] Guid subjectId)
        => Ok(await svc.GetPapersAsync(subjectId));

    [HttpPost("papers")]
    public async Task<IActionResult> CreatePaper([FromBody] CreatePaperRequest req)
    {
        var paper = await svc.CreatePaperAsync(req);
        return Ok(new { paper.Id, paper.Name, paper.Order, paper.SubjectId, chapterCount = 0 });
    }

    [HttpPut("papers/{id}")]
    public async Task<IActionResult> UpdatePaper(Guid id, [FromBody] UpdatePaperRequest req)
        => await svc.UpdatePaperAsync(id, req) ? Ok() : NotFound();

    [HttpDelete("papers/{id}")]
    public async Task<IActionResult> DeletePaper(Guid id)
        => await svc.DeletePaperAsync(id) ? Ok() : NotFound();

    // ── Chapters ──────────────────────────────────────────────────────────────
    [HttpGet("chapters")]
    public async Task<IActionResult> GetChapters([FromQuery] Guid subjectId)
        => Ok(await svc.GetChaptersAsync(subjectId));

    [HttpPost("chapters")]
    public async Task<IActionResult> CreateChapter([FromBody] CreateChapterRequest req)
    {
        var chapter = await svc.CreateChapterAsync(req);
        return Ok(new { chapter.Id, chapter.Number, chapter.Name, chapter.SubjectId, chapter.PaperId, topicCount = 0 });
    }

    [HttpPut("chapters/{id}")]
    public async Task<IActionResult> UpdateChapter(Guid id, [FromBody] UpdateChapterRequest req)
        => await svc.UpdateChapterAsync(id, req) ? Ok() : NotFound();

    [HttpDelete("chapters/{id}")]
    public async Task<IActionResult> DeleteChapter(Guid id)
        => await svc.DeleteChapterAsync(id) ? Ok() : NotFound();

    // ── Topics ────────────────────────────────────────────────────────────────
    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics([FromQuery] Guid chapterId)
        => Ok(await svc.GetTopicsAsync(chapterId));

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic([FromBody] CreateTopicRequest req)
    {
        var topic = await svc.CreateTopicAsync(req);
        return Ok(new { topic.Id, topic.Name, topic.Order, topic.ChapterId, materialCount = 0 });
    }

    [HttpPut("topics/{id}")]
    public async Task<IActionResult> UpdateTopic(Guid id, [FromBody] UpdateTopicRequest req)
        => await svc.UpdateTopicAsync(id, req) ? Ok() : NotFound();

    [HttpDelete("topics/{id}")]
    public async Task<IActionResult> DeleteTopic(Guid id)
        => await svc.DeleteTopicAsync(id) ? Ok() : NotFound();
}
