using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Mark : IValidatableObject
    {
        [Key]
        public int MarkId { get; set; }

        [ForeignKey(nameof(Student))]
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [ForeignKey(nameof(Module))]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        [Required, MaxLength(100)]
        public string AssessmentName { get; set; } = string.Empty;

        [Range(0, 1000)]
        public decimal Score { get; set; }

        [Range(0.01, 1000)]
        public decimal MaxScore { get; set; } = 100;

        [DataType(DataType.Date)]
        public DateTime DateCaptured { get; set; } = DateTime.Today;

        [NotMapped]
        public decimal Percentage => MaxScore == 0 ? 0 : Math.Round(Score / MaxScore * 100, 1);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Score > MaxScore)
            {
                yield return new ValidationResult("Score cannot be more than Max Score.", new[] { nameof(Score) });
            }
        }
    }

}