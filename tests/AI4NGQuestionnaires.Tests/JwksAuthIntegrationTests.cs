using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AI4NGQuestionnaires.Tests
{
    public class JwksAuthIntegrationTests
    {
        [Fact]
        public void ShouldLoadJwksFile_AndContainKeys()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "jwks.json");
            Assert.True(File.Exists(path));

            var json = File.ReadAllText(path);
            var jwks = new JsonWebKeySet(json);

            Assert.NotEmpty(jwks.Keys);
            Assert.All(jwks.Keys, key => Assert.Equal("RSA", key.Kty));
        }
    }
}
