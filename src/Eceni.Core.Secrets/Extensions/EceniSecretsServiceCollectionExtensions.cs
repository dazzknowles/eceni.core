using System;
using System.Collections.Generic;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Concrete;
using Eceni.Core.Secrets.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eceni.Core.Secrets.Extensions
{
    public static class EceniSecretsServiceCollectionExtensions
    {
        public static EceniSecretsBuilder AddEceniSecrets(this IServiceCollection services,
                                                           IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<EceniSecretsOptions>()
                    .Bind(configuration.GetSection(EceniSecretsOptions.SectionName));
            services.AddOptions<EceniVariablesOptions>()
                    .Bind(configuration.GetSection(EceniVariablesOptions.SectionName));
            services.AddMemoryCache();
            services.TryAddSingleton(configuration);
            services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.TryAddSingleton<IEceniSecrets, Concrete.EceniSecrets>();
            services.TryAddSingleton<IEceniVariables, EceniVariables>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IVariableProvider, ConfigurationVariableProvider>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, RequiredSecretsValidator>());

            return new EceniSecretsBuilder(services, configuration);
        }

        public static EceniSecretsBuilder AddInMemorySecretProvider(
            this EceniSecretsBuilder builder,
            IReadOnlyDictionary<string, string> secrets)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(secrets);

            builder.Services.AddSingleton<ISecretProvider>(new InMemorySecretProvider(secrets));
            return builder;
        }
    }
}
