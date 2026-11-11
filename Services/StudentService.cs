using System.Text.Json;
using HouseOfAlchemists.API.Data;
using HouseOfAlchemists.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseOfAlchemists.API.Services;

public class StudentService(AppDbContext db)
{
    public async Task<object> GetDashboardAsync(Guid userId)
    {
        var user = await db.Users.Include(u => u.Batch).FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new Exception("User not found");

        var attempts = await db.ExamAttempts
            .Where(a => a.StudentId == userId && a.Status == "submitted").ToListAsync();

        var totalExams = attempts.Count;
        var avgScore = totalExams > 0
            ? attempts.Average(a => a.TotalQuestions > 0 ? (double)a.Score / a.TotalQuestions * 100 : 0)
            : 0.0;

        var upcomingExams = user.BatchId.HasValue
            ? await db.Exams
                .Where(e => e.BatchId == user.BatchId && e.Status == "scheduled" && e.StartTime > DateTime.UtcNow)
                .OrderBy(e => e.StartTime).Take(3)
                .Select(e => new { e.Id, e.Title, e.StartTime, e.DurationMinutes })
                .ToListAsync()
            : new List<object>() as dynamic;

        var recentResources = user.BatchId.HasValue
            ? await db.Resources
                .Include(r => r.Chapter).ThenInclude(c => c.Subject)
                .Where(r => r.BatchId == null || r.BatchId == user.BatchId)
                .OrderByDescending(r => r.CreatedAt).Take(5)
                .Select(r => new { r.Id, r.Title, r.Type, subject = r.Chapter.Subject.Name, chapter = r.Chapter.Name })
                .ToListAsync()
            : new List<object>() as dynamic;

        return new { totalExams, avgScore = Math.Round(avgScore, 1), upcomingExams, recentResources };
    }

    public async Task<object> GetResourcesAsync(Guid userId, Guid? subjectId, Guid? chapterId, Guid? topicId, string? type, string? q)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new Exception("User not found");

        var query = db.Resources
            .Include(r => r.Chapter).ThenInclude(c => c.Subject)
            .Include(r => r.Topic)
            .Where(r => r.BatchId == null || r.BatchId == user.BatchId);

        if (subjectId.HasValue) query = query.Where(r => r.Chapter.SubjectId == subjectId);
        if (chapterId.HasValue) query = query.Where(r => r.ChapterId == chapterId);
        if (topicId.HasValue) query = query.Where(r => r.TopicId == topicId);
        if (!string.IsNullOrEmpty(type)) query = query.Where(r => r.Type == type);
        if (!string.IsNullOrEmpty(q)) query = query.Where(r => r.Title.Contains(q));

        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id, r.Title, r.Type, r.ContentUrl, r.ContentText, r.CreatedAt,
                subjectId = r.Chapter.SubjectId, chapterId = r.ChapterId, r.TopicId,
                subjectName = r.Chapter.Subject.Name, chapterName = r.Chapter.Name,
                topicName = r.Topic == null ? null : r.Topic.Name
            })
            .ToListAsync();
    }

    public async Task<object> GetExamsAsync(Guid userId, string? status)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new Exception("User not found");
        if (!user.BatchId.HasValue) return new List<object>();

        var now = DateTime.UtcNow;
        var exams = await db.Exams
            .Where(e => e.BatchId == user.BatchId && e.Status != "draft")
            .Select(e => new
            {
                e.Id, e.Title, e.StartTime, e.DurationMinutes,
                computedStatus = e.StartTime == null
                    ? e.Status  // immediate-publish exams: trust stored status
                    : now < e.StartTime ? "scheduled"
                    : now < e.StartTime.Value.AddMinutes(e.DurationMinutes) ? "ongoing"
                    : "closed",
                questionCount = e.ExamQuestions.Count,
                attempted = db.ExamAttempts.Any(a => a.ExamId == e.Id && a.StudentId == userId && a.Status == "submitted")
            })
            .ToListAsync();

        if (!string.IsNullOrEmpty(status))
            exams = exams.Where(e => e.computedStatus == status).ToList();

        return exams;
    }

    public async Task<object> StartExamAsync(Guid userId, Guid examId)
    {
        var existing = await db.ExamAttempts.FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == userId);
        if (existing?.Status == "submitted") throw new InvalidOperationException("You have already submitted this exam.");

        var exam = await db.Exams
            .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.Id == examId) ?? throw new Exception("Exam not found");

        if (existing == null)
        {
            existing = new ExamAttempt { ExamId = examId, StudentId = userId, TotalQuestions = exam.ExamQuestions.Count };
            db.ExamAttempts.Add(existing);
            try { await db.SaveChangesAsync(); }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // Concurrent request already created the attempt — re-fetch it
                db.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                existing = await db.ExamAttempts.FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == userId)
                           ?? throw new Exception("Exam not found.");
            }
        }

        var questions = exam.ExamQuestions.OrderBy(eq => eq.OrderIndex)
            .Select(eq => new
            {
                eq.Question.Id, eq.Question.QuestionText, eq.Question.HasDiagram, eq.Question.DiagramUrl,
                eq.Question.OptionA, eq.Question.OptionB, eq.Question.OptionC, eq.Question.OptionD
            }).ToList();

        return new { attemptId = existing.Id, examTitle = exam.Title,
                     durationMinutes = exam.DurationMinutes, startedAt = existing.StartedAt, questions };
    }

    public async Task<object> SubmitExamAsync(Guid userId, Guid examId, Dictionary<string, string> answers)
    {
        var attempt = await db.ExamAttempts.FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == userId)
                      ?? throw new Exception("Start the exam first.");
        if (attempt.Status == "submitted") throw new InvalidOperationException("Already submitted.");

        var questions = await db.ExamQuestions.Include(eq => eq.Question)
            .Where(eq => eq.ExamId == examId).ToListAsync();

        var score = questions.Count(eq =>
            answers.TryGetValue(eq.QuestionId.ToString(), out var given) &&
            given.ToLower() == eq.Question.CorrectOption.ToLower());

        attempt.Score = score;
        attempt.TotalQuestions = questions.Count;
        attempt.AnswersJson = JsonSerializer.Serialize(answers);
        attempt.Status = "submitted";
        attempt.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new { score, total = questions.Count, percentage = Math.Round((double)score / questions.Count * 100, 1) };
    }

    public async Task<object> GetResultAsync(Guid userId, Guid examId)
    {
        var attempt = await db.ExamAttempts.Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == userId && a.Status == "submitted")
            ?? throw new Exception("No submitted attempt found.");

        var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(attempt.AnswersJson) ?? new();
        var questions = await db.ExamQuestions.Include(eq => eq.Question)
            .Where(eq => eq.ExamId == examId).OrderBy(eq => eq.OrderIndex).ToListAsync();

        var correct = 0; var wrong = 0; var skipped = 0;
        var review = questions.Select(eq =>
        {
            answers.TryGetValue(eq.QuestionId.ToString(), out var given);
            var isCorrect = given != null && given.ToLower() == eq.Question.CorrectOption.ToLower();
            if (string.IsNullOrEmpty(given)) skipped++;
            else if (isCorrect) correct++;
            else wrong++;
            return new { eq.Question.Id, eq.Question.QuestionText,
                eq.Question.OptionA, eq.Question.OptionB, eq.Question.OptionC, eq.Question.OptionD,
                correctOption = eq.Question.CorrectOption, yourAnswer = given, eq.Question.Explanation };
        }).ToList();

        var allAttempts = await db.ExamAttempts
            .Where(a => a.ExamId == examId && a.Status == "submitted")
            .OrderByDescending(a => a.Score).ThenBy(a => a.SubmittedAt).ToListAsync();
        var rank = allAttempts.FindIndex(a => a.StudentId == userId) + 1;

        return new { examTitle = attempt.Exam.Title, score = attempt.Score, total = attempt.TotalQuestions,
            percentage = attempt.TotalQuestions > 0 ? Math.Round((double)attempt.Score / attempt.TotalQuestions * 100, 1) : 0.0,
            rank, totalParticipants = allAttempts.Count, correct, wrong, skipped, review };
    }

    public async Task<object> GetLeaderboardAsync(Guid? examId, Guid userId)
    {
        var query = db.ExamAttempts.Include(a => a.Student)
            .Where(a => a.Status == "submitted");
        if (examId.HasValue) query = query.Where(a => a.ExamId == examId);

        return await query.OrderByDescending(a => a.Score).ThenBy(a => a.SubmittedAt).Take(50)
            .Select(a => new
            {
                studentId = a.StudentId, name = a.Student.Name,
                score = a.Score, total = a.TotalQuestions,
                percentage = a.TotalQuestions > 0 ? Math.Round((double)a.Score / a.TotalQuestions * 100, 1) : 0.0,
                isMe = a.StudentId == userId, submittedAt = a.SubmittedAt
            }).ToListAsync();
    }
}
