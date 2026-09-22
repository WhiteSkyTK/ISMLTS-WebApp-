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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
        }
    }
}
