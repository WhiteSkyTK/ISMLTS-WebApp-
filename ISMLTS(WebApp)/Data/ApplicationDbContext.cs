using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Student> Students => Set<Student>();
        public DbSet<Lecturer> Lecturers => Set<Lecturer>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Mark> Marks => Set<Mark>();
        public DbSet<Assessment> Assessments => Set<Assessment>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<MarkChange> MarkChanges => Set<MarkChange>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Explicit precision for every decimal (the default is 18,2, which suits marks)
            modelBuilder.Entity<Mark>().Property(m => m.Score).HasPrecision(18, 2);
            modelBuilder.Entity<Mark>().Property(m => m.MaxScore).HasPrecision(18, 2);
            // Existing assessments get 100 when the column is added, rather than 0 (which would reject every score)
            modelBuilder.Entity<Assessment>().Property(a => a.MaxScore).HasPrecision(18, 2).HasDefaultValue(100m);
            modelBuilder.Entity<MarkChange>().Property(c => c.OldScore).HasPrecision(18, 2);
            modelBuilder.Entity<MarkChange>().Property(c => c.NewScore).HasPrecision(18, 2);
            modelBuilder.Entity<MarkChange>().Property(c => c.OldMaxScore).HasPrecision(18, 2);
            modelBuilder.Entity<MarkChange>().Property(c => c.NewMaxScore).HasPrecision(18, 2);

            // Marks already cascade from Module; a second cascade/set-null path through Assessment isn't allowed
            // on SQL Server, so deleting an assessment unlinks its marks in code (MarkService) first.
            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Assessment).WithMany().HasForeignKey(m => m.AssessmentId).OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<MarkChange>().HasIndex(c => c.MarkId);
            modelBuilder.Entity<MarkChange>().HasIndex(c => c.ModuleId);

            // The bell asks "how many unread for this user" on every page
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.Role, n.UserId, n.IsRead });

            modelBuilder.Entity<NotificationSetting>()
                .HasIndex(s => new { s.Role, s.UserId }).IsUnique();

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Module).WithMany().HasForeignKey(a => a.ModuleId).OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.Email)
                .IsUnique();

            modelBuilder.Entity<Lecturer>()
                .HasIndex(l => l.Email)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Username)
                .IsUnique();

            modelBuilder.Entity<Module>()
                .HasIndex(m => m.Code)
                .IsUnique();

            // A Lecturer owns many Modules; don't cascade-delete Modules if a Lecturer is removed
            modelBuilder.Entity<Lecturer>()
                .HasMany(l => l.Modules)
                .WithOne(m => m.Lecturer)
                .HasForeignKey(m => m.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Student <-> Module many-to-many, via an explicit "Enrolments" join table
            modelBuilder.Entity<Student>()
                .HasMany(s => s.Modules)
                .WithMany(m => m.Students)
                .UsingEntity(j => j.ToTable("Enrolments"));

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Code)
                .IsUnique();

            modelBuilder.Entity<Course>()
                .HasMany(c => c.Modules)
                .WithOne(m => m.Course)
                .HasForeignKey(m => m.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Student).WithMany().HasForeignKey(m => m.StudentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Module).WithMany().HasForeignKey(m => m.ModuleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Assessment>()
                .HasOne(a => a.Module).WithMany().HasForeignKey(a => a.ModuleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Assessment).WithMany().HasForeignKey(s => s.AssessmentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.AssessmentId, s.StudentId })
                .IsUnique();

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Student).WithMany().HasForeignKey(t => t.StudentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Module).WithMany().HasForeignKey(t => t.ModuleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttendanceSession>()
                .HasIndex(s => s.Code).IsUnique();

            modelBuilder.Entity<AttendanceSession>()
                .HasOne(s => s.Module).WithMany().HasForeignKey(s => s.ModuleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(r => r.Session).WithMany(s => s.Records).HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AttendanceRecord>()
                .HasIndex(r => new { r.SessionId, r.StudentId }).IsUnique();
        }
    }
}
