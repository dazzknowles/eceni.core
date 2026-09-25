using System;
using System.Collections.Generic;
using System.ComponentModel;
using Eceni.Core.Secrets.Abstract;
using Eceni.Core.Secrets.Configuration;
using Eceni.Core.Secrets.Exceptions;
using Microsoft.Extensions.Options;

namespace Eceni.Core.Secrets.Concrete
{
    internal sealed class EceniVariables : IEceniVariables
    {
        private readonly IReadOnlyCollection<IVariableProvider> _providers;
        private readonly EceniVariablesOptions _options;

        public EceniVariables(IEnumerable<IVariableProvider> providers,
                              IOptions<EceniVariablesOptions> options)
        {
            _providers = new List<IVariableProvider>(providers);
            _options = options.Value;
        }

        public string GetVariable(string name)
        {
            ValidateName(name);
            return ResolveProvider().GetVariable(name);
        }

        public string GetRequiredVariable(string name)
        {
            string value = GetVariable(name);

            if (value == null)
            {
                throw new VariableNotFoundException(name);
            }

            return value;
        }

        public T GetVariable<T>(string name)
        {
            ValidateName(name);
            string value = ResolveProvider().GetVariable(name);

            if (value == null)
            {
                return default;
            }

            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            TypeConverter converter = TypeDescriptor.GetConverter(targetType);
            object convertedValue = converter.ConvertFromInvariantString(value);

            return (T)convertedValue;
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A variable name is required.", nameof(name));
            }
        }

        private IVariableProvider ResolveProvider()
        {
            if (string.IsNullOrWhiteSpace(_options.Provider))
            {
                throw new SecretConfigurationException(
                    $"No variables provider is configured in '{EceniVariablesOptions.SectionName}:Provider'.");
            }

            IVariableProvider match = null;

            foreach (IVariableProvider provider in _providers)
            {
                if (!string.Equals(provider.ProviderName, _options.Provider, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new SecretConfigurationException(
                        $"More than one '{_options.Provider}' variables provider is registered.");
                }

                match = provider;
            }

            if (match == null)
            {
                throw new SecretConfigurationException(
                    $"Variables provider '{_options.Provider}' is configured but has not been registered.");
            }

            return match;
        }
    }
}
