using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests change programmes and terms
    public class AdminListTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AdminListTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Admin() => _factory.ClientFor("Admin", _factory.Data.AdminId);

        private static void AssertBefore(string html, string first, string second) =>
            Assert.True(html.IndexOf(first, StringComparison.Ordinal) < html.IndexOf(second, StringComparison.Ordinal),
                $"Expected {first} before {second}.");

        [Fact]
        public async Task Students_SortByName_BothWays()
        {
            var ascending = await Admin().GetStringAsync("/Students");
            AssertBefore(ascending, "Student A", "Student B");
            Assert.Contains("aria-sort=\"ascending\"", ascending);

            var descending = await Admin().GetStringAsync("/Students?sort=-name");
            AssertBefore(descending, "Student B", "Student A");
            Assert.Contains("aria-sort=\"descending\"", descending);
        }

        [Fact]
        public async Task Students_UnknownSort_FallsBackToName()
        {
            var response = await Admin().GetAsync("/Students?sort=PasswordHash");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertBefore(await response.Content.ReadAsStringAsync(), "Student A", "Student B");
        }

        [Fact]
        public async Task Students_FilterByProgramme_OrByHavingNone()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                await db.Students.Where(s => s.StudentId == data.OtherStudentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Programme, "Diploma in Testing"));
                return await db.Students.Where(s => s.StudentId == data.StudentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Programme, (string?)null));
            });

            var diploma = await Admin().GetStringAsync("/Students?programme=Diploma%20in%20Testing");
            Assert.Contains("Student B", diploma);
            Assert.DoesNotContain("Student A", diploma);
            Assert.Contains("<option value=\"Diploma in Testing\" selected=\"selected\">", diploma);

            var none = await Admin().GetStringAsync("/Students?programme=none");
            Assert.Contains("Student A", none);
            Assert.DoesNotContain("Student B", none);
        }

        [Fact]
        public async Task SortLinks_KeepTheSearchAndFilters()
        {
            var html = await Admin().GetStringAsync("/Students?q=Student&programme=none");

            Assert.Contains("href=\"/Students?q=Student&amp;programme=none&amp;sort=-name\"", html);
            Assert.Contains("href=\"/Students?q=Student&amp;programme=none&amp;sort=email\"", html);
        }

        [Fact]
        public async Task Modules_FilterByTermAndLecturer_AndSortByCode()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(db => db.Modules.Where(m => m.ModuleId == data.ModuleBId).ExecuteUpdateAsync(m => m.SetProperty(x => x.Term, "Term2")));

            var term2 = await Admin().GetStringAsync("/Modules?term=Term2");
            Assert.Contains(data.ModuleBCode, term2);
            Assert.DoesNotContain(data.ModuleACode, term2);

            var lecturerA = await Admin().GetStringAsync($"/Modules?lecturer={data.LecturerAId.ToString(CultureInfo.InvariantCulture)}");
            Assert.Contains(data.ModuleACode, lecturerA);
            Assert.DoesNotContain(data.ModuleBCode, lecturerA);

            var byCodeDescending = await Admin().GetStringAsync("/Modules?sort=-code");
            AssertBefore(byCodeDescending, data.ModuleBCode, data.ModuleACode);
        }

        [Fact]
        public async Task Modules_NotInACourse_FilterWorks_AndBadCourseValuesAreIgnored()
        {
            var data = _factory.Data;

            var none = await Admin().GetStringAsync("/Modules?course=none");
            Assert.Contains(data.ModuleACode, none);

            var junk = await Admin().GetAsync("/Modules?course=abc&term=Term9");
            Assert.Equal(HttpStatusCode.OK, junk.StatusCode);
            var html = await junk.Content.ReadAsStringAsync();
            Assert.Contains(data.ModuleACode, html);
            Assert.Contains(data.ModuleBCode, html);
        }
    }
}
