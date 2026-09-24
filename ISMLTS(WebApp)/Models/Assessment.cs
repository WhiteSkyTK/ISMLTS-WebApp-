using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Assessment
    {
        [Key]
        public int AssessmentId { get; set; }

        [ForeignKey(nameof(Module))]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Type { get; set; } = "ICE"; // ICE, Quiz, POE, Project

        [Required, DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}