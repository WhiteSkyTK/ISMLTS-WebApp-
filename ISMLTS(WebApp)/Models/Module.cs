using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [ForeignKey(nameof(Lecturer))]
        public int LecturerId { get; set; }
        public Lecturer? Lecturer { get; set; }

        // Navigation
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }
}
