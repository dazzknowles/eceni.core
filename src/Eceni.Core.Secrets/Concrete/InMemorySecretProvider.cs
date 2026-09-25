using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Models;

namespace Eceni.Core.Secrets.Concrete
{
    /// <summary>
    /// A simple provider intended for automated tests and local fixtures.
    /// </summary>
    public sealed class InMemorySecretProvider : ISecretProvider
    {
        public const string Name = "InMemory";

        private readonly IReadOnlyDictionary<string, string> _secrets;

        public InMemorySecretProvider(IReadOnlyDictionary<string, string> secrets)
        {
            _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        }

        public string ProviderName => Name;

        public ValueTask<SecretValue> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_secrets.TryGetValue(name, out string value))
            {
                return ValueTask.FromResult<SecretValue>(null);
            }

            return ValueTask.FromResult(new SecretValue(value));
        }
    }
}
