using System;
using Amazon;
using Amazon.SecretsManager;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Aws.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Aws.Extensions
{
    public static class AwsSecretsManagerBuilderExtensions
    {
        public static EceniSecretsBuilder AddAwsSecretsManagerProvider(this EceniSecretsBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddOptions<AwsSecretsManagerOptions>()
                   .Bind(builder.Configuration.GetSection(AwsSecretsManagerOptions.SectionName));
            builder.Services.TryAddSingleton<IAmazonSecretsManager>(serviceProvider =>
            {
                AwsSecretsManagerOptions options = serviceProvider
                    .GetRequiredService<IOptions<AwsSecretsManagerOptions>>()
                    .Value;

                if (string.IsNullOrWhiteSpace(options.Region))
                {
                    return new AmazonSecretsManagerClient();
                }

                RegionEndpoint region = RegionEndpoint.GetBySystemName(options.Region);
                return new AmazonSecretsManagerClient(region);
            });
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretProvider, AwsSecretsManagerProvider>());

            return builder;
        }
    }
}
