using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Models;

namespace Eceni.Core.Secrets.Abstract
{
    /// <summary>
    /// Implemented by packages that retrieve secrets from a particular backing service.
    /// </summary>
    public interface ISecretProvider
    {
        string ProviderName { get; }

        ValueTask<SecretValue> GetSecretAsync(string name, CancellationToken cancellationToken = default);
    }
}
