using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Exceptions;
using Eceni.Core.Secrets.Models;

namespace Eceni.Core.Secrets.Aws
{
    public sealed class AwsSecretsManagerProvider : ISecretProvider
    {
        public const string Name = "AwsSecretsManager";

        private readonly IAmazonSecretsManager _client;

        public AwsSecretsManagerProvider(IAmazonSecretsManager client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string ProviderName => Name;

        public async ValueTask<SecretValue> GetSecretAsync(string name,
                                                            CancellationToken cancellationToken = default)
        {
            try
            {
                GetSecretValueRequest request = new()
                {
                    SecretId = name
                };
                GetSecretValueResponse response = await _client.GetSecretValueAsync(request, cancellationToken)
                                                               .ConfigureAwait(false);

                if (response.SecretString != null)
                {
                    return new SecretValue(response.SecretString, response.VersionId);
                }

                if (response.SecretBinary != null)
                {
                    using MemoryStream binarySecret = response.SecretBinary;
                    return new SecretValue(Convert.ToBase64String(binarySecret.ToArray()), response.VersionId);
                }

                return null;
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SecretProviderException(Name, name, exception);
            }
        }
    }
}
