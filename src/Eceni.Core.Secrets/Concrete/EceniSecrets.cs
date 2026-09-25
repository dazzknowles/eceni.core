using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Configuration;
using Eceni.Core.Secrets.Exceptions;
using Eceni.Core.Secrets.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Concrete
{
    internal sealed class EceniSecrets : IEceniSecrets
    {
        private readonly IReadOnlyCollection<ISecretProvider> _providers;
        private readonly EceniSecretsOptions _options;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EceniSecrets> _logger;
        private readonly ConcurrentDictionary<string, Lazy<Task<SecretValue>>> _inFlight = new();

        public EceniSecrets(IEnumerable<ISecretProvider> providers,
                            IOptions<EceniSecretsOptions> options,
                            IMemoryCache cache,
                            ILogger<EceniSecrets> logger)
        {
            _providers = new List<ISecretProvider>(providers);
            _options = options.Value;
            _cache = cache;
            _logger = logger;
        }

        public async ValueTask<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A secret name is required.", nameof(name));
            }

            ISecretProvider provider = ResolveProvider();
            string providerSecretName = ResolveSecretName(name);
            string cacheKey = $"EceniSecrets:{provider.ProviderName}:{providerSecretName}";

            if (_cache.TryGetValue(cacheKey, out SecretCacheEntry cachedEntry))
            {
                return cachedEntry.Value?.Value;
            }

            Lazy<Task<SecretValue>> lazyFetch = _inFlight.GetOrAdd(
                cacheKey,
                _ => CreateFetch(provider, providerSecretName, cacheKey));
            Task<SecretValue> fetchTask = lazyFetch.Value;
            try
            {
                SecretValue secret = await fetchTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                return secret?.Value;
            }
            catch
            {
                if (fetchTask.IsCompleted)
                {
                    _inFlight.TryRemove(
                        new KeyValuePair<string, Lazy<Task<SecretValue>>>(cacheKey, lazyFetch));
                }

                throw;
            }
        }

        public async ValueTask<string> GetRequiredSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            string value = await GetSecretAsync(name, cancellationToken).ConfigureAwait(false);

            if (value == null)
            {
                throw new SecretNotFoundException(name);
            }

            return value;
        }

        private async Task<SecretValue> FetchAndCacheAsync(ISecretProvider provider,
                                                            string providerSecretName,
                                                            string cacheKey)
        {
            _logger.LogDebug("Retrieving secret {SecretName} from provider {ProviderName}",
                             providerSecretName,
                             provider.ProviderName);

            SecretValue secret = await provider.GetSecretAsync(providerSecretName, CancellationToken.None)
                                               .ConfigureAwait(false);
            TimeSpan cacheDuration = secret == null
                ? _options.MissingCacheDuration
                : _options.CacheDuration;

            if (cacheDuration > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, new SecretCacheEntry(secret), cacheDuration);
            }

            return secret;
        }

        private Lazy<Task<SecretValue>> CreateFetch(ISecretProvider provider,
                                                    string providerSecretName,
                                                    string cacheKey)
        {
            Lazy<Task<SecretValue>> lazyFetch = null;
            lazyFetch = new Lazy<Task<SecretValue>>(
                () =>
                {
                    Task<SecretValue> fetchTask = FetchAndCacheAsync(provider, providerSecretName, cacheKey);
                    fetchTask.ContinueWith(
                        _ => _inFlight.TryRemove(
                            new KeyValuePair<string, Lazy<Task<SecretValue>>>(cacheKey, lazyFetch)),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    return fetchTask;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            return lazyFetch;
        }

        private ISecretProvider ResolveProvider()
        {
            if (string.IsNullOrWhiteSpace(_options.Provider))
            {
                throw new SecretConfigurationException(
                    $"No secret provider is configured in '{EceniSecretsOptions.SectionName}:Provider'.");
            }

            ISecretProvider match = null;

            foreach (ISecretProvider provider in _providers)
            {
                if (!string.Equals(provider.ProviderName, _options.Provider, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new SecretConfigurationException(
                        $"More than one '{_options.Provider}' secret provider is registered.");
                }

                match = provider;
            }

            if (match == null)
            {
                throw new SecretConfigurationException(
                    $"Secret provider '{_options.Provider}' is configured but has not been registered.");
            }

            return match;
        }

        private string ResolveSecretName(string logicalName)
        {
            if (_options.Mappings != null && _options.Mappings.TryGetValue(logicalName, out string mappedName))
            {
                if (string.IsNullOrWhiteSpace(mappedName))
                {
                    throw new SecretConfigurationException(
                        $"The secret mapping for '{logicalName}' cannot be empty.");
                }

                return mappedName;
            }

            return $"{_options.Prefix}{logicalName}";
        }

        private sealed class SecretCacheEntry
        {
            public SecretCacheEntry(SecretValue value)
            {
                Value = value;
            }

            public SecretValue Value { get; }
        }
    }
}
