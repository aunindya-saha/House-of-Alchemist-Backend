namespace HouseOfAlchemists.API.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "student"; // student | teacher | superadmin
    public string Language { get; set; } = "bangla"; // bangla | english
    public string Theme { get; set; } = "system"; // light | dark | system
    public bool MustChangePassword { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? GoogleId { get; set; }   // set for Google-authed users
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? BatchId { get; set; }
    public Batch? Batch { get; set; }
    public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
    public ICollection<BatchTeacher> BatchTeachers { get; set; } = new List<BatchTeacher>();
    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
}

public class Batch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Level { get; set; } = "ssc"; // ssc | hsc | admission
    public string? Year { get; set; }
    public string Medium { get; set; } = "bangla"; // bangla | english
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Students { get; set; } = new List<User>();
    public ICollection<BatchTeacher> BatchTeachers { get; set; } = new List<BatchTeacher>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
}

public class BatchTeacher
{
    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
}

public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Level { get; set; } = "ssc"; // ssc | hsc | admission
    public ICollection<Paper> Papers { get; set; } = new List<Paper>();
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}

// Paper — optional; used by HSC and Admission, not SSC
public class Paper
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = ""; // "Paper 1", "Paper 2"
    public int Order { get; set; } = 1;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    // null = SSC (no paper); set = HSC/Admission
    public Guid? PaperId { get; set; }
    public Paper? Paper { get; set; }
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
}

// Topic — sits between Chapter and Material
public class Topic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Order { get; set; } = 1;
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public ICollection<Resource> Materials { get; set; } = new List<Resource>();
}

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = "";
    public string OptionA { get; set; } = "";
    public string OptionB { get; set; } = "";
    public string OptionC { get; set; } = "";
    public string OptionD { get; set; } = "";
    public string CorrectOption { get; set; } = "a"; // a | b | c | d
    public string? Explanation { get; set; }
    public bool HasDiagram { get; set; } = false;
    public string? DiagramUrl { get; set; }
    public string Level { get; set; } = "ssc";
    public string? Category { get; set; } // board | admission | null
    public string? Tags { get; set; } // comma-separated tags
    public string Language { get; set; } = "bangla";
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Type { get; set; } = "youtube"; // youtube | pdf | text
    public string? ContentUrl { get; set; }
    public string? ContentText { get; set; }
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    // TopicId links to a specific topic within the chapter
    public Guid? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public Guid? BatchId { get; set; } // null = all batches of this level
    public Batch? Batch { get; set; }
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Status { get; set; } = "draft"; // draft | scheduled | ongoing | closed
    public DateTime? StartTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public Guid BatchId { get; set; }
    public Batch Batch { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
}

public class ExamQuestion
{
    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int OrderIndex { get; set; }
}

public class ExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public string Status { get; set; } = "started"; // started | submitted
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public int Score { get; set; } = 0;
    public int TotalQuestions { get; set; } = 0;
    public string AnswersJson { get; set; } = "{}"; // { "questionId": "a", ... }
}

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = "";
    public string? ReplyText { get; set; }
    public string Status { get; set; } = "unanswered"; // unanswered | answered
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public Guid? TeacherId { get; set; }
    public User? Teacher { get; set; }
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RepliedAt { get; set; }
}
