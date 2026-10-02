using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // One teaching term with its dates, e.g. "2026 Term 2". Code matches Module.Term (Term1 or Term2).
    public class Term
    {
        [Key]
        public int TermId { get; set; }

        [Required, MaxLength(60)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        [Display(Name = "Modules taught")]
        public string Code { get; set; } = "Term1";

        [DataType(DataType.Date)]
        [Display(Name = "First day")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Last day")]
        public DateTime EndDate { get; set; }
    }
}
