using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Exceptions;
using Eceni.Core.Secrets.Extensions;
using Eceni.Core.Secrets.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eceni.Core.UnitTest.Secrets
{
    [TestClass]
    public class EceniSecretsTests
    {
        [TestMethod]
        public async Task GetRequiredSecretAsync_UsesConfiguredPrefixAndMapping()
        {
            Dictionary<string, string> configurationValues = new()
            {
                ["Eceni:Secrets:Provider"] = "InMemory",
                ["Eceni:Secrets:Prefix"] = "eceni/dev/",
                ["Eceni:Secrets:Mappings:PAYMENTS_KEY"] = "shared/payments",
                ["Variables:Retries"] = "3"
            };
            IConfiguration configuration = CreateConfiguration(configurationValues);
            ServiceCollection services = new();
            Dictionary<string, string> secrets = new()
            {
                ["eceni/dev/DATABASE_PASSWORD"] = "database-password",
                ["shared/payments"] = "payments-key"
            };

            services.AddEceniSecrets(configuration).AddInMemorySecretProvider(secrets);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();

            string prefixedValue = await accessor.GetRequiredSecretAsync("DATABASE_PASSWORD");
            string mappedValue = await accessor.GetRequiredSecretAsync("PAYMENTS_KEY");

            Assert.AreEqual("database-password", prefixedValue);
            Assert.AreEqual("payments-key", mappedValue);
        }

        [TestMethod]
        public async Task GetRequiredSecretAsync_ThrowsSpecificExceptionWhenMissing()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = "InMemory"
            });
            ServiceCollection services = new();
            services.AddEceniSecrets(configuration)
                    .AddInMemorySecretProvider(new Dictionary<string, string>());
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();

            SecretNotFoundException exception = await Assert.ThrowsAsync<SecretNotFoundException>(
                async () => await accessor.GetRequiredSecretAsync("MISSING"));

            Assert.AreEqual("MISSING", exception.SecretName);
        }

        [TestMethod]
        public async Task GetSecretAsync_CachesValuesAndCoalescesConcurrentRequests()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = CountingSecretProvider.Name,
                ["Eceni:Secrets:CacheDuration"] = "00:05:00"
            });
            ServiceCollection services = new();
            CountingSecretProvider provider = new("value", TimeSpan.FromMilliseconds(25));
            services.AddEceniSecrets(configuration);
            services.AddSingleton<ISecretProvider>(provider);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();

            Task<string>[] requests = new Task<string>[8];

            for (int index = 0; index < requests.Length; index++)
            {
                requests[index] = accessor.GetRequiredSecretAsync("API_KEY").AsTask();
            }

            string[] values = await Task.WhenAll(requests);
            string cachedValue = await accessor.GetRequiredSecretAsync("API_KEY");

            CollectionAssert.AreEqual(
                new[] { "value", "value", "value", "value", "value", "value", "value", "value" },
                values);
            Assert.AreEqual("value", cachedValue);
            Assert.AreEqual(1, provider.RequestCount);
        }

        [TestMethod]
        public async Task GetSecretAsync_DoesNotCacheProviderFailures()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = CountingSecretProvider.Name
            });
            ServiceCollection services = new();
            CountingSecretProvider provider = new("value", TimeSpan.Zero)
            {
                FailNextRequest = true
            };
            services.AddEceniSecrets(configuration);
            services.AddSingleton<ISecretProvider>(provider);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await accessor.GetRequiredSecretAsync("API_KEY"));
            string value = await accessor.GetRequiredSecretAsync("API_KEY");

            Assert.AreEqual("value", value);
            Assert.AreEqual(2, provider.RequestCount);
        }

        [TestMethod]
        public async Task GetSecretAsync_CancelledWaiterDoesNotCancelOrDuplicateSharedFetch()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = CountingSecretProvider.Name
            });
            ServiceCollection services = new();
            CountingSecretProvider provider = new("value", TimeSpan.FromMilliseconds(100));
            services.AddEceniSecrets(configuration);
            services.AddSingleton<ISecretProvider>(provider);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();
            using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(10));

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await accessor.GetRequiredSecretAsync("API_KEY", cancellation.Token));
            string value = await accessor.GetRequiredSecretAsync("API_KEY");

            Assert.AreEqual("value", value);
            Assert.AreEqual(1, provider.RequestCount);
        }

        [TestMethod]
        public async Task GetSecretAsync_RejectsConfiguredButUnregisteredProvider()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = "FutureProvider"
            });
            ServiceCollection services = new();
            services.AddEceniSecrets(configuration);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniSecrets accessor = serviceProvider.GetRequiredService<IEceniSecrets>();

            SecretConfigurationException exception = await Assert.ThrowsAsync<SecretConfigurationException>(
                async () => await accessor.GetSecretAsync("API_KEY"));

            StringAssert.Contains(exception.Message, "has not been registered");
        }

        [TestMethod]
        public void Variables_ReadStringsAndConvertedValuesFromConfiguredSection()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Variables:Section"] = "ApplicationVariables",
                ["ApplicationVariables:DisplayName"] = "Eceni",
                ["ApplicationVariables:Retries"] = "3"
            });
            ServiceCollection services = new();
            services.AddEceniSecrets(configuration);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniVariables variables = serviceProvider.GetRequiredService<IEceniVariables>();

            Assert.AreEqual("Eceni", variables.GetRequiredVariable("DisplayName"));
            Assert.AreEqual(3, variables.GetVariable<int>("Retries"));
            Assert.IsNull(variables.GetVariable("Missing"));
            Assert.ThrowsExactly<VariableNotFoundException>(
                () => variables.GetRequiredVariable("Missing"));
        }

        [TestMethod]
        public void Variables_SelectARegisteredProviderFromConfiguration()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Variables:Provider"] = TestVariableProvider.Name
            });
            ServiceCollection services = new();
            services.AddEceniSecrets(configuration);
            services.AddSingleton<IVariableProvider>(new TestVariableProvider());
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEceniVariables variables = serviceProvider.GetRequiredService<IEceniVariables>();

            Assert.AreEqual("from-test-provider", variables.GetRequiredVariable("DisplayName"));
        }

        [TestMethod]
        public async Task StartupValidation_FailsForAMissingRequiredSecret()
        {
            IConfiguration configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["Eceni:Secrets:Provider"] = "InMemory",
                ["Eceni:Secrets:ValidateOnStart"] = "true",
                ["Eceni:Secrets:Required:0"] = "DATABASE_PASSWORD"
            });
            ServiceCollection services = new();
            services.AddEceniSecrets(configuration)
                    .AddInMemorySecretProvider(new Dictionary<string, string>());
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IEnumerable<IHostedService> hostedServices = serviceProvider.GetServices<IHostedService>();

            foreach (IHostedService hostedService in hostedServices)
            {
                await Assert.ThrowsAsync<SecretNotFoundException>(
                    () => hostedService.StartAsync(CancellationToken.None));
            }
        }

        [TestMethod]
        public void SecretValue_ToStringDoesNotExposeValue()
        {
            SecretValue secret = new("do-not-log-me", "1");

            Assert.AreEqual("[REDACTED]", secret.ToString());
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private sealed class CountingSecretProvider : ISecretProvider
        {
            public const string Name = "Counting";

            private readonly string _value;
            private readonly TimeSpan _delay;
            private int _requestCount;

            public CountingSecretProvider(string value, TimeSpan delay)
            {
                _value = value;
                _delay = delay;
            }

            public string ProviderName => Name;
            public int RequestCount => _requestCount;
            public bool FailNextRequest { get; set; }

            public async ValueTask<SecretValue> GetSecretAsync(
                string name,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _requestCount);

                if (_delay > TimeSpan.Zero)
                {
                    await Task.Delay(_delay, cancellationToken);
                }

                if (FailNextRequest)
                {
                    FailNextRequest = false;
                    throw new InvalidOperationException("Simulated provider failure.");
                }

                return new SecretValue(_value);
            }
        }

        private sealed class TestVariableProvider : IVariableProvider
        {
            public const string Name = "TestVariables";

            public string ProviderName => Name;

            public string GetVariable(string name)
            {
                return name == "DisplayName" ? "from-test-provider" : null;
            }
        }
    }
}
