namespace ISMLTS_WebApp_.Models
{
    // What signing in and the profile page need from Admin, Lecturer and Student (users live in three tables)
    public interface IUserAccount
    {
        string PasswordHash { get; set; }
    }

    // One signed-in user, whichever table they live in. Login is the admin username or the lecturer/student email.
    public record UserAccount(string Role, int Id, string DisplayName, string Login, IUserAccount Entity);
}
