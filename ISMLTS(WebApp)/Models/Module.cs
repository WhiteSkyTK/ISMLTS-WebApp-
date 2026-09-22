using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }

        [Required, MaxLength(20)]
        [RegularExpression(@"^[A-Z]{2,6}\d{3,4}$", ErrorMessage = "Code must be letters followed by digits, e.g. XADAD7112.")]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }
        public Lecturer? Lecturer { get; set; }

        [Required, MaxLength(10)]
        public string Term { get; set; } = "Term1"; // Term1 or Term2

        [ForeignKey(nameof(Course))]
        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        // Navigation
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }
}
