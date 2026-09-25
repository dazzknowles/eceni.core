using System;

namespace Eceni.Core.Secrets.Exceptions
{
    public class SecretNotFoundException : Exception
    {
        public SecretNotFoundException(string secretName)
            : base($"The required secret '{secretName}' was not found.")
        {
            SecretName = secretName;
        }

        public string SecretName { get; }
    }
}
