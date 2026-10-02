using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Uploaded work opens only for the student who handed it in and the lecturer who teaches the module
    public class FileSubmissionTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public FileSubmissionTests(ApiFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private HttpClient StudentA => _factory.ClientFor(Roles.Student, _factory.Data.StudentId);
        private HttpClient StudentB => _factory.ClientFor(Roles.Student, _factory.Data.OtherStudentId);
        private HttpClient LecturerA => _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId);
        private HttpClient LecturerB => _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerBId);

        // A fresh assessment in Module A, so tests don't share submissions
        private Task<int> NewAssessmentAsync(int dueInDays = 7, int? lateDays = null) =>
            _factory.WithDbAsync(async db =>
            {
                var assessment = new Assessment { ModuleId = _factory.Data.ModuleAId, Name = $"Upload {Guid.NewGuid():N}"[..20], DueDate = DateTime.Today.AddDays(dueInDays), LateDays = lateDays };
                db.Assessments.Add(assessment);
                await db.SaveChangesAsync();
                return assessment.AssessmentId;
            });

        private static MultipartFormDataContent Form(byte[] content, string fileName, params (string Key, string Value)[] fields)
        {
            var form = new MultipartFormDataContent();
            foreach (var (key, value) in fields) form.Add(new StringContent(value), key);
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", fileName);
            return form;
        }

        // The layout's log-out form carries a token on every page, so this works even when the page has no upload form
        private static async Task<HttpResponseMessage> UploadAsync(HttpClient student, int assessmentId, byte[] content, string fileName)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(student, $"/Assessments/Submit/{Id(assessmentId)}");
            using var form = Form(content, fileName, ("__RequestVerificationToken", token));
            return await student.PostAsync($"/Assessments/Submit/{Id(assessmentId)}", form);
        }

        private Task<List<SubmissionFile>> FilesForAsync(int assessmentId) =>
            _factory.WithDbAsync(db => db.SubmissionFiles.Where(f => f.Submission!.AssessmentId == assessmentId).ToListAsync());

        private async Task<HttpClient> AppAsync(string email)
        {
            var login = await _factory.ClientFor().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, _factory.Data.Password, null));
            var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
            var client = _factory.ClientFor();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
            return client;
        }

        private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("code").GetString();
        }

        [Fact]
        public async Task UploadedFile_OpensForItsStudentAndTheirLecturer_AndNobodyElse()
        {
            var assessmentId = await NewAssessmentAsync();

            var upload = await UploadAsync(StudentA, assessmentId, TestFiles.Pdf(), "My Work.pdf");
            Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
            Assert.Equal("/Assessments/MyAssessments", upload.Headers.Location?.OriginalString);

            var file = Assert.Single(await FilesForAsync(assessmentId));
            Assert.Equal("My Work.pdf", file.FileName);
            Assert.DoesNotContain("Work", file.StoredName);
            var url = $"/SubmissionFiles/Download/{Id(file.SubmissionFileId)}";

            foreach (var allowed in new[] { StudentA, LecturerA })
            {
                var response = await allowed.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
                Assert.Equal("My Work.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
                Assert.StartsWith("%PDF-", await response.Content.ReadAsStringAsync());
            }

            Assert.Equal(HttpStatusCode.NotFound, (await StudentB.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await LecturerB.GetAsync(url)).StatusCode);
            var admin = await _factory.ClientFor(Roles.Admin, _factory.Data.AdminId).GetAsync(url);
            Assert.StartsWith("/Account/AccessDenied", admin.Headers.Location?.PathAndQuery);
            var anonymous = await _factory.ClientFor().GetAsync(url);
            Assert.StartsWith("/Account/Login", anonymous.Headers.Location?.PathAndQuery);
        }

        [Fact]
        public async Task LecturerSeesTheFile_OnTheSubmissionsPage()
        {
            var assessmentId = await NewAssessmentAsync();
            await UploadAsync(StudentA, assessmentId, TestFiles.Pdf(), "first.pdf");
            await UploadAsync(StudentA, assessmentId, TestFiles.Docx(), "second.docx");

            var html = await LecturerA.GetStringAsync($"/Assessments/Submissions/{Id(assessmentId)}");

            Assert.Contains("second.docx", html);
            Assert.DoesNotContain("first.pdf", html);
            Assert.Contains("1 earlier upload kept", html);
            Assert.Equal(2, (await FilesForAsync(assessmentId)).Count);
        }

        [Fact]
        public async Task EveryPageThatShowsWork_LinksToTheFile()
        {
            // Due long ago (late work still accepted), so it's first in Quick Eval's oldest-due-first queue
            var assessmentId = await NewAssessmentAsync(dueInDays: -400);
            var student = StudentA;
            await UploadAsync(student, assessmentId, TestFiles.Zip(("src/Program.cs", "class P {}")), "project.zip");
            var fileId = Id(Assert.Single(await FilesForAsync(assessmentId)).SubmissionFileId);
            var download = $"href=\"/SubmissionFiles/Download/{fileId}\"";

            var pages = new (HttpClient Client, string Url)[]
            {
                (student, "/Assessments/MyAssessments"),
                (student, $"/Assessments/Submit/{Id(assessmentId)}"),
                (student, "/Portfolio"),
                (LecturerA, $"/Grading/Gradebook/{Id(assessmentId)}"),
                (LecturerA, "/Grading/QuickEval")
            };
            foreach (var (client, url) in pages)
            {
                var html = await client.GetStringAsync(url);
                Assert.True(html.Contains(download), $"{url} has no link to the file");
                Assert.Contains("project.zip", html);
            }
        }

        [Fact]
        public async Task AFileThatIsntWhatItsNameSays_IsTurnedAway()
        {
            var assessmentId = await NewAssessmentAsync();

            var response = await UploadAsync(StudentA, assessmentId, TestFiles.Program(), "work.pdf");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("a real PDF", await response.Content.ReadAsStringAsync());
            Assert.Empty(await FilesForAsync(assessmentId));
        }

        [Fact]
        public async Task AfterTheWindowCloses_WorkIsTurnedAway()
        {
            var assessmentId = await NewAssessmentAsync(dueInDays: -2, lateDays: 1);

            var page = await StudentA.GetStringAsync($"/Assessments/Submit/{Id(assessmentId)}");
            var response = await UploadAsync(StudentA, assessmentId, TestFiles.Pdf(), "late.pdf");

            Assert.Contains("Submissions are closed", page);
            Assert.DoesNotContain("enctype=\"multipart/form-data\"", page);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.False(await _factory.WithDbAsync(db => db.Submissions.AnyAsync(s => s.AssessmentId == assessmentId)));
        }

        [Fact]
        public async Task Lecturer_SetsTheLateWindow_OnTheAssessmentForm()
        {
            var moduleId = Id(_factory.Data.ModuleAId);
            var lecturer = LecturerA;
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(lecturer, $"/Assessments/Create?moduleId={moduleId}");
            var name = $"Window {Guid.NewGuid():N}"[..20];

            var response = await lecturer.PostAsync("/Assessments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ModuleId"] = moduleId,
                ["Name"] = name,
                ["Type"] = "POE",
                ["MaxScore"] = "100",
                ["DueDate"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["LateDays"] = "2"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(2, await _factory.WithDbAsync(db => db.Assessments.Where(a => a.Name == name).Select(a => a.LateDays).SingleAsync()));
        }

        [Fact]
        public async Task DeletingTheAssessment_RemovesItsFilesFromStorage()
        {
            var assessmentId = await NewAssessmentAsync();
            await UploadAsync(StudentA, assessmentId, TestFiles.Pdf(), "work.pdf");
            var stored = Path.Combine(_factory.UploadFolder, Assert.Single(await FilesForAsync(assessmentId)).StoredName);
            Assert.True(File.Exists(stored));

            var lecturer = LecturerA;
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(lecturer, $"/Assessments/ForModule?moduleId={Id(_factory.Data.ModuleAId)}");
            var response = await lecturer.PostAsync($"/Assessments/Delete/{Id(assessmentId)}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.False(File.Exists(stored));
        }

        // ---------- The Android app ----------

        [Fact]
        public async Task App_UploadsAFile_AndDownloadsOnlyItsOwn()
        {
            var assessmentId = await NewAssessmentAsync();
            var app = await AppAsync(_factory.Data.StudentEmail);

            using var form = Form(TestFiles.Docx(), "essay.docx");
            var upload = await app.PostAsync($"/api/v1/assessments/{Id(assessmentId)}/files", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            var assessment = (await upload.Content.ReadFromJsonAsync<AssessmentDto>())!;
            Assert.Equal(("essay.docx", 1, "submitted", true), (assessment.File?.Name, assessment.FileCount, assessment.Status, assessment.SubmissionsOpen));

            var download = await app.GetAsync($"/api/v1/files/{Id(assessment.File!.FileId)}");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", download.Content.Headers.ContentType?.MediaType);

            // Student B isn't even in the module
            var other = await AppAsync("t@rcconnect.edu.za");
            var refused = await other.GetAsync($"/api/v1/files/{Id(assessment.File.FileId)}");
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            Assert.Equal("not_found", await ProblemCodeAsync(refused));
        }

        [Fact]
        public async Task App_GetsAReasonForEveryRefusedUpload()
        {
            var open = await NewAssessmentAsync();
            var closed = await NewAssessmentAsync(dueInDays: -1, lateDays: 0);
            var app = await AppAsync(_factory.Data.StudentEmail);

            using var fake = Form(TestFiles.Program(), "work.zip");
            var badFile = await app.PostAsync($"/api/v1/assessments/{Id(open)}/files", fake);
            using var noFile = new MultipartFormDataContent { { new StringContent("x"), "note" } };
            var missing = await app.PostAsync($"/api/v1/assessments/{Id(open)}/files", noFile);
            using var late = Form(TestFiles.Pdf(), "late.pdf");
            var closedResponse = await app.PostAsync($"/api/v1/assessments/{Id(closed)}/files", late);
            using var otherModule = Form(TestFiles.Pdf(), "work.pdf");
            var notMine = await app.PostAsync($"/api/v1/assessments/{Id(_factory.Data.AssessmentBId)}/files", otherModule);

            Assert.Equal((HttpStatusCode.BadRequest, "invalid_file"), (badFile.StatusCode, await ProblemCodeAsync(badFile)));
            Assert.Equal((HttpStatusCode.BadRequest, "missing_file"), (missing.StatusCode, await ProblemCodeAsync(missing)));
            Assert.Equal((HttpStatusCode.Conflict, "submissions_closed"), (closedResponse.StatusCode, await ProblemCodeAsync(closedResponse)));
            Assert.Equal((HttpStatusCode.NotFound, "not_found"), (notMine.StatusCode, await ProblemCodeAsync(notMine)));
        }
    }
}
