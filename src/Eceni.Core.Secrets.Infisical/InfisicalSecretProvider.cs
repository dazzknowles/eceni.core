using System;
using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Exceptions;
using Eceni.Core.Secrets.Infisical.Configuration;
using Eceni.Core.Secrets.Models;
using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Infisical
{
    public sealed class InfisicalSecretProvider : ISecretProvider, IDisposable
    {
        public const string Name = "Infisical";

        private readonly InfisicalClient _client;
        private readonly InfisicalOptions _options;
        private readonly SemaphoreSlim _authenticationLock = new(1, 1);
        private bool _authenticated;

        public InfisicalSecretProvider(InfisicalClient client, IOptions<InfisicalOptions> options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options.Value;
        }

        public string ProviderName => Name;

        public async ValueTask<SecretValue> GetSecretAsync(string name,
                                                            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateConfiguration();
                await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                GetSecretOptions getOptions = new()
                {
                    SecretName = name,
                    EnvironmentSlug = _options.Environment,
                    SecretPath = _options.SecretPath,
                    ProjectId = _options.ProjectId,
                    ExpandSecretReferences = _options.ExpandSecretReferences
                };
                Secret secret = await _client.Secrets().GetAsync(getOptions).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                return secret == null ? null : new SecretValue(secret.SecretValue, secret.Version.ToString());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SecretConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (IsNotFound(exception))
                {
                    return null;
                }

                throw new SecretProviderException(Name, name, exception);
            }
        }

        public void Dispose()
        {
            _authenticationLock.Dispose();
        }

        private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
        {
            if (_authenticated)
            {
                return;
            }

            await _authenticationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_authenticated)
                {
                    return;
                }

                string clientId = Environment.GetEnvironmentVariable(_options.ClientIdEnvironmentVariable);
                string clientSecret = Environment.GetEnvironmentVariable(_options.ClientSecretEnvironmentVariable);

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    throw new SecretConfigurationException(
                        $"Infisical credentials must be supplied through the '{_options.ClientIdEnvironmentVariable}' " +
                        $"and '{_options.ClientSecretEnvironmentVariable}' environment variables.");
                }

                await _client.Auth().UniversalAuth().LoginAsync(clientId, clientSecret).ConfigureAwait(false);
                _authenticated = true;
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_options.ProjectId))
            {
                throw new SecretConfigurationException(
                    $"Infisical ProjectId is required in '{InfisicalOptions.SectionName}'.");
            }

            if (string.IsNullOrWhiteSpace(_options.Environment))
            {
                throw new SecretConfigurationException(
                    $"Infisical Environment is required in '{InfisicalOptions.SectionName}'.");
            }

            if (string.IsNullOrWhiteSpace(_options.ClientIdEnvironmentVariable) ||
                string.IsNullOrWhiteSpace(_options.ClientSecretEnvironmentVariable))
            {
                throw new SecretConfigurationException(
                    "Infisical credential environment-variable names cannot be empty.");
            }
        }

        private static bool IsNotFound(Exception exception)
        {
            Exception currentException = exception;

            while (currentException != null)
            {
                if (currentException.Message != null &&
                    (currentException.Message.Contains("secret not found", StringComparison.OrdinalIgnoreCase) ||
                     currentException.Message.Contains("secret does not exist", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                currentException = currentException.InnerException;
            }

            return false;
        }
    }
}
