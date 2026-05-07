using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<Trainee> Trainees { get; set; }
        public DbSet<MediaFile> mediaFiles { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Receptionist> Receptionists { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Lecture> Lectures { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Presence> Presences { get; set; }
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<CourseTrainee> CourseTrainees { get; set; }
        public DbSet<CourseTrainer> CourseTrainers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<GusetMessage> GusetMessages { get; set; }
        public DbSet<ContactUs> ContactUs { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }
        public DbSet<QrLoginToken> QrLoginTokens { get; set; }
        public DbSet<Models.AppSetting> AppSettings { get; set; }
        public DbSet<UserNotification> Notifications { get; set; }

        // ── Online Exam System ──────────────────────────────────
        public DbSet<Question> Questions { get; set; }
        public DbSet<ExamQuestion> ExamQuestions { get; set; }
        public DbSet<ExamAttempt> ExamAttempts { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }

        // ── Video Management System ─────────────────────────────────
        public DbSet<LectureVideo> LectureVideos { get; set; }
        public DbSet<VideoView> VideoViews { get; set; }

        // ── Lecture Materials ────────────────────────────────────────
        public DbSet<LectureMaterial> LectureMaterials { get; set; }

        // ── Course Ratings ───────────────────────────────────────────
        public DbSet<CourseRating> CourseRatings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // علاقات المستخدمين
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Trainee)
                .WithOne(t => t.User)
                .HasForeignKey<Trainee>(t => t.UserId);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Trainer)
                .WithOne(t => t.User)
                .HasForeignKey<Trainer>(t => t.UserId);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Admin)
                .WithOne(a => a.User)
                .HasForeignKey<Admin>(a => a.UserId);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Receptionist)
                .WithOne(r => r.User)
                .HasForeignKey<Receptionist>(r => r.UserId);

            // جدول الربط بين Course و Trainee
            builder.Entity<CourseTrainee>()
                .HasKey(ct => new { ct.CourseId, ct.TraineeId });

            builder.Entity<CourseTrainee>()
                .HasOne(ct => ct.Course)
                .WithMany(c => c.CourseTrainees)
                .HasForeignKey(ct => ct.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CourseTrainee>()
                .HasOne(ct => ct.Trainee)
                .WithMany(t => t.CourseTrainees)
                .HasForeignKey(ct => ct.TraineeId)
                .OnDelete(DeleteBehavior.Restrict);

            // تنسيق العمود المالي
            builder.Entity<Payment>()
                .Property(p => p.TotalAmount)
                .HasColumnType("decimal(18,2)");


    // Course - Trainer
            builder.Entity<CourseTrainer>()
    .HasKey(ct => new { ct.CourseId, ct.TrainerId });

            builder.Entity<CourseTrainer>()
                .HasOne(ct => ct.Course)
                .WithMany(c => c.CourseTrainers)
                .HasForeignKey(ct => ct.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CourseTrainer>()
                .HasOne(ct => ct.Trainer)
                .WithMany(t => t.CourseTrainers)
                .HasForeignKey(ct => ct.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);




            builder.Entity<Payment>()
                    .HasOne(p => p.Trainee)
                    .WithMany(t => t.Payments)
                    .HasForeignKey(p => p.TraineeId)
                    .OnDelete(DeleteBehavior.Restrict); // 👈 لا تقم بالحذف المتسلسل

            builder.Entity<Payment>()
                .HasOne(p => p.Course)
                .WithMany(c => c.Payments)
                .HasForeignKey(p => p.CourseId)
                .OnDelete(DeleteBehavior.Restrict); // 👈 أو ممكن ترك واحدة منها Cascade



            builder.Entity<Certificate>()
                   .HasOne(c => c.Course)
                   .WithMany(course => course.Certificates)
                   .HasForeignKey(c => c.CourseId)
                   .OnDelete(DeleteBehavior.Restrict); // أو DeleteBehavior.NoAction

            builder.Entity<Certificate>()
                .HasOne(c => c.Exam)
                .WithMany(exam => exam.Certificates)
                .HasForeignKey(c => c.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Certificate>()
                .HasOne(c => c.Trainee)
                .WithMany(trainee => trainee.Certificates)
                .HasForeignKey(c => c.TraineeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Certificate>()
                .HasOne(c => c.Trainer)
                .WithMany(trainer => trainer.Certificates)
                .HasForeignKey(c => c.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<Presence>()
                   .HasOne(p => p.Lecture)
                   .WithMany(l => l.Presences)
                   .HasForeignKey(p => p.LectureId)
                   .OnDelete(DeleteBehavior.Restrict); // بدل Cascade

            builder.Entity<Presence>()
                .HasOne(p => p.Trainee)
                .WithMany(t => t.Presences)
                .HasForeignKey(p => p.TraineeId)
                .OnDelete(DeleteBehavior.Restrict); // بدل Cascade

            // Configure Message-to-User relationships (Sender and Receiver)
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany() // No inverse collection for Receiver
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete



            // Configure GroupMessage-to-Course relationship
            builder.Entity<GroupMessage>()
      .HasOne(gm => gm.Course)
      .WithMany(c => c.GroupMessages)
      .HasForeignKey(gm => gm.CourseId)
      .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserNotification>()
            .HasKey(n => n.NotificationId);
        builder.Entity<UserNotification>()
            .ToTable("Notifications");
        builder.Entity<UserNotification>()
            .Property(n => n.Title)
            .HasMaxLength(200);
        builder.Entity<UserNotification>()
            .Property(n => n.Message)
            .HasMaxLength(1000);
        builder.Entity<UserNotification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ══════════════════════════════════════════════════════════
        //  ONLINE EXAM SYSTEM — Fluent API
        // ══════════════════════════════════════════════════════════

        // ── Exam ──────────────────────────────────────────────────

        builder.Entity<Exam>()
            .HasOne(e => e.Course)
            .WithMany(c => c.Exams)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Exam>()
            .HasOne(e => e.Trainer)
            .WithMany(t => t.Exams)
            .HasForeignKey(e => e.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Exam>()
            .Property(e => e.PassingScore)
            .HasColumnType("decimal(5,2)");

        // فلتر عالمي: لا تُظهر الامتحانات المحذوفة
        builder.Entity<Exam>()
            .HasQueryFilter(e => !e.IsDeleted);

        // Index للبحث السريع
        builder.Entity<Exam>()
            .HasIndex(e => e.CourseId)
            .HasDatabaseName("IX_Exams_CourseId");

        builder.Entity<Exam>()
            .HasIndex(e => new { e.TrainerId, e.IsPublished })
            .HasDatabaseName("IX_Exams_TrainerId_IsPublished");

        builder.Entity<Exam>()
            .HasIndex(e => e.StartDateTime)
            .HasDatabaseName("IX_Exams_StartDateTime");

        // ── Question (Question Bank) ───────────────────────────────

        builder.Entity<Question>()
            .HasOne(q => q.Trainer)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Question>()
            .Property(q => q.DefaultPoints)
            .HasColumnType("decimal(5,2)");

        builder.Entity<Question>()
            .HasQueryFilter(q => !q.IsDeleted);

        builder.Entity<Question>()
            .HasIndex(q => new { q.TrainerId, q.QuestionType })
            .HasDatabaseName("IX_Questions_TrainerId_Type");

        // OptionsJson — يُحفظ كـ nvarchar
        builder.Entity<Question>()
            .Property(q => q.OptionsJson)
            .HasColumnType("nvarchar(max)");

        // ── ExamQuestion (Junction) ────────────────────────────────

        // Unique constraint: سؤال واحد مرة واحدة فقط في كل امتحان
        builder.Entity<ExamQuestion>()
            .HasIndex(eq => new { eq.ExamId, eq.QuestionId })
            .IsUnique()
            .HasDatabaseName("UX_ExamQuestion_ExamId_QuestionId");

        builder.Entity<ExamQuestion>()
            .HasOne(eq => eq.Exam)
            .WithMany(e => e.ExamQuestions)
            .HasForeignKey(eq => eq.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamQuestion>()
            .HasOne(eq => eq.Question)
            .WithMany(q => q.ExamQuestions)
            .HasForeignKey(eq => eq.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamQuestion>()
            .Property(eq => eq.PointsOverride)
            .HasColumnType("decimal(5,2)");

        // ── ExamAttempt ───────────────────────────────────────────

        builder.Entity<ExamAttempt>()
            .HasOne(a => a.Exam)
            .WithMany(e => e.ExamAttempts)
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamAttempt>()
            .HasOne(a => a.Trainee)
            .WithMany(t => t.ExamAttempts)
            .HasForeignKey(a => a.TraineeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamAttempt>()
            .Property(a => a.TotalScore)
            .HasColumnType("decimal(7,2)");

        builder.Entity<ExamAttempt>()
            .Property(a => a.MaxScore)
            .HasColumnType("decimal(7,2)");

        builder.Entity<ExamAttempt>()
            .Property(a => a.ScorePercentage)
            .HasColumnType("decimal(5,2)");

        // Unique: طالب واحد → محاولة واحدة نشطة per exam (للـ InProgress)
        builder.Entity<ExamAttempt>()
            .HasIndex(a => new { a.ExamId, a.TraineeId, a.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttempt_Exam_Trainee_Number");

        builder.Entity<ExamAttempt>()
            .HasIndex(a => new { a.ExamId, a.Status })
            .HasDatabaseName("IX_ExamAttempt_ExamId_Status");

        builder.Entity<ExamAttempt>()
            .HasIndex(a => a.TraineeId)
            .HasDatabaseName("IX_ExamAttempt_TraineeId");

        // ── StudentAnswer ──────────────────────────────────────────

        builder.Entity<StudentAnswer>()
            .HasOne(sa => sa.Attempt)
            .WithMany(a => a.StudentAnswers)
            .HasForeignKey(sa => sa.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentAnswer>()
            .HasOne(sa => sa.Question)
            .WithMany(q => q.StudentAnswers)
            .HasForeignKey(sa => sa.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StudentAnswer>()
            .Property(sa => sa.PointsEarned)
            .HasColumnType("decimal(5,2)");

        // Unique: إجابة واحدة لكل سؤال في كل محاولة
        builder.Entity<StudentAnswer>()
            .HasIndex(sa => new { sa.AttemptId, sa.QuestionId })
            .IsUnique()
            .HasDatabaseName("UX_StudentAnswer_Attempt_Question");

        builder.Entity<StudentAnswer>()
            .HasIndex(sa => sa.AttemptId)
            .HasDatabaseName("IX_StudentAnswer_AttemptId");

        // ══════════════════════════════════════════════════════════
        //  VIDEO MANAGEMENT SYSTEM — Fluent API
        // ══════════════════════════════════════════════════════════

        builder.Entity<LectureVideo>()
            .HasOne(lv => lv.Lecture)
            .WithMany(l => l.Videos)
            .HasForeignKey(lv => lv.LectureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LectureVideo>()
            .HasOne(lv => lv.UploadedByTrainer)
            .WithMany(t => t.LectureVideos)
            .HasForeignKey(lv => lv.UploadedByTrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LectureVideo>()
            .HasQueryFilter(lv => !lv.IsDeleted);

        builder.Entity<LectureVideo>()
            .HasIndex(lv => lv.LectureId)
            .HasDatabaseName("IX_LectureVideos_LectureId");

        builder.Entity<LectureVideo>()
            .HasIndex(lv => lv.YouTubeVideoId)
            .HasDatabaseName("IX_LectureVideos_YouTubeVideoId");

        builder.Entity<LectureVideo>()
            .HasIndex(lv => lv.UploadedByTrainerId)
            .HasDatabaseName("IX_LectureVideos_UploadedByTrainerId");

        builder.Entity<VideoView>()
            .HasOne(vv => vv.Video)
            .WithMany(v => v.Views)
            .HasForeignKey(vv => vv.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VideoView>()
            .HasOne(vv => vv.Trainee)
            .WithMany(t => t.VideoViews)
            .HasForeignKey(vv => vv.TraineeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VideoView>()
            .HasIndex(vv => new { vv.VideoId, vv.TraineeId })
            .IsUnique()
            .HasDatabaseName("UX_VideoView_VideoId_TraineeId");

        builder.Entity<VideoView>()
            .HasIndex(vv => vv.TraineeId)
            .HasDatabaseName("IX_VideoView_TraineeId");

        // ══════════════════════════════════════════════════════════
        //  LECTURE MATERIALS — Fluent API
        // ══════════════════════════════════════════════════════════

        builder.Entity<LectureMaterial>()
            .HasOne(m => m.Lecture)
            .WithMany(l => l.Materials)
            .HasForeignKey(m => m.LectureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LectureMaterial>()
            .HasOne(m => m.UploadedByTrainer)
            .WithMany(t => t.LectureMaterials)
            .HasForeignKey(m => m.UploadedByTrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LectureMaterial>()
            .HasQueryFilter(m => !m.IsDeleted);

        builder.Entity<LectureMaterial>()
            .HasIndex(m => m.LectureId)
            .HasDatabaseName("IX_LectureMaterials_LectureId");

        // ══════════════════════════════════════════════════════════
        //  COURSE RATINGS — Fluent API
        // ══════════════════════════════════════════════════════════

        builder.Entity<CourseRating>()
            .HasOne(r => r.Course)
            .WithMany(c => c.Ratings)
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CourseRating>()
            .HasOne(r => r.Trainee)
            .WithMany(t => t.CourseRatings)
            .HasForeignKey(r => r.TraineeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CourseRating>()
            .HasIndex(r => new { r.CourseId, r.TraineeId })
            .IsUnique()
            .HasDatabaseName("UX_CourseRating_Course_Trainee");

        builder.Entity<CourseRating>()
            .HasIndex(r => r.CourseId)
            .HasDatabaseName("IX_CourseRating_CourseId");

        }
    }
}


