using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.Extensions.Options;
using OtpNet;

namespace ISMLTS.Tests
{
    public class AccountServiceTests : IDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly SqliteTestDb _db = new();

        private sealed class FixedClock : TimeProvider
        {
            public DateTimeOffset At { get; set; } = Now;
            public override DateTimeOffset GetUtcNow() => At;
        }

        private static AccountService Service(ApplicationDbContext context, TimeProvider clock, bool required = true) =>
            new(new AdminRepository(context), new LecturerRepository(context), new StudentRepository(context),
                Options.Create(new TwoFactorOptions { RequiredForAdmins = required }), clock);

        private async Task<(Admin Admin, Lecturer Lecturer)> SeedAsync()
        {
            await using var context = _db.NewContext();
            var hash = BCrypt.Net.BCrypt.HashPassword("Password1!", workFactor: 4);
            var admin = new Admin { Username = "admin", PasswordHash = hash };
            var lecturer = new Lecturer { FullName = "Lecturer", Email = "l@lecturers.test", PasswordHash = hash };
            context.Admins.Add(admin);
            context.Lecturers.Add(lecturer);
            await context.SaveChangesAsync();
            return (admin, lecturer);
        }

        private static string CodeAt(string secret, DateTimeOffset time) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(time.UtcDateTime);

        private static string WrongCode(string secret, DateTimeOffset time) => CodeAt(secret, time) == "000000" ? "111111" : "000000";

        [Fact]
        public async Task SignIn_FindsAdminsByUsername_AndOthersByEmail()
        {
            await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());

            Assert.Equal(Roles.Admin, (await service.SignInAsync("admin", "Password1!"))?.Role);
            Assert.Equal(Roles.Lecturer, (await service.SignInAsync("l@lecturers.test", "Password1!"))?.Role);
            Assert.Null(await service.SignInAsync("admin", "wrong"));
        }

        [Fact]
        public async Task OnlyAdmins_MustSetUpTwoFactor_AndOnlyWhenRequired()
        {
            var (admin, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var adminAccount = (await service.FindAsync(Roles.Admin, admin.AdminId))!;
            var lecturerAccount = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;

            Assert.True(service.MustSetUpTwoFactor(adminAccount));
            Assert.False(service.CanTurnOffTwoFactor(adminAccount));
            Assert.False(service.MustSetUpTwoFactor(lecturerAccount));
            Assert.True(service.CanTurnOffTwoFactor(lecturerAccount));
            Assert.False(Service(context, new FixedClock(), required: false).MustSetUpTwoFactor(adminAccount));
        }

        [Fact]
        public async Task Setup_KeepsTheSameSecret_UntilItIsSwitchedOn()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;

            var first = await service.StartTwoFactorSetupAsync(account);
            var second = await service.StartTwoFactorSetupAsync(account);

            Assert.Equal(first, second);
            Assert.False(account.Entity.TwoFactorEnabled);
        }

        [Fact]
        public async Task Enable_NeedsTheRightCode_AndReturnsRecoveryCodes()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;
            var secret = await service.StartTwoFactorSetupAsync(account);

            Assert.Null(await service.EnableTwoFactorAsync(account, WrongCode(secret, Now)));
            Assert.False(account.Entity.TwoFactorEnabled);

            var codes = await service.EnableTwoFactorAsync(account, CodeAt(secret, Now));

            Assert.NotNull(codes);
            Assert.Equal(8, codes.Count);
            await using var check = _db.NewContext();
            var saved = await check.Lecturers.FindAsync(lecturer.LecturerId);
            Assert.True(saved!.TwoFactorEnabled);
            Assert.Equal(8, TwoFactor.RecoveryCodesLeft(saved.TwoFactorRecoveryCodes));
        }

        [Fact]
        public async Task Check_AcceptsACodeOnce_ThenTheNextOne()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var clock = new FixedClock();
            var service = Service(context, clock);
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;
            var secret = await service.StartTwoFactorSetupAsync(account);
            await service.EnableTwoFactorAsync(account, CodeAt(secret, Now));

            // The code used to switch it on can't be replayed to log in
            Assert.Equal(TwoFactorCheck.Wrong, await service.CheckTwoFactorAsync(account, CodeAt(secret, Now)));

            clock.At = Now.AddMinutes(1);
            Assert.Equal(TwoFactorCheck.AppCode, await service.CheckTwoFactorAsync(account, CodeAt(secret, clock.At)));
            Assert.Equal(TwoFactorCheck.Wrong, await service.CheckTwoFactorAsync(account, CodeAt(secret, clock.At)));
        }

        [Fact]
        public async Task Check_AcceptsEachRecoveryCodeOnce()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;
            var secret = await service.StartTwoFactorSetupAsync(account);
            var codes = (await service.EnableTwoFactorAsync(account, CodeAt(secret, Now)))!;

            Assert.Equal(TwoFactorCheck.RecoveryCode, await service.CheckTwoFactorAsync(account, codes[0]));
            Assert.Equal(TwoFactorCheck.Wrong, await service.CheckTwoFactorAsync(account, codes[0]));
            Assert.Equal(7, TwoFactor.RecoveryCodesLeft(account.Entity.TwoFactorRecoveryCodes));
        }

        [Fact]
        public async Task TurnOff_ForgetsTheSecretAndCodes()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;
            var secret = await service.StartTwoFactorSetupAsync(account);
            await service.EnableTwoFactorAsync(account, CodeAt(secret, Now));

            await service.TurnOffTwoFactorAsync(account);

            await using var check = _db.NewContext();
            var saved = await check.Lecturers.FindAsync(lecturer.LecturerId);
            Assert.False(saved!.TwoFactorEnabled);
            Assert.Null(saved.TwoFactorSecret);
            Assert.Null(saved.TwoFactorRecoveryCodes);
        }

        [Fact]
        public async Task ChangePassword_ChecksTheCurrentOneAndTheRules()
        {
            var (_, lecturer) = await SeedAsync();
            await using var context = _db.NewContext();
            var service = Service(context, new FixedClock());
            var account = (await service.FindAsync(Roles.Lecturer, lecturer.LecturerId))!;

            Assert.Equal("CurrentPassword", (await service.ChangePasswordAsync(account, "wrong", "LongEnough1"))?.Field);
            Assert.Equal("NewPassword", (await service.ChangePasswordAsync(account, "Password1!", "short"))?.Field);
            Assert.Equal("NewPassword", (await service.ChangePasswordAsync(account, "Password1!", "Password1!"))?.Field);
            Assert.Null(await service.ChangePasswordAsync(account, "Password1!", "LongEnough1"));
            Assert.NotNull(await service.SignInAsync("l@lecturers.test", "LongEnough1"));
        }

        public void Dispose() => _db.Dispose();
    }
}
