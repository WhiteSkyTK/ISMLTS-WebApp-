using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty; // e.g. ADAD0701

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty; // e.g. Advanced Diploma in Application Development

        public ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}