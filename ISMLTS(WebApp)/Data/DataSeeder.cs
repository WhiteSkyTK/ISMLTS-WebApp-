using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Data
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            await context.Database.MigrateAsync();

            if (!await context.Admins.AnyAsync())
            {
                context.Admins.Add(new Admin
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "Admin"
                });
                await context.SaveChangesAsync();
            }

            if (!await context.Lecturers.AnyAsync())
            {
                context.Lecturers.Add(new Lecturer
                {
                    FullName = "Edward Nkata",
                    Email = "edward.nkata@rosebank.iie.ac.za",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lecturer@123")
                });
                await context.SaveChangesAsync();
            }

            if (!await context.Courses.AnyAsync())
            {
                context.Courses.Add(new Course { Code = "ADAD0701", Name = "Advanced Diploma in Application Development" });
                await context.SaveChangesAsync();
            }

            if (!await context.Modules.AnyAsync())
            {
                var lecturer = await context.Lecturers.FirstAsync();
                var course = await context.Courses.FirstAsync();
                var modules = new[]
                {
                    ("APDS7311", "Application Development Security", "Term1"),
                    ("EAPD7111", "Enterprise Application Development", "Term1"),
                    ("CLDV7111", "Cloud Development A", "Term1"),
                    ("IRIT7311", "Introduction to Research for ICT", "Term1"),
                    ("AAPD7112", "Advanced Application Development", "Term2"),
                    ("CLDV7112", "Cloud Development B", "Term2"),
                    ("SOEN7112", "Software Engineering", "Term2"),
                    ("XADAD7112", "Work Integrated Learning 3", "Term2"),
                };
                foreach (var (code, name, term) in modules)
                {
                    context.Modules.Add(new Module { Code = code, Name = name, Term = term, LecturerId = lecturer.LecturerId, CourseId = course.CourseId });
                }
                await context.SaveChangesAsync();
            }

            // Add / edit / remove seed blocks above as your data structure evolves.
        }
    }
}