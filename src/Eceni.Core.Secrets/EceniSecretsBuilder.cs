using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eceni.Core.Secrets
{
    /// <summary>
    /// Carries the application services and configuration through provider registration calls.
    /// </summary>
    public sealed class EceniSecretsBuilder
    {
        internal EceniSecretsBuilder(IServiceCollection services, IConfiguration configuration)
        {
            Services = services;
            Configuration = configuration;
        }

        public IServiceCollection Services { get; }
        public IConfiguration Configuration { get; }
    }
}
