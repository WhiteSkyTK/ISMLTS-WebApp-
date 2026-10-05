using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ISMLTS_WebApp_.Models.Api;

namespace ISMLTS.Tests.Integration
{
    public class CalendarApiTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public CalendarApiTests(ApiFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpClient> AppAsync(string email)
        {
            var login = await _factory.ClientFor().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, _factory.Data.Password, null));
            var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
            var client = _factory.ClientFor();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
            return client;
        }

        private static async Task<string?> CodeAsync(HttpResponseMessage response)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("code").GetString();
        }

        [Fact]
        public async Task TheCalendar_ShowsTheStudentsDueDates_AndChecksTheRange()
        {
            var app = await AppAsync(_factory.Data.StudentEmail);

            var entries = await app.GetFromJsonAsync<List<CalendarEntryDto>>("/api/v1/calendar");
            var badRange = await app.GetAsync("/api/v1/calendar?from=2026-10-01&to=2027-10-01");

            var due = Assert.Single(entries!, e => e.Kind == "due");
            Assert.Equal(($"{_factory.Data.ModuleACode}: POE A due", true), (due.Title, due.AllDay));
            Assert.DoesNotContain(entries!, e => e.Title.Contains(_factory.Data.ModuleBCode, StringComparison.Ordinal));
            Assert.Equal((HttpStatusCode.BadRequest, "invalid_range"), (badRange.StatusCode, await CodeAsync(badRange)));
        }

        [Fact]
        public async Task Notes_CanBeAdded_TickedOff_AndDeleted_OnlyByTheirOwner()
        {
            var app = await AppAsync(_factory.Data.StudentEmail);
            var date = DateOnly.FromDateTime(DateTime.Today.AddDays(2));

            var created = await app.PostAsJsonAsync("/api/v1/calendar/notes", new NoteRequest(date, new TimeOnly(18, 30), "Study group", "Library, level 2", true));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var note = (await created.Content.ReadFromJsonAsync<CalendarEntryDto>())!;
            Assert.Equal(("note", "Study group", date, false), (note.Kind, note.Title, note.Date, note.Done));
            Assert.NotNull(note.Start);

            var invalid = await app.PostAsJsonAsync("/api/v1/calendar/notes", new NoteRequest(date, null, " ", null, false));
            Assert.Equal((HttpStatusCode.BadRequest, "invalid_note"), (invalid.StatusCode, await CodeAsync(invalid)));

            var other = await AppAsync("t@rcconnect.edu.za");
            var url = $"/api/v1/calendar/notes/{note.NoteId!.Value.ToString(CultureInfo.InvariantCulture)}";
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(url + "/done", new NoteDoneRequest(true))).StatusCode);

            var done = await app.PutAsJsonAsync(url + "/done", new NoteDoneRequest(true));
            Assert.True((await done.Content.ReadFromJsonAsync<CalendarEntryDto>())!.Done);
            Assert.Equal(HttpStatusCode.NoContent, (await app.DeleteAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await app.DeleteAsync(url)).StatusCode);
        }
    }
}
