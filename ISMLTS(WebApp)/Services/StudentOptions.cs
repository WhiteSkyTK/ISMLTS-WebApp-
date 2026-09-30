namespace ISMLTS_WebApp_.Services
{
    // "Students" config section
    public class StudentOptions
    {
        // Every student email must be at this domain (IIE Rosebank student addresses)
        public string EmailDomain { get; set; } = "rcconnect.edu.za";

        public bool IsStudentEmail(string? email) =>
            email != null && email.Trim().EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);

        public string EmailDomainMessage => $"Student emails must end in @{EmailDomain}.";
    }
}
