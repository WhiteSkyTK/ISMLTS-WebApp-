using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add and delete rows
    public class AdminAreaTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AdminAreaTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Admin => _factory.ClientFor("Admin", _factory.Data.AdminId);

        [Fact]
        public async Task StudentList_PagesPast25RowsAndSearches()
        {
            await _factory.WithDbAsync(async db =>
            {
                for (var i = 1; i <= 30; i++)
                {
                    db.Students.Add(new Student { FullName = $"Paging Student {i:00}", Email = $"paging{i}@students.test" });
                }
                return await db.SaveChangesAsync();
            });
            var total = await _factory.WithDbAsync(db => db.Students.CountAsync());

            var firstPage = await Admin.GetStringAsync("/Students");
            Assert.Contains($"Showing 1–25 of {total}", firstPage);
            Assert.Contains("<span class=\"visually-hidden\">Next page</span>", firstPage);

            var secondPage = await Admin.GetStringAsync("/Students?page=2");
            Assert.Contains($"Showing 26–{total} of {total}", secondPage);

            var search = await Admin.GetStringAsync("/Students?q=Paging%20Student%2007");
            Assert.Contains("Paging Student 07", search);
            Assert.DoesNotContain("Paging Student 08", search);

            var noMatch = await Admin.GetStringAsync("/Students?q=nobody-by-this-name");
            Assert.Contains("No students match", noMatch);
        }

        [Theory]
        [InlineData("/Students/Delete/1")]
        [InlineData("/Lecturers/Delete/1")]
        [InlineData("/Admins/Delete/1")]
        [InlineData("/Modules/Delete/1")]
        [InlineData("/Courses/Delete/1")]
        public async Task SeparateDeletePages_AreGone(string url)
        {
            var response = await Admin.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeletingAStudent_ShowsAToast()
        {
            var client = Admin;
            var id = await _factory.WithDbAsync(async db =>
            {
                var student = new Student { FullName = "Delete Me", Email = "delete.me@students.test" };
                db.Students.Add(student);
                await db.SaveChangesAsync();
                return student.StudentId;
            });

            var landing = await PostAndFollowAsync(client, $"/Students/Delete/{id}", "/Students");

            Assert.Contains("Delete Me was deleted.", landing);
            Assert.False(await _factory.WithDbAsync(db => db.Students.AnyAsync(s => s.StudentId == id)));
        }

        [Fact]
        public async Task DeletingALecturerWhoTeachesModules_IsRefusedWithAMessage()
        {
            var lecturerId = _factory.Data.LecturerAId;

            var landing = await PostAndFollowAsync(Admin, $"/Lecturers/Delete/{lecturerId}", "/Lecturers");

            Assert.Contains("still teaches modules", landing);
            Assert.True(await _factory.WithDbAsync(db => db.Lecturers.AnyAsync(l => l.LecturerId == lecturerId)));
        }

        [Fact]
        public async Task AnAdminCannotDeleteTheirOwnAccount()
        {
            var adminId = _factory.Data.AdminId;

            var landing = await PostAndFollowAsync(Admin, $"/Admins/Delete/{adminId}", "/Admins");

            Assert.Contains("You can&#x27;t delete your own account.", landing);
            Assert.True(await _factory.WithDbAsync(db => db.Admins.AnyAsync(a => a.AdminId == adminId)));
        }

        [Fact]
        public async Task EditingAnAdmin_Saves()
        {
            var client = Admin;
            var id = await _factory.WithDbAsync(async db =>
            {
                var admin = new Admin { Username = "rename-me" };
                db.Admins.Add(admin);
                await db.SaveChangesAsync();
                return admin.AdminId;
            });
            var idText = id.ToString(CultureInfo.InvariantCulture);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Admins/Edit/{idText}");

            var response = await client.PostAsync($"/Admins/Edit/{idText}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["AdminId"] = idText,
                ["Username"] = "renamed",
                ["Role"] = "Admin"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Admins.AnyAsync(a => a.AdminId == id && a.Username == "renamed")));
        }

        [Fact]
        public async Task CoursePages_AddModulesThenEnrolAClassForOneTerm()
        {
            var client = Admin;
            var data = _factory.Data;
            var (courseId, studentId) = await _factory.WithDbAsync(async db =>
            {
                var course = new Course { Code = "TEST0101", Name = "Test Course" };
                var student = new Student { FullName = "Course Student", Email = "course.student@students.test" };
                db.AddRange(course, student);
                (await db.Modules.FindAsync(data.ModuleBId))!.Term = "Term2";
                await db.SaveChangesAsync();
                return (course.CourseId, student.StudentId);
            });
            var id = Id(courseId);

            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Courses/Modules/{id}");
            var modulesResponse = await client.PostAsync($"/Courses/Modules/{id}", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("moduleIds", Id(data.ModuleAId)),
                new KeyValuePair<string, string>("moduleIds", Id(data.ModuleBId))
            }));
            Assert.Equal(HttpStatusCode.Redirect, modulesResponse.StatusCode);
            Assert.Contains("TEST0101 now has 2 module(s).", await client.GetStringAsync(modulesResponse.Headers.Location));

            token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Courses/Enrol/{id}");
            var enrolResponse = await client.PostAsync($"/Courses/Enrol/{id}", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("term", "Term2"),
                new KeyValuePair<string, string>("mode", "enrol"),
                new KeyValuePair<string, string>("selectedStudentIds", Id(studentId))
            }));
            Assert.Equal(HttpStatusCode.Redirect, enrolResponse.StatusCode);
            Assert.Contains("Enrolled 1 student(s) in 1 Term 2 module(s) of TEST0101", await client.GetStringAsync(enrolResponse.Headers.Location));

            var enrolledIn = await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == studentId)
                .SelectMany(s => s.Modules.Select(m => m.ModuleId)).ToListAsync());
            Assert.Equal(new[] { data.ModuleBId }, enrolledIn);
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static async Task<string> PostAndFollowAsync(HttpClient client, string url, string formPage)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formPage);
            var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return await client.GetStringAsync(response.Headers.Location);
        }
    }
}
