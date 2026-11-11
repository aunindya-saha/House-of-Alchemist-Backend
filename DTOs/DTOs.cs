namespace HouseOfAlchemists.API.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────
public record RegisterRequest(string Name, string Email, string? Phone, string Password, string? Language);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record GoogleAuthRequest(string Credential);

// ── Profile ───────────────────────────────────────────────────────────────────
public record UpdateProfileRequest(string? Name, string? Phone, string? Language, string? Theme);

// ── Resources ─────────────────────────────────────────────────────────────────
public record CreateResourceRequest(
    string Title,
    string Type,           // youtube | pdf | text
    string? ContentUrl,
    string? ContentText,
    Guid ChapterId,
    Guid? TopicId,
    Guid? BatchId
);
public record UpdateResourceRequest(string? Title, string? ContentUrl, string? ContentText, Guid? TopicId);

// ── Questions ─────────────────────────────────────────────────────────────────
public record QuestionImportRow
{
    public string QuestionText { get; init; } = "";
    public string OptionA { get; init; } = "";
    public string OptionB { get; init; } = "";
    public string OptionC { get; init; } = "";
    public string OptionD { get; init; } = "";
    public string CorrectOption { get; init; } = "a";
    public string? Explanation { get; init; }
    public string? Category { get; init; }
    public string? DiagramFilename { get; init; }
}

public record ImportQuestionsRequest(
    Guid ChapterId,
    string Level,
    string Language,
    List<QuestionImportRow> Questions
);

// ── Exams ─────────────────────────────────────────────────────────────────────
public record CreateExamRequest(
    string Title,
    DateTime? StartTime,
    int DurationMinutes,
    Guid BatchId,
    List<Guid> QuestionIds
);
public record UpdateExamRequest(string? Title, DateTime? StartTime, int? DurationMinutes, List<Guid>? QuestionIds);
public record UpdateQuestionRequest(string? QuestionText, string? OptionA, string? OptionB, string? OptionC, string? OptionD, string? CorrectOption, string? Explanation, string? Category, string? Tags, string? DiagramUrl);
public record SubmitExamRequest(Dictionary<string, string> Answers);

// ── Admin ─────────────────────────────────────────────────────────────────────
public record CreateBatchRequest(string Name, string Level, string? Year, string Medium, List<Guid>? TeacherIds);
public record UpdateBatchRequest(string? Name, string? Level, string? Year, string? Medium, List<Guid>? TeacherIds);
public record CreateTeacherRequest(string Name, string Email, string? Phone);
public record AssignStudentBatchRequest(Guid? BatchId);
public record CreateSubjectRequest(string Name, string Level);
public record UpdateSubjectRequest(string? Name, string? Level);
public record CreatePaperRequest(Guid SubjectId, string Name, int Order);
public record UpdatePaperRequest(string? Name, int? Order);
public record CreateChapterRequest(Guid SubjectId, Guid? PaperId, int Number, string Name);
public record UpdateChapterRequest(int? Number, string? Name);
public record CreateTopicRequest(Guid ChapterId, string Name, int Order);
public record UpdateTopicRequest(string? Name, int? Order);

// ── Comments ──────────────────────────────────────────────────────────────────
public record CreateCommentRequest(Guid ResourceId, string QuestionText);
public record ReplyCommentRequest(string ReplyText);
public record EditCommentRequest(string QuestionText);
public record EditReplyRequest(string ReplyText);
