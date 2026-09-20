using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Eceni.Core.Base.Configuration;
using Eceni.Core.Base.Database.Abstract;
using System;
using System.Linq;

namespace Eceni.Core.Base.Database.Concrete
{
    /// <summary>
    /// Resolves the appropriate database connection string based on a specified key.
    /// This class supports retrieving connection strings for different types of database operations,
    /// such as read-only and read/write, from a configurable list of connection strings.
    /// </summary>
    public class DBConnectionResolver : IDBConnectionResolver
    {
        protected readonly DatabaseConfig _databaseConfig;
        protected readonly ILogger<DBConnectionResolver> _logger;

        /// <summary>
        /// Initializes a new instance of the DBConnectionResolver class with the database configuration.
        /// </summary>
        /// <param name="databaseConfig">The database configuration containing the connection strings.</param>
        public DBConnectionResolver(IOptions<DatabaseConfig> databaseConfig,
                                    ILogger<DBConnectionResolver> logger = null)
        {
            _databaseConfig = databaseConfig.Value;
            _logger = logger;
        }

        /// <summary>
        /// Resolves the database connection string based on the provided key.
        /// If multiple connection strings are associated with the same key, a random one is selected.
        /// Falls back to the default connection string if no specific match is found.
        /// </summary>
        /// <param name="key">The key to identify the type of database operation (e.g., "ReadOnly" or "ReadWrite").</param>
        /// <returns>The resolved database connection string.</returns>
        public string ResolveConnectionString(string key = null)
        {
            try
            {
                _logger?.LogTrace("Attempting to resolve connection string from configuration...");

                if (string.IsNullOrWhiteSpace(_databaseConfig.ConnectionString) && (_databaseConfig.ConnectionStrings == null || _databaseConfig.ConnectionStrings.Count == 0))
                {
                    throw new ArgumentException("No connection strings provided.");
                }

                if (_databaseConfig.ConnectionStrings == null || !_databaseConfig.ConnectionStrings.Any())
                {
                    _logger?.LogTrace("Using single Database.ConnectionString value");

                    return _databaseConfig.ConnectionString;
                }

                if (_databaseConfig.ConnectionStrings.Count == 1)
                {
                    _logger?.LogTrace("Using only Database.ConnectionStrings value");

                    return _databaseConfig.ConnectionStrings.First().ConnectionString;
                }

                ConnectionStringItem[] matchingConnections = _databaseConfig.ConnectionStrings.Where(c => c.Key == key).ToArray();

                // Check for scenarios where a key is passed, but is not one of the expected keys
                if (!matchingConnections.Any())
                {
                    _logger?.LogTrace("Key not found - using single Database.ConnectionString config property");

                    // Fallback to the default connection string if no matching connections are found
                    return _databaseConfig.ConnectionString;
                }

                _logger?.LogTrace("Key found - using matching Database.ConnectionStrings config property");

                // Randomly select one connection string if multiple matches are found; otherwise, return the single match
                // Random.Shared is thread-safe, so replica balancing is not skewed by concurrent callers sharing one Random.
                return matchingConnections.Length > 1
                    ? matchingConnections[Random.Shared.Next(matchingConnections.Length)].ConnectionString
                    : matchingConnections.First().ConnectionString;
            }
            catch (Exception ex)
            {
                // Log at this layer, then let genuine configuration/validation errors propagate rather than
                // converting them to a null connection string that fails opaquely at connection-open time.
                _logger?.LogError(ex, "Error resolving connection string from configuration");

                throw;
            }
        }

        /// <summary>
        /// Resolves the provider key that owns the connection string chosen for <paramref name="key"/>, mirroring
        /// the selection in <see cref="ResolveConnectionString"/>: the single <c>ConnectionString</c> and the
        /// key-not-found fallback use <see cref="DatabaseConfig.Provider"/>, while a matched
        /// <see cref="ConnectionStringItem"/> uses its own <c>Provider</c> and falls back to the config default.
        /// Connection strings sharing a key address the same engine, so the first match's provider is authoritative.
        /// </summary>
        /// <param name="key">The key identifying the type of database operation, or null for the default.</param>
        /// <returns>The resolved provider key, or null to use the default registered provider.</returns>
        public string ResolveProvider(string key = null)
        {
            try
            {
                if (_databaseConfig.ConnectionStrings == null || !_databaseConfig.ConnectionStrings.Any())
                {
                    return _databaseConfig.Provider;
                }

                if (_databaseConfig.ConnectionStrings.Count == 1)
                {
                    return _databaseConfig.ConnectionStrings.First().Provider ?? _databaseConfig.Provider;
                }

                ConnectionStringItem[] matchingConnections = _databaseConfig.ConnectionStrings.Where(c => c.Key == key).ToArray();

                if (!matchingConnections.Any())
                {
                    return _databaseConfig.Provider;
                }

                return matchingConnections.First().Provider ?? _databaseConfig.Provider;
            }
            catch (Exception ex)
            {
                // Log at this layer, then let genuine configuration/validation errors propagate rather than
                // converting them to a null provider that fails opaquely at connection-open time.
                _logger?.LogError(ex, "Error resolving database provider from configuration");

                throw;
            }
        }
    }
}
