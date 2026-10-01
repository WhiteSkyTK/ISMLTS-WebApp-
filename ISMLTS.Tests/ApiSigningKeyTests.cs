using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class ApiSigningKeyTests
    {
        [Fact]
        public void WithoutAConfiguredKey_ARandomOneIsMade()
        {
            var first = ApiSigningKey.From(null);
            var second = ApiSigningKey.From("  ");

            Assert.True(first.IsGenerated);
            Assert.Equal(64, first.Key.Key.Length);
            Assert.NotEqual(first.Key.Key, second.Key.Key);
        }

        [Fact]
        public void AConfiguredKey_IsUsed_WhenItIsLongEnough()
        {
            var key = ApiSigningKey.From(new string((char)0x61, 40));

            Assert.False(key.IsGenerated);
            Assert.Equal(40, key.Key.Key.Length);
        }

        [Fact]
        public void AShortKey_StopsTheSiteFromStarting()
        {
            Assert.Throws<InvalidOperationException>(() => ApiSigningKey.From("too-short"));
        }

        [Fact]
        public void RefreshTokens_AreStoredAsHashes()
        {
            var hash = ApiTokenService.Hash("token-value");

            Assert.Equal(64, hash.Length);
            Assert.DoesNotContain("token-value", hash);
            Assert.Equal(hash, ApiTokenService.Hash("token-value"));
        }
    }
}
