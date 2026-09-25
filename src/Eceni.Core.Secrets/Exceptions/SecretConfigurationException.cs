using System;

namespace Eceni.Core.Secrets.Exceptions
{
    public class SecretConfigurationException : Exception
    {
        public SecretConfigurationException(string message)
            : base(message)
        {
        }

        public SecretConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
