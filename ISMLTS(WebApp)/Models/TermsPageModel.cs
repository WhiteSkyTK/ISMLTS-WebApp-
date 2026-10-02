namespace ISMLTS_WebApp_.Models
{
    public class TermsPageModel
    {
        public List<Term> Terms { get; set; } = new();
        public int? CurrentTermId { get; set; }
        public List<CollegeDate> Dates { get; set; } = new();
        public CollegeDate NewDate { get; set; } = new();
    }
}
