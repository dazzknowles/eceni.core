using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Eceni.Core.Secrets.Aws;
using Eceni.Core.Secrets.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Eceni.Core.UnitTest.Secrets
{
    [TestClass]
    public class AwsSecretsManagerProviderTests
    {
        [TestMethod]
        public async Task GetSecretAsync_ReturnsStringValueAndVersion()
        {
            IAmazonSecretsManager client = Substitute.For<IAmazonSecretsManager>();
            client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(new GetSecretValueResponse
                  {
                      SecretString = "secret-value",
                      VersionId = "00000000-0000-0000-0000-000000000001"
                  }));
            AwsSecretsManagerProvider provider = new(client);

            SecretValue result = await provider.GetSecretAsync("eceni/production/API_KEY");

            Assert.AreEqual("secret-value", result.Value);
            Assert.AreEqual("00000000-0000-0000-0000-000000000001", result.Version);
            await client.Received(1).GetSecretValueAsync(
                Arg.Is<GetSecretValueRequest>(request => request.SecretId == "eceni/production/API_KEY"),
                Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task GetSecretAsync_ReturnsNullWhenAwsReportsMissingSecret()
        {
            IAmazonSecretsManager client = Substitute.For<IAmazonSecretsManager>();
            client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
                  .Returns<Task<GetSecretValueResponse>>(
                      _ => throw new ResourceNotFoundException("Secret not found."));
            AwsSecretsManagerProvider provider = new(client);

            SecretValue result = await provider.GetSecretAsync("missing");

            Assert.IsNull(result);
        }
    }
}
