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

        // Null for marks captured before assessments existed, or for work that isn't a listed assessment
        [ForeignKey(nameof(Assessment))]
        [Display(Name = "Assessment")]
        public int? AssessmentId { get; set; }
        public Assessment? Assessment { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Name")]
        public string AssessmentName { get; set; } = string.Empty;

        [Range(0, 1000)]
        public decimal Score { get; set; }

        [Range(0.01, 1000)]
        [Display(Name = "Out of")]
        public decimal MaxScore { get; set; } = 100;

        [DataType(DataType.Date)]
        [Display(Name = "Date captured")]
        public DateTime DateCaptured { get; set; } = DateTime.Today;

        [MaxLength(1000)]
        public string? Feedback { get; set; }

        // Students only see a mark once its assessment's marks are released; unlinked marks are always visible
        [NotMapped]
        public bool IsVisibleToStudent => AssessmentId == null || Assessment?.MarksReleased == true;

        [NotMapped]
        public decimal Percentage => MaxScore == 0 ? 0 : Math.Round(Score / MaxScore * 100, 1);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Score > MaxScore)
            {
                yield return new ValidationResult("The score can't be more than the total it's out of.", new[] { nameof(Score) });
            }
        }
    }

}