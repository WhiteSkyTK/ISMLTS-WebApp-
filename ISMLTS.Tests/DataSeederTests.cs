using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ISMLTS.Tests
{
    public class DataSeederTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();

        private static IConfiguration Config(bool withPasswords = true) =>
            new ConfigurationBuilder().AddInMemoryCollection(withPasswords
                ? new Dictionary<string, string?> { ["Seed:AdminPassword"] = "Seed-Admin-1", ["Seed:LecturerPassword"] = "Seed-Lecturer-1" }
                : new Dictionary<string, string?>()).Build();

        [Fact]
        public async Task EmptyDatabase_GetsOneCourseWithFourModulesPerTerm()
        {
            await using (var context = _db.NewContext())
            {
                await DataSeeder.SeedDataAsync(context, Config());
            }

            await using var check = _db.NewContext();
            var course = await check.Courses.Include(c => c.Modules).SingleAsync();
            Assert.Equal(DataSeeder.CourseCode, course.Code);
            Assert.Equal(8, course.Modules.Count);
            Assert.Equal(4, course.Modules.Count(m => m.Term == "Term1"));
            Assert.Equal(4, course.Modules.Count(m => m.Term == "Term2"));
            Assert.Equal(1, await check.Admins.CountAsync());
            Assert.Equal(1, await check.Lecturers.CountAsync());
        }

        [Fact]
        public async Task RunningOnEveryStartup_AddsNothingTwice()
        {
            for (var run = 0; run < 2; run++)
            {
                await using var context = _db.NewContext();
                await DataSeeder.SeedDataAsync(context, Config());
            }

            await using var check = _db.NewContext();
            Assert.Equal(1, await check.Courses.CountAsync());
            Assert.Equal(8, await check.Modules.CountAsync());
        }

        [Fact]
        public async Task ExistingModuleWithoutACourse_JoinsTheCourseWithItsTerm()
        {
            await using (var setup = _db.NewContext())
            {
                var lecturer = new Lecturer { FullName = "Existing Lecturer", Email = "existing@lecturers.test", PasswordHash = "x" };
                setup.Modules.Add(new Module { Code = "XADAD7112", Name = "WIL (renamed by an admin)", Term = "Term1", Lecturer = lecturer });
                await setup.SaveChangesAsync();
            }

            await using (var context = _db.NewContext())
            {
                await DataSeeder.SeedDataAsync(context, Config());
            }

            await using var check = _db.NewContext();
            var wil = await check.Modules.Include(m => m.Course).SingleAsync(m => m.Code == "XADAD7112");
            Assert.Equal(DataSeeder.CourseCode, wil.Course?.Code);
            Assert.Equal("Term2", wil.Term);
            Assert.Equal("WIL (renamed by an admin)", wil.Name);
            Assert.Equal(8, await check.Modules.CountAsync());
        }

        [Fact]
        public async Task ModuleAlreadyInAnotherCourse_IsLeftWhereItIs()
        {
            await using (var setup = _db.NewContext())
            {
                var other = new Course { Code = "OTHER01", Name = "Another course" };
                var lecturer = new Lecturer { FullName = "Existing Lecturer", Email = "existing@lecturers.test", PasswordHash = "x" };
                setup.Modules.Add(new Module { Code = "SOEN7112", Name = "Software Engineering", Term = "Term1", Lecturer = lecturer, Course = other });
                await setup.SaveChangesAsync();
            }

            await using (var context = _db.NewContext())
            {
                await DataSeeder.SeedDataAsync(context, Config());
            }

            await using var check = _db.NewContext();
            var soen = await check.Modules.Include(m => m.Course).SingleAsync(m => m.Code == "SOEN7112");
            Assert.Equal("OTHER01", soen.Course?.Code);
            Assert.Equal("Term1", soen.Term);
        }

        [Fact]
        public async Task WithoutALecturer_NoModulesAreCreated()
        {
            await using (var context = _db.NewContext())
            {
                await DataSeeder.SeedDataAsync(context, Config(withPasswords: false));
            }

            await using var check = _db.NewContext();
            Assert.Equal(1, await check.Courses.CountAsync());
            Assert.Equal(0, await check.Modules.CountAsync());
            Assert.Equal(0, await check.Admins.CountAsync());
        }

        public void Dispose() => _db.Dispose();
    }
}
