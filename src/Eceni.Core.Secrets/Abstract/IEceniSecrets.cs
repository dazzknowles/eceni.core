using System.Threading;
using System.Threading.Tasks;

namespace Eceni.Core.Secrets.Abstract
{
    /// <summary>
    /// Retrieves secrets by their provider-independent logical names.
    /// </summary>
    public interface IEceniSecrets
    {
        /// <summary>
        /// Gets a secret, returning null when the configured provider does not contain it.
        /// </summary>
        ValueTask<string> GetSecretAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a secret or throws when the configured provider does not contain it.
        /// </summary>
        ValueTask<string> GetRequiredSecretAsync(string name, CancellationToken cancellationToken = default);
    }
}
