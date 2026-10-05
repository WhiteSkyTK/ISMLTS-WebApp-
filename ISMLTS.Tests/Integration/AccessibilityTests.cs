using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ISMLTS_WebApp_.Models;

namespace ISMLTS.Tests.Integration
{
    // Automated WCAG 2.1 checks on every main page, for every role, in CI. These are the axe rules that can be judged
    // from the HTML alone (names, labels, ids, landmarks, headings, focus order); colour contrast and keyboard use
    // are covered by the palette check and the manual walk-through in docs/ACCESSIBILITY.md.
    public class AccessibilityTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AccessibilityTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string, string> Pages => new()
        {
            { "", "/" }, { "", "/Account/Login" }, { "", "/Home/Privacy" },
            { Roles.Student, "/" }, { Roles.Student, "/Assessments/MyAssessments" }, { Roles.Student, "/Assessments/Submit/{assessmentA}" },
            { Roles.Student, "/Marks/MyMarks" }, { Roles.Student, "/Tickets/MyTickets" }, { Roles.Student, "/Tickets/Create" },
            { Roles.Student, "/Attendance/MyAttendance" }, { Roles.Student, "/Attendance/Scan" }, { Roles.Student, "/Insights/MyProgress" },
            { Roles.Student, "/Awards" }, { Roles.Student, "/Portfolio" }, { Roles.Student, "/Calendar" }, { Roles.Student, "/Profile" },
            { Roles.Student, "/Help" }, { Roles.Student, "/Notifications" }, { Roles.Student, "/Announcements" },
            { Roles.Lecturer, "/" }, { Roles.Lecturer, "/Marks" }, { Roles.Lecturer, "/Marks/ForModule?moduleId={moduleA}" },
            { Roles.Lecturer, "/Assessments/ForModule?moduleId={moduleA}" }, { Roles.Lecturer, "/Assessments/Create?moduleId={moduleA}" },
            { Roles.Lecturer, "/Assessments/Submissions/{assessmentA}" }, { Roles.Lecturer, "/Attendance" },
            { Roles.Lecturer, "/Attendance/ForModule?moduleId={moduleA}" }, { Roles.Lecturer, "/Tickets" }, { Roles.Lecturer, "/Grading/QuickEval" },
            { Roles.Lecturer, "/Grading/Gradebook/{assessmentA}" }, { Roles.Lecturer, "/Insights/Class" }, { Roles.Lecturer, "/Timetable" },
            { Roles.Lecturer, "/Calendar" },
            { Roles.Admin, "/" }, { Roles.Admin, "/Students" }, { Roles.Admin, "/Students/Create" }, { Roles.Admin, "/Lecturers" },
            { Roles.Admin, "/Admins" }, { Roles.Admin, "/Modules" }, { Roles.Admin, "/Modules/Create" }, { Roles.Admin, "/Courses" },
            { Roles.Admin, "/Terms" }, { Roles.Admin, "/Terms/Create" }, { Roles.Admin, "/Audit" }, { Roles.Admin, "/Reports" },
            { Roles.Admin, "/Import" }, { Roles.Admin, "/SiteCheck" }, { Roles.Admin, "/Calendar" }
        };

        [Theory]
        [MemberData(nameof(Pages))]
        public async Task EveryPage_PassesTheHtmlAccessibilityRules(string role, string template)
        {
            var data = _factory.Data;
            var url = template
                .Replace("{assessmentA}", data.AssessmentAId.ToString(CultureInfo.InvariantCulture))
                .Replace("{moduleA}", data.ModuleAId.ToString(CultureInfo.InvariantCulture));
            var userId = role switch { Roles.Student => data.StudentId, Roles.Lecturer => data.LecturerAId, Roles.Admin => data.AdminId, _ => (int?)null };
            var response = await _factory.ClientFor(role == "" ? null : role, userId).GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, $"{role} {url} answered {(int)response.StatusCode}");

            var document = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
            var problems = Check(document);

            Assert.True(problems.Count == 0, $"{role} {url}:\n" + string.Join("\n", problems));
        }

        public static List<string> Check(IDocument page)
        {
            var problems = new List<string>();
            var ids = page.All.Where(e => e.HasAttribute("id")).GroupBy(e => e.Id!).ToList();

            if (string.IsNullOrWhiteSpace(page.DocumentElement.GetAttribute("lang"))) problems.Add("html has no lang");
            if (string.IsNullOrWhiteSpace(page.Title)) problems.Add("page has no title");
            if (page.QuerySelectorAll("main").Length != 1) problems.Add("page needs exactly one main landmark");
            if (page.QuerySelectorAll("h1").Length == 0) problems.Add("page has no h1");

            problems.AddRange(ids.Where(g => g.Count() > 1).Select(g => $"duplicate id \"{g.Key}\""));
            var known = ids.Select(g => g.Key).ToHashSet();
            foreach (var element in page.All)
            {
                foreach (var attribute in new[] { "for", "aria-labelledby", "aria-describedby", "aria-controls" })
                {
                    var value = element.GetAttribute(attribute);
                    if (value == null) continue;
                    foreach (var reference in value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(r => !known.Contains(r)))
                        problems.Add($"{Describe(element)} {attribute} points at missing id \"{reference}\"");
                }
                if (element.HasAttribute("autofocus")) problems.Add($"{Describe(element)} uses autofocus");
                if (int.TryParse(element.GetAttribute("tabindex"), out var tabIndex) && tabIndex > 0) problems.Add($"{Describe(element)} has a positive tabindex");
            }

            problems.AddRange(page.QuerySelectorAll("img:not([alt])").Select(e => $"{Describe(e)} has no alt"));

            var labelled = page.QuerySelectorAll("label[for]").Select(l => l.GetAttribute("for")).ToHashSet();
            foreach (var control in page.QuerySelectorAll("input, select, textarea"))
            {
                var type = control.GetAttribute("type")?.ToLowerInvariant();
                if (type is "hidden" or "submit" or "button" or "reset" or "image") continue;
                var named = (control.Id != null && labelled.Contains(control.Id)) || control.Closest("label") != null
                    || control.HasAttribute("aria-label") || control.HasAttribute("aria-labelledby") || control.HasAttribute("title");
                if (!named) problems.Add($"{Describe(control)} has no label");
            }

            foreach (var element in page.QuerySelectorAll("a[href], button"))
            {
                if (string.IsNullOrWhiteSpace(Name(element))) problems.Add($"{Describe(element)} has no accessible name");
            }

            foreach (var header in page.QuerySelectorAll("th"))
            {
                if (string.IsNullOrWhiteSpace(header.TextContent)) problems.Add($"empty table header in {Describe(header.Closest("table")!)}");
            }

            foreach (var hidden in page.QuerySelectorAll("[aria-hidden=true]"))
            {
                if (hidden.QuerySelectorAll("a[href], button, input, select, textarea").Length > 0 && hidden.Id != "confirmModal" && !hidden.ClassList.Contains("modal"))
                    problems.Add($"{Describe(hidden)} is aria-hidden but holds focusable elements");
            }

            // Headings may go back up any number of levels but only down one at a time
            var level = 0;
            foreach (var heading in page.QuerySelectorAll("h1, h2, h3, h4, h5, h6"))
            {
                var next = heading.LocalName[1] - '0';
                if (level > 0 && next > level + 1) problems.Add($"heading jumps from h{level} to h{next} at \"{heading.TextContent.Trim()}\"");
                level = next;
            }

            return problems;
        }

        // Visible text (icons are aria-hidden) plus visually-hidden text, or an aria-label or title
        private static string Name(IElement element)
        {
            if (element.GetAttribute("aria-label") is { Length: > 0 } label) return label;
            var text = string.Concat(element.Descendants<IText>()
                .Where(t => !HiddenInside(t.ParentElement, element))
                .Select(t => t.Data));
            return string.IsNullOrWhiteSpace(text) ? element.GetAttribute("title") ?? string.Empty : text;
        }

        // An aria-hidden element between the text and the button or link (a hidden dialog around it doesn't count)
        private static bool HiddenInside(IElement? from, IElement container)
        {
            for (var e = from; e != null && e != container; e = e.ParentElement)
            {
                if (e.GetAttribute("aria-hidden") == "true") return true;
            }
            return false;
        }

        private static string Describe(IElement element) =>
            $"<{element.LocalName}{(!string.IsNullOrEmpty(element.Id) ? $" id=\"{element.Id}\"" : null)}{(element.ClassName != null ? $" class=\"{element.ClassName}\"" : null)}>";
    }
}
