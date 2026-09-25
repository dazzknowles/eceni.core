using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Concrete
{
    public sealed class ConfigurationVariableProvider : IVariableProvider
    {
        public const string Name = "Configuration";

        private readonly IConfiguration _configuration;
        private readonly EceniVariablesOptions _options;

        public ConfigurationVariableProvider(IConfiguration configuration,
                                             IOptions<EceniVariablesOptions> options)
        {
            _configuration = configuration;
            _options = options.Value;
        }

        public string ProviderName => Name;

        public string GetVariable(string name)
        {
            return _configuration.GetSection(_options.Section)[name];
        }
    }
}
