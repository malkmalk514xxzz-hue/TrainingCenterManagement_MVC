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

        }
    }
}


