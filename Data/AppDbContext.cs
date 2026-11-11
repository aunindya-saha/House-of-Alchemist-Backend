using Microsoft.EntityFrameworkCore;
using HouseOfAlchemists.API.Models;

namespace HouseOfAlchemists.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder opt)
    {
        if (!opt.IsConfigured) return;
        opt.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchTeacher> BatchTeachers => Set<BatchTeacher>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Paper> Papers => Set<Paper>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Unique email across all users
        mb.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Many-to-many: Batch ↔ Teacher
        mb.Entity<BatchTeacher>().HasKey(bt => new { bt.BatchId, bt.TeacherId });
        mb.Entity<BatchTeacher>()
            .HasOne(bt => bt.Batch).WithMany(b => b.BatchTeachers).HasForeignKey(bt => bt.BatchId);
        mb.Entity<BatchTeacher>()
            .HasOne(bt => bt.Teacher).WithMany(u => u.BatchTeachers).HasForeignKey(bt => bt.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-many: Exam ↔ Question
        mb.Entity<ExamQuestion>().HasKey(eq => new { eq.ExamId, eq.QuestionId });

        // One student → one attempt per exam
        mb.Entity<ExamAttempt>()
            .HasIndex(a => new { a.ExamId, a.StudentId }).IsUnique();

        // Prevent multiple cascade path errors in SQL Server
        mb.Entity<Exam>()
            .HasOne(e => e.Teacher).WithMany().HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Resource>()
            .HasOne(r => r.Teacher).WithMany(u => u.Resources).HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Resource>()
            .HasOne(r => r.Topic).WithMany(t => t.Materials).HasForeignKey(r => r.TopicId)
            .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Chapter>()
            .HasOne(c => c.Paper).WithMany(p => p.Chapters).HasForeignKey(c => c.PaperId)
            .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Question>()
            .HasOne(q => q.Teacher).WithMany().HasForeignKey(q => q.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Comment>()
            .HasOne(c => c.Teacher).WithMany().HasForeignKey(c => c.TeacherId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        // Seed the super admin account (password: Admin@123)
        // Hash is static so EF Core model snapshot stays stable across builds
        mb.Entity<User>().HasData(new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Super Admin",
            Email = "admin@hoa.edu",
            PasswordHash = "$2a$11$JLy.RYbsyanjNYPbQbVTcezbe/UQZJV39gmvUNSvt6uz0cna9kGGe",
            Role = "superadmin",
            Language = "english",
            Theme = "system"
        });
    }
}
