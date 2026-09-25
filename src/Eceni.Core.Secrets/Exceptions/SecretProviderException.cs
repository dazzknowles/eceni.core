using System;

namespace Eceni.Core.Secrets.Exceptions
{
    public class SecretProviderException : Exception
    {
        public SecretProviderException(string providerName, string secretName, Exception innerException)
            : base($"The '{providerName}' provider could not retrieve secret '{secretName}'.", innerException)
        {
            ProviderName = providerName;
            SecretName = secretName;
        }

        public string ProviderName { get; }
        public string SecretName { get; }
    }
}
