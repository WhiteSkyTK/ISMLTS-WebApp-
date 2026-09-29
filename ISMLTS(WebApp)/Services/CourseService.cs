using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // Which of a course's modules an enrolment applies to
    public static class CourseTerms
    {
        public const string All = "All";
        public const string Term1 = "Term1";
        public const string Term2 = "Term2";

        public static bool IsValid(string? term) => term is All or Term1 or Term2;

        public static bool Matches(string moduleTerm, string term) => term == All || moduleTerm == term;
    }

    public record CourseEnrolmentResult(int Students, int Modules, int Changes);

    public interface ICourseService
    {
        Task<int> AssignModulesAsync(int courseId, IReadOnlyCollection<int> moduleIds);
        Task<CourseEnrolmentResult> EnrolAsync(Course course, IReadOnlyCollection<int> studentIds, string term);
        Task<CourseEnrolmentResult> UnenrolAsync(Course course, IReadOnlyCollection<int> studentIds, string term);
    }

    public class CourseService : ICourseService
    {
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;

        public CourseService(IModuleRepository moduleRepository, IStudentRepository studentRepository)
        {
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
        }

        // Ticked modules join the course; unticked modules that were in it leave it. A module belongs to one course.
        public async Task<int> AssignModulesAsync(int courseId, IReadOnlyCollection<int> moduleIds)
        {
            var modules = await _moduleRepository.GetAllWithCourseAsync();
            foreach (var module in modules)
            {
                if (moduleIds.Contains(module.ModuleId))
                {
                    module.CourseId = courseId;
                }
                else if (module.CourseId == courseId)
                {
                    module.CourseId = null;
                }
            }
            await _moduleRepository.SaveChangesAsync();
            return modules.Count(m => m.CourseId == courseId);
        }

        // Enrols each student in every module of the course for the chosen term(s); existing enrolments are kept
        public async Task<CourseEnrolmentResult> EnrolAsync(Course course, IReadOnlyCollection<int> studentIds, string term)
        {
            var modules = await ModulesForAsync(course.CourseId, term);
            var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToList();

            var changes = 0;
            foreach (var module in modules)
            {
                foreach (var student in students.Where(s => module.Students.All(e => e.StudentId != s.StudentId)))
                {
                    module.Students.Add(student);
                    changes++;
                }
            }
            foreach (var student in students)
            {
                student.Programme = course.Name;
            }

            await _moduleRepository.SaveChangesAsync();
            return new CourseEnrolmentResult(students.Count, modules.Count, changes);
        }

        public async Task<CourseEnrolmentResult> UnenrolAsync(Course course, IReadOnlyCollection<int> studentIds, string term)
        {
            var modules = await ModulesForAsync(course.CourseId, term);

            var changes = 0;
            foreach (var module in modules)
            {
                foreach (var student in module.Students.Where(s => studentIds.Contains(s.StudentId)).ToList())
                {
                    module.Students.Remove(student);
                    changes++;
                }
            }

            await _moduleRepository.SaveChangesAsync();
            return new CourseEnrolmentResult(studentIds.Count, modules.Count, changes);
        }

        private async Task<List<Module>> ModulesForAsync(int courseId, string term) =>
            (await _moduleRepository.GetByCourseWithStudentsAsync(courseId))
                .Where(m => CourseTerms.Matches(m.Term, term))
                .ToList();
    }
}
