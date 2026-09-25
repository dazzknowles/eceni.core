using System;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Infisical.Configuration;
using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Infisical.Extensions
{
    public static class InfisicalBuilderExtensions
    {
        public static EceniSecretsBuilder AddInfisicalProvider(this EceniSecretsBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddOptions<InfisicalOptions>()
                   .Bind(builder.Configuration.GetSection(InfisicalOptions.SectionName));
            builder.Services.TryAddSingleton<InfisicalClient>(serviceProvider =>
            {
                InfisicalOptions options = serviceProvider.GetRequiredService<IOptions<InfisicalOptions>>().Value;
                InfisicalSdkSettingsBuilder settingsBuilder = new();

                if (!string.IsNullOrWhiteSpace(options.Host))
                {
                    settingsBuilder.WithHostUri(options.Host);
                }

                return new InfisicalClient(settingsBuilder.Build());
            });
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretProvider, InfisicalSecretProvider>());

            return builder;
        }
    }
}
