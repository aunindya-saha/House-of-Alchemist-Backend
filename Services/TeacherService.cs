using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.DTOs;
using HouseOfAlchemists.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Services;

public class TeacherService(AppDbContext db)
{
    public async Task<object> GetDashboardAsync(Guid teacherId)
    {
        var batches = await db.BatchTeachers.Include(bt => bt.Batch)
            .Where(bt => bt.TeacherId == teacherId)
            .Select(bt => new { bt.Batch.Id, bt.Batch.Name, bt.Batch.Level, studentCount = bt.Batch.Students.Count })
            .ToListAsync();

        var examCount = await db.Exams.CountAsync(e => e.TeacherId == teacherId);
        var resourceCount = await db.Resources.CountAsync(r => r.TeacherId == teacherId);
        var questionCount = await db.Questions.CountAsync(q => q.TeacherId == teacherId);
        var teacherResourceIds = await db.Resources.Where(r => r.TeacherId == teacherId).Select(r => r.Id).ToListAsync();
        var pendingComments = await db.Comments.CountAsync(c => c.Status == "unanswered" && teacherResourceIds.Contains(c.ResourceId));

        return new { batches, examCount, resourceCount, questionCount, pendingComments };
    }

    public async Task<List<object>> GetBatchesAsync(Guid teacherId)
        => await db.BatchTeachers.Include(bt => bt.Batch)
            .Where(bt => bt.TeacherId == teacherId)
            .Select(bt => (object)new { bt.Batch.Id, bt.Batch.Name, bt.Batch.Level, bt.Batch.Year, bt.Batch.Medium })
            .ToListAsync();

    // ── Resources ──────────────────────────────────────────────────────────────
    public async Task<object> GetResourcesAsync(Guid teacherId, Guid? subjectId, Guid? chapterId)
    {
        var query = db.Resources
            .Include(r => r.Chapter).ThenInclude(c => c.Subject)
            .Include(r => r.Topic)
            .Include(r => r.Batch)
            .Where(r => r.TeacherId == teacherId);
        if (subjectId.HasValue) query = query.Where(r => r.Chapter.SubjectId == subjectId);
        if (chapterId.HasValue) query = query.Where(r => r.ChapterId == chapterId);
        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Title, r.Type, r.ContentUrl, r.ContentText, r.CreatedAt,
                r.ChapterId, subjectId = r.Chapter.SubjectId, r.BatchId, r.TopicId,
                subjectName = r.Chapter.Subject.Name, chapterName = r.Chapter.Name,
                topicName = r.Topic == null ? null : r.Topic.Name,
                batchName = r.Batch == null ? "All batches" : r.Batch.Name })
            .ToListAsync();
    }

    public async Task<Resource> CreateResourceAsync(Guid teacherId, CreateResourceRequest req)
    {
        var resource = new Resource { Title = req.Title, Type = req.Type, ContentUrl = req.ContentUrl,
            ContentText = req.ContentText, ChapterId = req.ChapterId, TopicId = req.TopicId,
            BatchId = req.BatchId, TeacherId = teacherId };
        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return resource;
    }

    public async Task<Resource?> UpdateResourceAsync(Guid teacherId, Guid id, UpdateResourceRequest req)
    {
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.Id == id && r.TeacherId == teacherId);
        if (resource == null) return null;
        if (req.Title != null) resource.Title = req.Title;
        if (req.ContentUrl != null) resource.ContentUrl = req.ContentUrl;
        if (req.ContentText != null) resource.ContentText = req.ContentText;
        if (req.TopicId.HasValue) resource.TopicId = req.TopicId;
        await db.SaveChangesAsync();
        return resource;
    }

    public async Task<bool> DeleteResourceAsync(Guid teacherId, Guid id)
    {
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.Id == id && r.TeacherId == teacherId);
        if (resource == null) return false;
        db.Resources.Remove(resource);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<Topic> CreateTopicAsync(Guid teacherId, CreateTopicRequest req)
    {
        var topic = new Topic { Name = req.Name, Order = req.Order, ChapterId = req.ChapterId };
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic;
    }

    // ── Questions ──────────────────────────────────────────────────────────────
    public List<QuestionImportRow> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            // snake_case headers → PascalCase properties: question_text ↔ QuestionText
            PrepareHeaderForMatch = args => args.Header.Replace("_", "").ToLowerInvariant()
        });
        return csv.GetRecords<QuestionImportRow>().ToList();
    }

    public async Task<int> ImportQuestionsAsync(Guid teacherId, ImportQuestionsRequest req)
    {
        var questions = req.Questions.Select(r => new Question
        {
            QuestionText = r.QuestionText, OptionA = r.OptionA, OptionB = r.OptionB,
            OptionC = r.OptionC, OptionD = r.OptionD, CorrectOption = r.CorrectOption,
            Explanation = r.Explanation, Category = r.Category,
            ChapterId = req.ChapterId, Level = req.Level, Language = req.Language,
            TeacherId = teacherId, HasDiagram = !string.IsNullOrEmpty(r.DiagramFilename)
        }).ToList();
        db.Questions.AddRange(questions);
        await db.SaveChangesAsync();
        return questions.Count;
    }

    public async Task<object> GetQuestionsAsync(Guid teacherId, Guid? subjectId, Guid? chapterId, string? q)
    {
        var batchLevels = await db.BatchTeachers.Include(bt => bt.Batch)
            .Where(bt => bt.TeacherId == teacherId).Select(bt => bt.Batch.Level).Distinct().ToListAsync();

        var query = db.Questions.Include(q2 => q2.Chapter).ThenInclude(c => c.Subject)
            .Where(q2 => batchLevels.Contains(q2.Level));
        if (subjectId.HasValue) query = query.Where(q2 => q2.Chapter.SubjectId == subjectId);
        if (chapterId.HasValue) query = query.Where(q2 => q2.ChapterId == chapterId);
        if (!string.IsNullOrEmpty(q)) query = query.Where(q2 => q2.QuestionText.Contains(q));

        return await query.Select(q2 => new { q2.Id, q2.QuestionText, q2.OptionA, q2.OptionB, q2.OptionC, q2.OptionD,
            q2.CorrectOption, q2.Level, q2.Category, q2.Language, q2.HasDiagram, q2.DiagramUrl,
            q2.Tags, q2.Explanation,
            subjectName = q2.Chapter.Subject.Name, chapterName = q2.Chapter.Name }).ToListAsync();
    }

    public async Task<bool> UpdateQuestionAsync(Guid teacherId, Guid questionId, UpdateQuestionRequest req)
    {
        var q = await db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.TeacherId == teacherId);
        if (q == null) return false;
        if (req.QuestionText != null) q.QuestionText = req.QuestionText;
        if (req.OptionA != null) q.OptionA = req.OptionA;
        if (req.OptionB != null) q.OptionB = req.OptionB;
        if (req.OptionC != null) q.OptionC = req.OptionC;
        if (req.OptionD != null) q.OptionD = req.OptionD;
        if (req.CorrectOption != null) q.CorrectOption = req.CorrectOption;
        if (req.Explanation != null) q.Explanation = req.Explanation;
        if (req.Category != null) q.Category = req.Category;
        if (req.Tags != null) q.Tags = req.Tags;
        if (req.DiagramUrl != null) { q.DiagramUrl = req.DiagramUrl; q.HasDiagram = true; }
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteQuestionAsync(Guid teacherId, Guid questionId)
    {
        var q = await db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.TeacherId == teacherId);
        if (q == null) return false;
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Exams ──────────────────────────────────────────────────────────────────
    public async Task<object> GetExamsAsync(Guid teacherId)
        => await db.Exams.Include(e => e.Batch).Where(e => e.TeacherId == teacherId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title, e.Status, e.StartTime, e.DurationMinutes,
                batchName = e.Batch.Name, questionCount = e.ExamQuestions.Count,
                attemptCount = e.Attempts.Count(a => a.Status == "submitted") })
            .ToListAsync();

    public async Task<Exam> CreateExamAsync(Guid teacherId, CreateExamRequest req)
    {
        var exam = new Exam { Title = req.Title, StartTime = req.StartTime,
            DurationMinutes = req.DurationMinutes, BatchId = req.BatchId, TeacherId = teacherId };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        db.ExamQuestions.AddRange(req.QuestionIds.Select((qid, idx) =>
            new ExamQuestion { ExamId = exam.Id, QuestionId = qid, OrderIndex = idx }));
        await db.SaveChangesAsync();
        return exam;
    }

    public async Task<bool> UpdateExamAsync(Guid teacherId, Guid examId, UpdateExamRequest req)
    {
        var exam = await db.Exams.Include(e => e.ExamQuestions)
            .FirstOrDefaultAsync(e => e.Id == examId && e.TeacherId == teacherId && e.Status == "draft");
        if (exam == null) return false;
        if (req.Title != null) exam.Title = req.Title;
        if (req.StartTime.HasValue) exam.StartTime = req.StartTime;
        if (req.DurationMinutes.HasValue) exam.DurationMinutes = req.DurationMinutes.Value;
        if (req.QuestionIds != null)
        {
            db.ExamQuestions.RemoveRange(exam.ExamQuestions);
            db.ExamQuestions.AddRange(req.QuestionIds.Select((qid, idx) =>
                new ExamQuestion { ExamId = exam.Id, QuestionId = qid, OrderIndex = idx }));
        }
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteExamAsync(Guid teacherId, Guid examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.TeacherId == teacherId && e.Status == "draft");
        if (exam == null) return false;
        db.Exams.Remove(exam);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PublishExamAsync(Guid teacherId, Guid examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.TeacherId == teacherId);
        if (exam == null) return false;
        var now = DateTime.UtcNow;
        exam.Status = exam.StartTime.HasValue && exam.StartTime > now ? "scheduled" : "ongoing";
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CloseExamAsync(Guid teacherId, Guid examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.TeacherId == teacherId && (e.Status == "ongoing" || e.Status == "scheduled"));
        if (exam == null) return false;
        exam.Status = "closed";
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReopenExamAsync(Guid teacherId, Guid examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.TeacherId == teacherId && e.Status == "closed");
        if (exam == null) return false;
        exam.Status = "ongoing";
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<object> GetExamResultsAsync(Guid examId)
        => await db.ExamAttempts.Include(a => a.Student)
            .Where(a => a.ExamId == examId && a.Status == "submitted")
            .OrderByDescending(a => a.Score)
            .Select(a => new { studentId = a.StudentId, name = a.Student.Name, score = a.Score, total = a.TotalQuestions,
                percentage = a.TotalQuestions > 0 ? Math.Round((double)a.Score / a.TotalQuestions * 100, 1) : 0.0,
                submittedAt = a.SubmittedAt })
            .ToListAsync();

    public async Task<object> GetBatchStudentsAsync(Guid teacherId, Guid batchId)
    {
        if (!await db.BatchTeachers.AnyAsync(bt => bt.TeacherId == teacherId && bt.BatchId == batchId))
            throw new UnauthorizedAccessException();
        return await db.Users.Where(u => u.BatchId == batchId && u.Role == "student")
            .Select(u => new { u.Id, u.Name, u.Email,
                examsTaken = db.ExamAttempts.Count(a => a.StudentId == u.Id && a.Status == "submitted"),
                avgScore = db.ExamAttempts
                    .Where(a => a.StudentId == u.Id && a.Status == "submitted" && a.TotalQuestions > 0)
                    .Select(a => (double)a.Score / a.TotalQuestions * 100).DefaultIfEmpty(0).Average() })
            .ToListAsync();
    }

    // ── Comments ──────────────────────────────────────────────────────────────
    public async Task<object> GetCommentsAsync(Guid teacherId, string? status)
    {
        var ids = await db.Resources.Where(r => r.TeacherId == teacherId).Select(r => r.Id).ToListAsync();
        var query = db.Comments.Include(c => c.Student).Include(c => c.Resource)
            .Where(c => ids.Contains(c.ResourceId));
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
        return await query.OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.QuestionText, c.ReplyText, c.Status, c.CreatedAt, c.RepliedAt,
                studentName = c.Student.Name, resourceTitle = c.Resource.Title })
            .ToListAsync();
    }

    public async Task<(bool ok, string studentEmail, string studentName)> ReplyToCommentAsync(Guid teacherId, Guid commentId, string replyText)
    {
        var ids = await db.Resources.Where(r => r.TeacherId == teacherId).Select(r => r.Id).ToListAsync();
        var comment = await db.Comments.Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.Id == commentId && ids.Contains(c.ResourceId));
        if (comment == null) return (false, "", "");
        comment.ReplyText = replyText;
        comment.Status = "answered";
        comment.TeacherId = teacherId;
        comment.RepliedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, comment.Student.Email, comment.Student.Name);
    }
}
