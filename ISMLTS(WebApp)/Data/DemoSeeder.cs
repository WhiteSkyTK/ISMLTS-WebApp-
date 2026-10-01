using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Data
{
    // Development only (Seed:DemoData): fills a database that has no students yet with realistic test data so every page
    // has something to show. Values come from fixed patterns rather than random numbers, so every fresh seed looks the same.
    public static class DemoSeeder
    {
        public const string DemoSource = "Demo data";

        private static readonly (string Name, string Email)[] ExtraLecturers =
        {
            ("Nomsa Dlamini", "nomsa.dlamini@rosebank.iie.ac.za"),
            ("Pieter van Wyk", "pieter.vanwyk@rosebank.iie.ac.za"),
            ("Aisha Patel", "aisha.patel@rosebank.iie.ac.za")
        };

        // Each module's lecturer is an index into all lecturers: 0 is the seeded lecturer, 1-3 the ones above
        private static readonly (string Code, string Name, (string Code, string Name, string Term, int Lecturer)[] Modules)[] ExtraCourses =
        {
            ("DSWD0601", "Diploma in Software Development", new[]
            {
                ("PROG6211", "Programming 2A", "Term1", 1),
                ("WEDE6021", "Web Development (Intermediate)", "Term1", 1),
                ("DATA6211", "Database Programming", "Term2", 1),
                ("PROG6212", "Programming 2B", "Term2", 3)
            }),
            ("HCIT0501", "Higher Certificate in Information Technology", new[]
            {
                ("ITPP5111", "IT Practice", "Term1", 2),
                ("NETW5111", "Networking Fundamentals", "Term1", 2),
                ("COMP5112", "Computer Fundamentals", "Term2", 3),
                ("SUPP5112", "End-user Support", "Term2", 2)
            })
        };

        // ADAD0701 modules taught by someone other than the seeded lecturer in the demo
        private static readonly Dictionary<string, int> CurriculumLecturers = new()
        {
            ["APDS7311"] = 2, ["SOEN7112"] = 2, ["CLDV7111"] = 3, ["CLDV7112"] = 3
        };

        private static readonly string[] StudentNames =
        {
            "Thandi Mokoena", "Sipho Ndlovu", "Lerato Khumalo", "Kagiso Molefe", "Ayanda Zulu", "Naledi Mahlangu",
            "Tshepo Sithole", "Zanele Nkosi", "Bongani Mthembu", "Refilwe Masilo", "Musa Shabalala", "Palesa Radebe",
            "Liam Naidoo", "Priya Pillay", "Johan Botha", "Megan Smith", "Thabo Mabena", "Nandi Cele",
            "Karabo Tau", "Anele Dube", "Yusuf Moosa", "Chloe Jacobs", "Mpho Letsoalo", "Keabetswe Motaung",
            "Sibusiso Gumede", "Amahle Ngcobo", "Ruan de Klerk", "Tumi Maseko", "Lindiwe Hadebe", "Kyle Pretorius"
        };

        // How many students each course gets, in order: ADAD0701, DSWD0601, HCIT0501
        private static readonly int[] StudentsPerCourse = { 12, 10, 8 };

        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int SessionsPerModule = 6;

        public static bool IsEnabled(IConfiguration configuration) =>
            configuration.GetValue<bool>("Seed:DemoData") && !string.IsNullOrWhiteSpace(configuration["Seed:DemoPassword"]);

        public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration)
        {
            if (!IsEnabled(configuration) || await context.Students.AnyAsync()) return;

            // One hash for every demo account keeps the first start quick
            var hash = BCrypt.Net.BCrypt.HashPassword(configuration["Seed:DemoPassword"]);
            var today = DateTime.Today;

            var admin = await context.Admins.OrderBy(a => a.AdminId).FirstOrDefaultAsync();
            if (admin == null)
            {
                admin = new Admin { Username = "admin", PasswordHash = hash, Role = Roles.Admin };
                context.Admins.Add(admin);
            }
            if (!await context.Lecturers.AnyAsync())
                context.Lecturers.Add(new Lecturer { FullName = "Edward Nkata", Email = "edward.nkata@rosebank.iie.ac.za", PasswordHash = hash });
            await context.SaveChangesAsync();

            // The ADAD0701 curriculum needs a lecturer before its modules can be added
            await DataSeeder.SeedDataAsync(context, configuration);

            var lecturers = await AddLecturersAsync(context, hash);
            var courses = await AddCoursesAsync(context, lecturers);
            var students = AddStudents(context, courses, hash);
            await context.SaveChangesAsync();

            var lecturerNames = lecturers.ToDictionary(l => l.LecturerId, l => l.FullName);
            var modules = courses.SelectMany(c => c.Modules).OrderBy(m => m.ModuleId).ToList();
            var marks = new List<Mark>();
            for (var m = 0; m < modules.Count; m++)
            {
                var enrolled = students.Where(s => s.Modules.Contains(modules[m])).ToList();
                marks.AddRange(AddAssessmentsAndMarks(context, modules[m], enrolled, today));
                AddAttendance(context, modules[m], enrolled, today, m);
            }
            await context.SaveChangesAsync();

            AddMarkHistoryAndNotifications(context, marks, lecturerNames);
            AddTickets(context, students, today);
            AddAnnouncements(context, admin, modules, lecturerNames, today);
            await context.SaveChangesAsync();
        }

        private static async Task<List<Lecturer>> AddLecturersAsync(ApplicationDbContext context, string hash)
        {
            var lecturers = await context.Lecturers.OrderBy(l => l.LecturerId).ToListAsync();
            foreach (var (name, email) in ExtraLecturers)
            {
                if (lecturers.Exists(l => l.Email == email)) continue;
                var lecturer = new Lecturer { FullName = name, Email = email, PasswordHash = hash };
                context.Lecturers.Add(lecturer);
                lecturers.Add(lecturer);
            }
            await context.SaveChangesAsync();
            return lecturers;
        }

        private static async Task<List<Course>> AddCoursesAsync(ApplicationDbContext context, List<Lecturer> lecturers)
        {
            var existingCodes = await context.Modules.Select(m => m.Code).ToListAsync();
            var adad = await context.Courses.Include(c => c.Modules).FirstAsync(c => c.Code == DataSeeder.CourseCode);
            foreach (var module in adad.Modules)
            {
                if (CurriculumLecturers.TryGetValue(module.Code, out var index)) module.LecturerId = lecturers[index].LecturerId;
            }

            var courses = new List<Course> { adad };
            foreach (var (code, name, modules) in ExtraCourses)
            {
                var course = await context.Courses.Include(c => c.Modules).FirstOrDefaultAsync(c => c.Code == code);
                if (course == null)
                {
                    course = new Course { Code = code, Name = name };
                    context.Courses.Add(course);
                }
                foreach (var (moduleCode, moduleName, term, lecturer) in modules)
                {
                    if (existingCodes.Contains(moduleCode)) continue;
                    course.Modules.Add(new Module { Code = moduleCode, Name = moduleName, Term = term, LecturerId = lecturers[lecturer].LecturerId });
                }
                courses.Add(course);
            }
            await context.SaveChangesAsync();
            return courses;
        }

        // Student numbers look like the college's: st10000001@rcconnect.edu.za
        private static List<Student> AddStudents(ApplicationDbContext context, List<Course> courses, string hash)
        {
            var students = new List<Student>();
            for (var c = 0; c < courses.Count; c++)
            {
                for (var i = 0; i < StudentsPerCourse[c]; i++)
                {
                    var number = 10000001 + students.Count;
                    var student = new Student
                    {
                        FullName = StudentNames[students.Count],
                        Email = $"st{number.ToString(CultureInfo.InvariantCulture)}@rcconnect.edu.za",
                        Programme = courses[c].Name,
                        PasswordHash = hash
                    };
                    foreach (var module in courses[c].Modules) student.Modules.Add(module);
                    context.Students.Add(student);
                    students.Add(student);
                }
            }
            return students;
        }

        // 0-99, fixed for a student, module and purpose; decides scores, lateness and absences
        public static int Pattern(int studentId, int moduleId, int salt) =>
            (int)(((long)(studentId + 3) * 7919 + (long)(moduleId + 5) * 104729 + (long)salt * 7907) % 100);

        // A percentage: most students do well, roughly one in six struggles in each module
        public static decimal Ability(int studentId, int moduleId) =>
            Pattern(studentId, moduleId, 1) < 16
                ? 30 + Pattern(studentId, moduleId, 2) % 18
                : 52 + Pattern(studentId, moduleId, 3) * 0.44m;

        // Three assessments per module: an ICE task (marked and released), a quiz (partly marked, not released yet)
        // and a POE that is still open
        private static List<Mark> AddAssessmentsAndMarks(ApplicationDbContext context, Module module, List<Student> enrolled, DateTime today)
        {
            var ice = new Assessment
            {
                Module = module, Name = "ICE Task 1", Type = "ICE", DueDate = today.AddDays(-21), MaxScore = 20,
                Description = "Short in-class exercise on the first two weeks of work.",
                MarksReleased = true, MarksReleasedAt = DateTime.UtcNow.AddDays(-14)
            };
            var quiz = new Assessment
            {
                Module = module, Name = "Quiz 1", Type = "Quiz", DueDate = today.AddDays(-5), MaxScore = 30,
                Description = "Online quiz covering weeks 3 to 5."
            };
            var poe = new Assessment
            {
                Module = module, Name = "POE Part 1", Type = "POE", DueDate = today.AddDays(10), MaxScore = 100,
                Description = "First part of the portfolio of evidence. Submit a link to your repository."
            };
            context.Assessments.AddRange(ice, quiz, poe);

            var marks = new List<Mark>();
            var repo = module.Code.ToLowerInvariant();
            for (var i = 0; i < enrolled.Count; i++)
            {
                var student = enrolled[i];
                var (s, m) = (student.StudentId, module.ModuleId);
                var ability = Ability(s, m);
                var user = student.Email[..student.Email.IndexOf('@')];

                // ICE task: nearly everyone handed in; all of those are marked and released
                if (Pattern(s, m, 4) >= 8)
                {
                    AddSubmission(context, ice, student, $"https://github.com/{user}/{repo}-ice1", late: Pattern(s, m, 5) < 10);
                    var feedback = ability < 50 ? "Several parts are missing. Come and see me in consultation hours." : null;
                    marks.Add(AddMark(context, module, ice, student, ability, feedback));
                }

                // Quiz: most handed in; two thirds are marked so far and none are released yet
                if (Pattern(s, m, 6) >= 12)
                {
                    AddSubmission(context, quiz, student, $"https://github.com/{user}/{repo}-quiz1", late: Pattern(s, m, 7) < 8);
                    if (i % 3 != 2) marks.Add(AddMark(context, module, quiz, student, ability, feedback: null));
                }

                // POE: a few early submissions
                if (i % 4 == 0)
                    AddSubmission(context, poe, student, $"https://github.com/{user}/{repo}-poe", late: false);
            }
            return marks;
        }

        private static void AddSubmission(ApplicationDbContext context, Assessment assessment, Student student, string link, bool late) =>
            context.Submissions.Add(new Submission
            {
                Assessment = assessment,
                Student = student,
                Link = link,
                // Due dates are local calendar dates: on time is the day before, late is the day after
                SubmittedAt = assessment.DueDate.AddDays(late ? 1 : -1).AddHours(10).ToUniversalTime()
            });

        private static Mark AddMark(ApplicationDbContext context, Module module, Assessment assessment, Student student, decimal ability, string? feedback)
        {
            // Rounded to the nearest half mark
            var score = Math.Round(assessment.MaxScore * ability / 100m * 2, MidpointRounding.AwayFromZero) / 2;
            var mark = new Mark
            {
                Student = student,
                Module = module,
                Assessment = assessment,
                AssessmentName = assessment.Name,
                Score = Math.Min(score, assessment.MaxScore),
                MaxScore = assessment.MaxScore,
                Feedback = feedback ?? (ability >= 85 ? "Excellent work." : null),
                DateCaptured = assessment.DueDate.AddDays(3)
            };
            context.Marks.Add(mark);
            return mark;
        }

        // Weekly sessions over the last six weeks; most students attend nearly all, about one in five misses half
        private static void AddAttendance(ApplicationDbContext context, Module module, List<Student> enrolled, DateTime today, int moduleNumber)
        {
            for (var week = SessionsPerModule; week >= 1; week--)
            {
                var started = today.AddDays(-7 * week).AddHours(9).ToUniversalTime();
                var session = new AttendanceSession
                {
                    Module = module,
                    Code = SessionCode(moduleNumber * SessionsPerModule + week),
                    StartedAt = started,
                    ExpiresAt = started.AddMinutes(15),
                    IsClosed = true,
                    Latitude = -26.1455,
                    Longitude = 28.0436
                };
                context.AttendanceSessions.Add(session);

                foreach (var student in enrolled)
                {
                    var (s, m) = (student.StudentId, module.ModuleId);
                    var poorAttender = Pattern(s, m, 8) < 20;
                    var absent = poorAttender ? week % 2 == 0 : Pattern(s, m, 10 + week) < 8;
                    if (absent) continue;

                    var manual = Pattern(s, m, 20 + week) < 5;
                    context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        Session = session,
                        Student = student,
                        ScannedAt = started.AddMinutes(2 + Pattern(s, m, 30 + week) % 10),
                        IsManual = manual,
                        IpAddress = manual ? null : "203.0.113.10",
                        IpOnCampus = !manual,
                        Latitude = manual ? null : -26.1456,
                        Longitude = manual ? null : 28.0438,
                        AccuracyMeters = manual ? null : 15,
                        DistanceMeters = manual ? null : 10 + Pattern(s, m, 40 + week) % 60,
                        LocationVerified = !manual
                    });
                }
            }
        }

        // Six characters from the attendance alphabet, all starting with "D" so they stand out as demo sessions
        public static string SessionCode(int number)
        {
            var code = new char[6];
            code[0] = 'D';
            for (var i = 5; i >= 1; i--)
            {
                code[i] = CodeAlphabet[number % CodeAlphabet.Length];
                number /= CodeAlphabet.Length;
            }
            return new string(code);
        }

        // Mark history needs the saved MarkId; students get a bell notification for each released mark
        private static void AddMarkHistoryAndNotifications(ApplicationDbContext context, List<Mark> marks, Dictionary<int, string> lecturerNames)
        {
            foreach (var mark in marks)
            {
                var module = mark.Module!;
                context.MarkChanges.Add(new MarkChange
                {
                    MarkId = mark.MarkId,
                    StudentId = mark.StudentId,
                    ModuleId = mark.ModuleId,
                    AssessmentName = mark.AssessmentName,
                    Action = MarkChangeActions.Created,
                    NewScore = mark.Score,
                    NewMaxScore = mark.MaxScore,
                    FeedbackChanged = mark.Feedback != null,
                    Source = DemoSource,
                    ChangedById = module.LecturerId,
                    ChangedByName = lecturerNames[module.LecturerId],
                    ChangedAt = mark.DateCaptured.ToUniversalTime()
                });

                if (mark.Assessment?.MarksReleased != true) continue;
                context.Notifications.Add(new Notification
                {
                    Role = Roles.Student,
                    UserId = mark.StudentId,
                    Title = $"Marks released: {mark.AssessmentName}",
                    Message = string.Create(CultureInfo.InvariantCulture, $"{module.Code} · {mark.Score:0.##}/{mark.MaxScore:0.##} ({mark.Percentage:0.#}%)"),
                    Url = "/Marks/MyMarks",
                    CreatedAt = mark.Assessment.MarksReleasedAt ?? DateTime.UtcNow,
                    // Term 2 releases are still unread so the bell has something to show
                    IsRead = module.Term == "Term1"
                });
            }
        }

        private static void AddTickets(ApplicationDbContext context, List<Student> students, DateTime today)
        {
            // Which student, which of their modules, subject, question, status, lecturer reply
            (int Student, int Module, string Subject, string Question, string Status, string? Reply)[] tickets =
            {
                (0, 7, "POE Part 1 brief", "Does the POE need unit tests, or only the running app?", "Open", null),
                (1, 7, "Missed the attendance scan", "My phone died in class on Monday. Can you mark me present?", "In Progress", "I will check the register after class tomorrow."),
                (2, 0, "Quiz 1 mark", "When will the Quiz 1 marks be released?", "Resolved", "They go out on Friday once everyone has written."),
                (5, 2, "Cloud credits", "My Azure student credits ran out. Is there another subscription we can use?", "Open", null),
                (12, 0, "Extension request", "I was in hospital last week. Can I hand in ICE Task 1 late?", "Open", null),
                (14, 3, "Group project partner", "My partner dropped out. Can I do the POE alone?", "In Progress", "Yes, but talk to me about the reduced scope."),
                (22, 1, "Lab access", "The networking lab was locked during our session.", "Resolved", "Sorted with facilities. It is open from 08:00 now.")
            };
            foreach (var (student, module, subject, question, status, reply) in tickets)
            {
                context.Tickets.Add(new Ticket
                {
                    Student = students[student],
                    Module = students[student].Modules.OrderBy(m => m.ModuleId).ElementAt(module),
                    Subject = subject,
                    Description = question,
                    Status = status,
                    LecturerResponse = reply,
                    DateOpened = today.AddDays(-3 - student % 4).AddHours(11).ToUniversalTime(),
                    DateResolved = status == "Resolved" ? today.AddDays(-1).AddHours(15).ToUniversalTime() : null
                });
            }
        }

        private static void AddAnnouncements(ApplicationDbContext context, Admin admin, List<Module> modules, Dictionary<int, string> lecturerNames, DateTime today)
        {
            context.Announcements.Add(new Announcement
            {
                Title = "Welcome to ISMLTS",
                Body = "Your marks, assessments, attendance and questions for your lecturers now live in one place. Check My Progress each week.",
                Audience = AnnouncementAudiences.Everyone,
                AuthorRole = Roles.Admin,
                AuthorId = admin.AdminId,
                AuthorName = admin.Username,
                CreatedAt = today.AddDays(-20).AddHours(8).ToUniversalTime()
            });
            context.Announcements.Add(new Announcement
            {
                Title = "Moderation meeting on Thursday",
                Body = "All lecturers: bring your Quiz 1 marks so we can moderate them before they are released.",
                Audience = AnnouncementAudiences.Lecturers,
                AuthorRole = Roles.Admin,
                AuthorId = admin.AdminId,
                AuthorName = admin.Username,
                CreatedAt = today.AddDays(-1).AddHours(14).ToUniversalTime()
            });

            var wil = modules.Find(m => m.Code == "XADAD7112");
            if (wil == null) return;
            context.Announcements.Add(new Announcement
            {
                Title = "POE Part 1 is open",
                Body = "Submit a link to your repository by the due date and make sure your lecturer has access to it.",
                Module = wil,
                Audience = AnnouncementAudiences.Students,
                AuthorRole = Roles.Lecturer,
                AuthorId = wil.LecturerId,
                AuthorName = lecturerNames[wil.LecturerId],
                CreatedAt = today.AddDays(-2).AddHours(9).ToUniversalTime()
            });
        }
    }
}
