using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Data
{
    public static class DataSeeder
    {
        public const string CourseCode = "ADAD0701";
        public const string CourseName = "Advanced Diploma in Application Development";

        // The ADAD0701 curriculum: four modules per term
        public static readonly IReadOnlyList<(string Code, string Name, string Term)> CurriculumModules = new[]
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

        public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration)
        {
            await context.Database.MigrateAsync();
            // Demo data goes first so its accounts get the demo password; it only runs while there are no students
            await DemoSeeder.SeedAsync(context, configuration);
            await DemoSeeder.SeedCalendarAsync(context, configuration);
            await SeedDataAsync(context, configuration);
        }

        // Safe to run on every startup: it only adds what is missing and never overwrites an admin's changes
        public static async Task SeedDataAsync(ApplicationDbContext context, IConfiguration configuration)
        {
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

            var course = await context.Courses.FirstOrDefaultAsync(c => c.Code == CourseCode);
            if (course == null)
            {
                course = new Course { Code = CourseCode, Name = CourseName };
                context.Courses.Add(course);
                await context.SaveChangesAsync();
            }

            await SeedCurriculumAsync(context, course);
        }

        // Adds any curriculum module that is missing, and puts existing ones that aren't in a course yet into this one
        // with their correct term. Modules already in a course, and their names and lecturers, are left alone.
        private static async Task SeedCurriculumAsync(ApplicationDbContext context, Course course)
        {
            var codes = CurriculumModules.Select(m => m.Code).ToList();
            var existing = await context.Modules.Where(m => codes.Contains(m.Code)).ToDictionaryAsync(m => m.Code);
            var lecturer = await context.Lecturers.OrderBy(l => l.LecturerId).FirstOrDefaultAsync();

            foreach (var (code, name, term) in CurriculumModules)
            {
                if (existing.TryGetValue(code, out var module))
                {
                    if (module.CourseId == null)
                    {
                        module.CourseId = course.CourseId;
                        module.Term = term;
                    }
                }
                else if (lecturer != null)
                {
                    context.Modules.Add(new Module { Code = code, Name = name, Term = term, LecturerId = lecturer.LecturerId, CourseId = course.CourseId });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
