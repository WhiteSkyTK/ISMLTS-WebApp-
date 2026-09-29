using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Data
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration)
        {
            await context.Database.MigrateAsync();

            // Seed passwords come from configuration (App Service settings on Azure), never from the repo
            var adminPassword = configuration["Seed:AdminPassword"];
            var lecturerPassword = configuration["Seed:LecturerPassword"];

            if (!string.IsNullOrWhiteSpace(adminPassword) && !await context.Admins.AnyAsync())
            {
                context.Admins.Add(new Admin
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    Role = "Admin"
                });
                await context.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(lecturerPassword) && !await context.Lecturers.AnyAsync())
            {
                context.Lecturers.Add(new Lecturer
                {
                    FullName = "Edward Nkata",
                    Email = "edward.nkata@rosebank.iie.ac.za",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(lecturerPassword)
                });
                await context.SaveChangesAsync();
            }

            if (!await context.Courses.AnyAsync())
            {
                context.Courses.Add(new Course { Code = "ADAD0701", Name = "Advanced Diploma in Application Development" });
                await context.SaveChangesAsync();
            }

            var lecturer = await context.Lecturers.FirstOrDefaultAsync();
            if (lecturer == null || await context.Modules.AnyAsync())
            {
                return;
            }

            var course = await context.Courses.FirstAsync();
            (string Code, string Name, string Term)[] modules =
            {
                ("APDS7311", "Application Development Security", "Term1"),
                ("EAPD7111", "Enterprise Application Development", "Term1"),
                ("CLDV7111", "Cloud Development A", "Term1"),
                ("IRIT7311", "Introduction to Research for ICT", "Term1"),
                ("AAPD7112", "Advanced Application Development", "Term2"),
                ("CLDV7112", "Cloud Development B", "Term2"),
                ("SOEN7112", "Software Engineering", "Term2"),
                ("XADAD7112", "Work Integrated Learning 3", "Term2")
            };
            foreach (var (code, name, term) in modules)
            {
                context.Modules.Add(new Module { Code = code, Name = name, Term = term, LecturerId = lecturer.LecturerId, CourseId = course.CourseId });
            }
            await context.SaveChangesAsync();
        }
    }
}