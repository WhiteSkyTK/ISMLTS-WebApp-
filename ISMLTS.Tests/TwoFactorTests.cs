using ISMLTS_WebApp_.Services;
using OtpNet;

namespace ISMLTS.Tests
{
    public class TwoFactorTests
    {
        private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        private static string CodeAt(string secret, DateTime time) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(time);

        [Fact]
        public void NewSecret_Is160RandomBitsInBase32()
        {
            var first = TwoFactor.NewSecret();

            Assert.Equal(32, first.Length);
            Assert.Equal(20, Base32Encoding.ToBytes(first).Length);
            Assert.NotEqual(first, TwoFactor.NewSecret());
        }

        [Fact]
        public void Verify_AcceptsTheCurrentCode_AndOneStepEitherSide()
        {
            var secret = TwoFactor.NewSecret();

            Assert.NotNull(TwoFactor.Verify(secret, CodeAt(secret, Now), 0, Now));
            Assert.NotNull(TwoFactor.Verify(secret, CodeAt(secret, Now.AddSeconds(-30)), 0, Now));
            Assert.NotNull(TwoFactor.Verify(secret, CodeAt(secret, Now.AddSeconds(30)), 0, Now));
            Assert.Null(TwoFactor.Verify(secret, CodeAt(secret, Now.AddMinutes(-2)), 0, Now));
        }

        [Fact]
        public void Verify_RejectsACodeWhoseStepWasAlreadyUsed()
        {
            var secret = TwoFactor.NewSecret();
            var code = CodeAt(secret, Now);

            var step = TwoFactor.Verify(secret, code, 0, Now);

            Assert.NotNull(step);
            Assert.Null(TwoFactor.Verify(secret, code, step.Value, Now));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("12345")]
        [InlineData("1234567")]
        [InlineData("12a456")]
        public void Verify_RejectsAnythingThatIsNotSixDigits(string? code)
        {
            Assert.Null(TwoFactor.Verify(TwoFactor.NewSecret(), code, 0, Now));
        }

        [Fact]
        public void Verify_IgnoresSpacesInTheCode()
        {
            var secret = TwoFactor.NewSecret();
            var code = CodeAt(secret, Now);

            Assert.NotNull(TwoFactor.Verify(secret, $"{code[..3]} {code[3..]}", 0, Now));
        }

        [Fact]
        public void OtpAuthUri_NamesTheSiteAndAccount()
        {
            var uri = TwoFactor.OtpAuthUri("ABCDEFGHIJKLMNOP", "st1@rcconnect.edu.za");

            Assert.Equal("otpauth://totp/ISMLTS:st1%40rcconnect.edu.za?secret=ABCDEFGHIJKLMNOP&issuer=ISMLTS&digits=6&period=30", uri);
        }

        [Fact]
        public void Grouped_SplitsTheKeyIntoFours()
        {
            Assert.Equal("ABCD EFGH IJ", TwoFactor.Grouped("ABCDEFGHIJ"));
        }

        [Fact]
        public void RecoveryCodes_AreEightDistinctCodes()
        {
            var codes = TwoFactor.NewRecoveryCodes();

            Assert.Equal(8, codes.Count);
            Assert.Equal(8, codes.Distinct().Count());
            Assert.All(codes, c => Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", c));
        }

        [Fact]
        public void RecoveryCode_WorksOnce_InAnyCaseWithOrWithoutTheDash()
        {
            var codes = TwoFactor.NewRecoveryCodes();
            var stored = TwoFactor.HashRecoveryCodes(codes);

            var remaining = TwoFactor.UseRecoveryCode(stored, codes[3].Replace("-", "").ToLowerInvariant());

            Assert.NotNull(remaining);
            Assert.Equal(7, TwoFactor.RecoveryCodesLeft(remaining));
            Assert.Null(TwoFactor.UseRecoveryCode(remaining, codes[3]));
            Assert.NotNull(TwoFactor.UseRecoveryCode(remaining, codes[4]));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ZZZZ-ZZZZ")]
        public void RecoveryCode_ThatIsNotStored_IsRejected(string? code)
        {
            var stored = TwoFactor.HashRecoveryCodes(TwoFactor.NewRecoveryCodes());

            Assert.Null(TwoFactor.UseRecoveryCode(stored, code));
            Assert.Null(TwoFactor.UseRecoveryCode(null, "ABCD-EFGH"));
        }

        [Fact]
        public void RecoveryCodes_AreStoredAsHashesOnly()
        {
            var codes = TwoFactor.NewRecoveryCodes();

            var stored = TwoFactor.HashRecoveryCodes(codes);

            Assert.All(codes, c => Assert.DoesNotContain(c.Replace("-", ""), stored));
            Assert.True(stored.Length <= 600);
        }
    }
}
