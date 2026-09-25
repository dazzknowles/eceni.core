using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Concrete
{
    internal sealed class RequiredSecretsValidator : IHostedService
    {
        private readonly IEceniSecrets _secrets;
        private readonly EceniSecretsOptions _options;

        public RequiredSecretsValidator(IEceniSecrets secrets, IOptions<EceniSecretsOptions> options)
        {
            _secrets = secrets;
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.ValidateOnStart || _options.Required == null)
            {
                return;
            }

            foreach (string secretName in _options.Required)
            {
                await _secrets.GetRequiredSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
