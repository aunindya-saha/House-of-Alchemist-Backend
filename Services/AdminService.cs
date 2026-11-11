using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Services;

public class AdminService(AppDbContext db)
{
    public async Task<object> GetDashboardAsync()
    {
        var batchCount = await db.Batches.CountAsync();
        var teacherCount = await db.Users.CountAsync(u => u.Role == "teacher");
        var studentCount = await db.Users.CountAsync(u => u.Role == "student");
        var examCount = await db.Exams.CountAsync();
        var recentBatches = await db.Batches.OrderByDescending(b => b.CreatedAt).Take(5)
            .Select(b => new { b.Id, b.Name, b.Level, b.Medium, studentCount = b.Students.Count }).ToListAsync();
        var recentTeachers = await db.Users.Where(u => u.Role == "teacher")
            .OrderByDescending(u => u.CreatedAt).Take(5)
            .Select(u => new { u.Id, u.Name, u.Email, u.Language, u.MustChangePassword, u.IsActive }).ToListAsync();
        return new {
            stats = new { batches = batchCount, teachers = teacherCount, students = studentCount, exams = examCount },
            recentBatches,
            recentTeachers
        };
    }

    // ── Batches ───────────────────────────────────────────────────────────────
    public async Task<object> GetBatchesAsync()
        => await db.Batches.Include(b => b.BatchTeachers).ThenInclude(bt => bt.Teacher)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.Id, b.Name, b.Level, b.Year, b.Medium,
                studentCount = b.Students.Count,
                teachers = b.BatchTeachers.Select(bt => new { bt.Teacher.Id, bt.Teacher.Name }) })
            .ToListAsync();

    public async Task<Batch> CreateBatchAsync(CreateBatchRequest req)
    {
        var batch = new Batch { Name = req.Name, Level = req.Level, Year = req.Year, Medium = req.Medium };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        if (req.TeacherIds?.Any() == true)
        {
            db.BatchTeachers.AddRange(req.TeacherIds.Select(tid => new BatchTeacher { BatchId = batch.Id, TeacherId = tid }));
            await db.SaveChangesAsync();
        }
        return batch;
    }

    public async Task<bool> UpdateBatchAsync(Guid id, UpdateBatchRequest req)
    {
        var batch = await db.Batches.Include(b => b.BatchTeachers).FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null) return false;
        if (req.Name != null) batch.Name = req.Name;
        if (req.Level != null) batch.Level = req.Level;
        if (req.Year != null) batch.Year = req.Year;
        if (req.Medium != null) batch.Medium = req.Medium;
        if (req.TeacherIds != null)
        {
            db.BatchTeachers.RemoveRange(batch.BatchTeachers);
            db.BatchTeachers.AddRange(req.TeacherIds.Select(tid => new BatchTeacher { BatchId = id, TeacherId = tid }));
        }
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteBatchAsync(Guid id)
    {
        var batch = await db.Batches.FindAsync(id);
        if (batch == null) return false;
        var students = await db.Users.Where(u => u.BatchId == id).ToListAsync();
        foreach (var s in students) s.BatchId = null;
        db.Batches.Remove(batch);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Teachers ──────────────────────────────────────────────────────────────
    public async Task<List<object>> GetTeachersAsync()
        => await db.Users.Where(u => u.Role == "teacher")
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => (object)new { u.Id, u.Name, u.Email, u.Phone, u.Language,
                u.MustChangePassword, u.IsActive, u.CreatedAt,
                batchCount = db.BatchTeachers.Count(bt => bt.TeacherId == u.Id) })
            .ToListAsync();

    public async Task<bool> ResetTeacherPasswordAsync(Guid id)
    {
        var teacher = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "teacher");
        if (teacher == null) return false;
        var tempPassword = GenerateTempPassword();
        teacher.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        teacher.MustChangePassword = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool ok, string tempPassword)> ResetTeacherPasswordWithValueAsync(Guid id)
    {
        var teacher = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "teacher");
        if (teacher == null) return (false, "");
        var tempPassword = GenerateTempPassword();
        teacher.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        teacher.MustChangePassword = true;
        await db.SaveChangesAsync();
        return (true, tempPassword);
    }

    public async Task<bool> ToggleUserAccessAsync(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role != "superadmin");
        if (user == null) return false;
        user.IsActive = !user.IsActive;
        if (!user.IsActive) { user.RefreshToken = null; }
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<object>> GetBatchStudentsAsync(Guid batchId)
        => await db.Users.Where(u => u.BatchId == batchId && u.Role == "student")
            .Select(u => (object)new { u.Id, u.Name, u.Email, u.Phone, u.Language, u.IsActive, u.CreatedAt })
            .ToListAsync();

    public async Task<(User teacher, string tempPassword)> CreateTeacherAsync(CreateTeacherRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            throw new InvalidOperationException("Email already in use");
        var tempPassword = GenerateTempPassword();
        var teacher = new User { Name = req.Name, Email = req.Email, Phone = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            Role = "teacher", MustChangePassword = true };
        db.Users.Add(teacher);
        await db.SaveChangesAsync();
        return (teacher, tempPassword);
    }

    public async Task<bool> DeleteTeacherAsync(Guid id)
    {
        var teacher = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "teacher");
        if (teacher == null) return false;
        db.Users.Remove(teacher);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Students ──────────────────────────────────────────────────────────────
    public async Task<object> GetStudentsAsync()
        => await db.Users.Where(u => u.Role == "student").Include(u => u.Batch)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone, u.Language, u.IsActive, u.CreatedAt,
                batchName = u.Batch == null ? null : u.Batch.Name, u.BatchId })
            .ToListAsync();

    public async Task<bool> AssignStudentBatchAsync(Guid studentId, Guid? batchId)
    {
        var student = await db.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == "student");
        if (student == null) return false;
        student.BatchId = batchId;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Subjects, Papers, Chapters, Topics ────────────────────────────────────
    public async Task<object> GetSubjectsAsync()
        => await db.Subjects
            .Select(s => new {
                s.Id, s.Name, s.Level,
                paperCount = db.Papers.Count(p => p.SubjectId == s.Id),
                chapterCount = db.Chapters.Count(c => c.SubjectId == s.Id)
            })
            .ToListAsync();

    public async Task<Subject> CreateSubjectAsync(CreateSubjectRequest req)
    {
        var subject = new Subject { Name = req.Name, Level = req.Level };
        db.Subjects.Add(subject); await db.SaveChangesAsync(); return subject;
    }

    public async Task<bool> UpdateSubjectAsync(Guid id, UpdateSubjectRequest req)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject == null) return false;
        if (req.Name != null) subject.Name = req.Name;
        if (req.Level != null) subject.Level = req.Level;
        await db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteSubjectAsync(Guid id)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject == null) return false;
        db.Subjects.Remove(subject); await db.SaveChangesAsync(); return true;
    }

    // Papers
    public async Task<List<object>> GetPapersAsync(Guid subjectId)
        => await db.Papers.Where(p => p.SubjectId == subjectId).OrderBy(p => p.Order)
            .Select(p => (object)new { p.Id, p.Name, p.Order, p.SubjectId,
                chapterCount = db.Chapters.Count(c => c.PaperId == p.Id) })
            .ToListAsync();

    public async Task<Paper> CreatePaperAsync(CreatePaperRequest req)
    {
        var paper = new Paper { Name = req.Name, Order = req.Order, SubjectId = req.SubjectId };
        db.Papers.Add(paper); await db.SaveChangesAsync(); return paper;
    }

    public async Task<bool> UpdatePaperAsync(Guid id, UpdatePaperRequest req)
    {
        var paper = await db.Papers.FindAsync(id);
        if (paper == null) return false;
        if (req.Name != null) paper.Name = req.Name;
        if (req.Order.HasValue) paper.Order = req.Order.Value;
        await db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeletePaperAsync(Guid id)
    {
        var paper = await db.Papers.FindAsync(id);
        if (paper == null) return false;
        db.Papers.Remove(paper); await db.SaveChangesAsync(); return true;
    }

    // Chapters
    public async Task<List<object>> GetChaptersAsync(Guid subjectId)
        => await db.Chapters.Where(c => c.SubjectId == subjectId).OrderBy(c => c.Number)
            .Select(c => (object)new { c.Id, c.Number, c.Name, c.SubjectId, c.PaperId,
                topicCount = db.Topics.Count(t => t.ChapterId == c.Id) })
            .ToListAsync();

    public async Task<Chapter> CreateChapterAsync(CreateChapterRequest req)
    {
        var chapter = new Chapter { Number = req.Number, Name = req.Name, SubjectId = req.SubjectId, PaperId = req.PaperId };
        db.Chapters.Add(chapter); await db.SaveChangesAsync(); return chapter;
    }

    public async Task<bool> UpdateChapterAsync(Guid id, UpdateChapterRequest req)
    {
        var chapter = await db.Chapters.FindAsync(id);
        if (chapter == null) return false;
        if (req.Number.HasValue) chapter.Number = req.Number.Value;
        if (req.Name != null) chapter.Name = req.Name;
        await db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteChapterAsync(Guid id)
    {
        var chapter = await db.Chapters.FindAsync(id);
        if (chapter == null) return false;
        db.Chapters.Remove(chapter); await db.SaveChangesAsync(); return true;
    }

    // Topics
    public async Task<List<object>> GetTopicsAsync(Guid chapterId)
        => await db.Topics.Where(t => t.ChapterId == chapterId).OrderBy(t => t.Order)
            .Select(t => (object)new { t.Id, t.Name, t.Order, t.ChapterId,
                materialCount = db.Resources.Count(r => r.TopicId == t.Id) })
            .ToListAsync();

    public async Task<Topic> CreateTopicAsync(CreateTopicRequest req)
    {
        var topic = new Topic { Name = req.Name, Order = req.Order, ChapterId = req.ChapterId };
        db.Topics.Add(topic); await db.SaveChangesAsync(); return topic;
    }

    public async Task<bool> UpdateTopicAsync(Guid id, UpdateTopicRequest req)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic == null) return false;
        if (req.Name != null) topic.Name = req.Name;
        if (req.Order.HasValue) topic.Order = req.Order.Value;
        await db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteTopicAsync(Guid id)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic == null) return false;
        db.Topics.Remove(topic); await db.SaveChangesAsync(); return true;
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 10).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}
