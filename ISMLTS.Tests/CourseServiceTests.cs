using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ISMLTS.Tests
{
    public class CourseServiceTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();

        [Theory]
        [InlineData("Term1", "All", true)]
        [InlineData("Term2", "All", true)]
        [InlineData("Term1", "Term1", true)]
        [InlineData("Term2", "Term1", false)]
        public void CourseTerms_MatchesTheChosenTerm(string moduleTerm, string choice, bool expected) =>
            Assert.Equal(expected, CourseTerms.Matches(moduleTerm, choice));

        [Theory]
        [InlineData("All", true)]
        [InlineData("Term2", true)]
        [InlineData("Term3", false)]
        [InlineData(null, false)]
        public void CourseTerms_OnlyAcceptsKnownChoices(string? choice, bool expected) =>
            Assert.Equal(expected, CourseTerms.IsValid(choice));

        [Fact]
        public async Task Enrol_Term1_AddsStudentsToTerm1ModulesOnly()
        {
            var (courseId, studentIds) = await SeedAsync();

            await using (var context = _db.NewContext())
            {
                var result = await Service(context).EnrolAsync(await context.Courses.FindAsync(courseId) ?? throw new InvalidOperationException(), studentIds, CourseTerms.Term1);
                Assert.Equal(new CourseEnrolmentResult(2, 4, 8), result);
            }

            await using var check = _db.NewContext();
            var modules = await check.Modules.Include(m => m.Students).ToListAsync();
            Assert.All(modules.Where(m => m.Term == "Term1"), m => Assert.Equal(2, m.Students.Count));
            Assert.All(modules.Where(m => m.Term == "Term2"), m => Assert.Empty(m.Students));
            Assert.All(await check.Students.ToListAsync(), s => Assert.Equal(DataSeeder.CourseName, s.Programme));
        }

        [Fact]
        public async Task Enrol_Twice_DoesNotDuplicateEnrolments()
        {
            var (courseId, studentIds) = await SeedAsync();

            CourseEnrolmentResult second;
            await using (var context = _db.NewContext())
            {
                var course = await context.Courses.FindAsync(courseId) ?? throw new InvalidOperationException();
                await Service(context).EnrolAsync(course, studentIds, CourseTerms.All);
            }
            await using (var context = _db.NewContext())
            {
                var course = await context.Courses.FindAsync(courseId) ?? throw new InvalidOperationException();
                second = await Service(context).EnrolAsync(course, studentIds, CourseTerms.All);
            }

            Assert.Equal(0, second.Changes);
            await using var check = _db.NewContext();
            Assert.All(await check.Modules.Include(m => m.Students).ToListAsync(), m => Assert.Equal(2, m.Students.Count));
        }

        [Fact]
        public async Task Unenrol_Term2_RemovesOnlyTerm2Enrolments()
        {
            var (courseId, studentIds) = await SeedAsync();
            await using (var context = _db.NewContext())
            {
                var course = await context.Courses.FindAsync(courseId) ?? throw new InvalidOperationException();
                await Service(context).EnrolAsync(course, studentIds, CourseTerms.All);
            }

            await using (var context = _db.NewContext())
            {
                var course = await context.Courses.FindAsync(courseId) ?? throw new InvalidOperationException();
                var result = await Service(context).UnenrolAsync(course, new[] { studentIds[0] }, CourseTerms.Term2);
                Assert.Equal(4, result.Changes);
            }

            await using var check = _db.NewContext();
            var student = await check.Students.Include(s => s.Modules).SingleAsync(s => s.StudentId == studentIds[0]);
            Assert.Equal(4, student.Modules.Count);
            Assert.All(student.Modules, m => Assert.Equal("Term1", m.Term));
        }

        [Fact]
        public async Task AssignModules_MovesTickedModulesInAndUntickedOnesOut()
        {
            var (courseId, _) = await SeedAsync();
            int keep, drop, extra;
            await using (var setup = _db.NewContext())
            {
                var modules = await setup.Modules.OrderBy(m => m.Code).ToListAsync();
                keep = modules[0].ModuleId;
                drop = modules[1].ModuleId;
                var lecturer = await setup.Lecturers.FirstAsync();
                var loose = new Module { Code = "EXTR7111", Name = "Elective", Term = "Term1", LecturerId = lecturer.LecturerId };
                setup.Modules.Add(loose);
                await setup.SaveChangesAsync();
                extra = loose.ModuleId;
            }

            await using (var context = _db.NewContext())
            {
                var count = await Service(context).AssignModulesAsync(courseId, new[] { keep, extra });
                Assert.Equal(2, count);
            }

            await using var check = _db.NewContext();
            Assert.Equal(courseId, (await check.Modules.FindAsync(extra))?.CourseId);
            Assert.Null((await check.Modules.FindAsync(drop))?.CourseId);
        }

        private static CourseService Service(ApplicationDbContext context) =>
            new(new ModuleRepository(context), new StudentRepository(context));

        // The seeded curriculum (8 modules) plus two students
        private async Task<(int CourseId, int[] StudentIds)> SeedAsync()
        {
            await using var context = _db.NewContext();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:LecturerPassword"] = "Seed-Lecturer-1" })
                .Build();
            await DataSeeder.SeedDataAsync(context, config);

            var students = new[]
            {
                new Student { FullName = "Student One", Email = "one@students.test" },
                new Student { FullName = "Student Two", Email = "two@students.test" }
            };
            context.Students.AddRange(students);
            await context.SaveChangesAsync();
            return ((await context.Courses.SingleAsync()).CourseId, students.Select(s => s.StudentId).ToArray());
        }

        public void Dispose() => _db.Dispose();
    }
}
