using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class ReportsTests
    {
        [Fact]
        public void Users_CountsEveryoneAndWhoNeedsAttention()
        {
            var module = new Module { ModuleId = 1, LecturerId = 10 };
            var students = new[]
            {
                new Student { StudentId = 1, Modules = { module }, TwoFactorEnabled = true },
                new Student { StudentId = 2 }
            };
            var lecturers = new[] { new Lecturer { LecturerId = 10 }, new Lecturer { LecturerId = 11, TwoFactorEnabled = true } };
            var admins = new[] { new Admin { AdminId = 1, TwoFactorEnabled = true } };

            var summary = Reports.Users(students, lecturers, admins, new[] { module });

            Assert.Equal(new UserSummary(Students: 2, StudentsNotEnrolled: 1, Lecturers: 2, LecturersWithoutModules: 1, Admins: 1, TwoFactorOn: 3, TotalUsers: 5), summary);
        }

        [Fact]
        public void ModulesCsv_HasOneLinePerModule()
        {
            var insight = new ModuleInsight(1, "PROG6211", "Programming, part A", 12, 64.25m, 81, null, new List<StudentInsight>
            {
                new(5, "Sipho", 40m, 60, 1, 3, new[] { "Average below 50%" })
            });

            var csv = Reports.ModulesCsv(new[] { new ModuleReport(insight, "DSWD0601", "Term 1", "Nomsa Dlamini", 6) });

            var lines = csv.TrimEnd().Split("\r\n");
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("Module,Name,Course,Term,Lecturer,Enrolled", lines[0]);
            Assert.Equal("PROG6211,\"Programming, part A\",DSWD0601,Term 1,Nomsa Dlamini,12,64.3,81,,1,6", lines[1]);
        }
    }
}
