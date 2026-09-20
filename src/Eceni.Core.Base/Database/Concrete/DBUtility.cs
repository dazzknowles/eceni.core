using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Eceni.Core.Base.Database.Concrete
{
    /// <summary>
    /// Static facade over the registered <see cref="IDBUtility"/> provider(s). Each provider registers the
    /// connection type(s) it owns, and every call that carries a connection is routed to the provider that owns
    /// that connection's concrete type. This lets more than one provider (for example MySQL and, later, MSSQL) be
    /// registered in the same process without one clobbering the other. The few helpers that do not carry a
    /// connection (connection creation, transaction scopes, unit-of-work creation, the parameter cache) use the
    /// default provider, which is the most recently registered one; in a single-provider process that is simply
    /// that sole provider.
    /// </summary>
    public static class DBUtility
    {
        private static readonly ConcurrentDictionary<Type, IDBUtility> _providersByConnectionType = new ConcurrentDictionary<Type, IDBUtility>();
        private static readonly ConcurrentDictionary<string, IDBUtility> _providersByKey = new ConcurrentDictionary<string, IDBUtility>(StringComparer.OrdinalIgnoreCase);
        private static volatile IDBUtility _default;

        /// <summary>
        /// Registers a database provider. The provider is indexed by each of its
        /// <see cref="IDBUtility.SupportedConnectionTypes"/> and also becomes the default provider for the
        /// connection-less helpers. Registering more than one provider is supported; each keeps serving its own
        /// connection types. Passing <c>null</c> clears the registry entirely, fully unregistering every provider.
        /// </summary>
        /// <param name="innerUtility">The provider to register, or <c>null</c> to reset the registry.</param>
        public static void RegisterDbUtility(IDBUtility innerUtility)
        {
            if (innerUtility == null)
            {
                _providersByConnectionType.Clear();
                _providersByKey.Clear();
                _default = null;
                return;
            }

            IReadOnlyCollection<Type> connectionTypes = innerUtility.SupportedConnectionTypes;
            if (connectionTypes != null)
            {
                foreach (Type connectionType in connectionTypes)
                {
                    if (connectionType != null)
                    {
                        _providersByConnectionType[connectionType] = innerUtility;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(innerUtility.ProviderKey))
            {
                _providersByKey[innerUtility.ProviderKey] = innerUtility;
            }

            _default = innerUtility;
        }

        /// <summary>
        /// Resolves the provider that owns <paramref name="connection"/>'s concrete type, throwing a clear,
        /// actionable exception when the connection is null or no provider handles its type.
        /// </summary>
        private static IDBUtility ForConnection(IDbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection), "A database connection is required so DBUtility can select the matching database provider.");
            }

            Type connectionType = connection.GetType();
            if (_providersByConnectionType.TryGetValue(connectionType, out IDBUtility provider))
            {
                return provider;
            }

            // Fall back to assignability so a derived or proxied connection type still resolves.
            foreach (KeyValuePair<Type, IDBUtility> entry in _providersByConnectionType)
            {
                if (entry.Key.IsInstanceOfType(connection))
                {
                    return entry.Value;
                }
            }

            throw new InvalidOperationException($"No database provider is registered for connection type '{connectionType.FullName}'. Call AddEceniCoreDatabaseMySQL(...) at application startup before using the database.");
        }

        /// <summary>
        /// The default provider for helpers that do not carry a connection. Throws a clear, actionable exception
        /// when no provider has been registered.
        /// </summary>
        private static IDBUtility Default
        {
            get
            {
                IDBUtility provider = _default;
                if (provider == null)
                {
                    throw new InvalidOperationException("No database provider has been registered with DBUtility. Call AddEceniCoreDatabaseMySQL(...) at application startup before using the database.");
                }

                return provider;
            }
        }

        /// <summary>
        /// Resolves the provider named by <paramref name="providerKey"/>, or the default provider when the key is
        /// null or empty. Throws a clear, actionable exception when a non-empty key names no registered provider.
        /// </summary>
        private static IDBUtility ProviderByKeyOrDefault(string providerKey)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return Default;
            }

            if (_providersByKey.TryGetValue(providerKey, out IDBUtility provider))
            {
                return provider;
            }

            throw new InvalidOperationException($"No database provider is registered under the key '{providerKey}'. Register it with AddEceniCoreDatabaseMySQL(...), or correct the Provider value in the database configuration.");
        }

        public static DbConnection CreateConnectionAsync(string connectionString, bool enableStatistics = false) => Default.CreateConnectionAsync(connectionString, enableStatistics);
        /// <summary>
        /// Creates a connection for the engine named by <paramref name="providerKey"/> (an <c>IDBUtility.ProviderKey</c>
        /// such as <c>"MySQL"</c>). A null or empty key uses the default provider, so a single-engine process
        /// behaves exactly as before. This is the overload that lets a mixed-engine process create the right
        /// connection type for a given connection string.
        /// </summary>
        public static DbConnection CreateConnectionAsync(string connectionString, string providerKey, bool enableStatistics = false) => ProviderByKeyOrDefault(providerKey).CreateConnectionAsync(connectionString, enableStatistics);
        public static TransactionScope CreateTransactionScopeAsync(TransactionScopeOption transactionScopeOption = TransactionScopeOption.Required, TransactionScopeAsyncFlowOption transactionScopeAsyncFlowOption = TransactionScopeAsyncFlowOption.Enabled) => Default.CreateTransactionScopeAsync(transactionScopeOption, transactionScopeAsyncFlowOption);

        /// <summary>
        /// Opens a unit of work against the default provider. See <see cref="IDBUtility.BeginUnitOfWorkAsync"/>.
        /// </summary>
        public static Task<IDbUnitOfWork> BeginUnitOfWorkAsync(string connectionString, CancellationToken cancellationToken = default) => Default.BeginUnitOfWorkAsync(connectionString, cancellationToken);
        /// <summary>
        /// Opens a unit of work against the engine named by <paramref name="providerKey"/>. A null or empty key
        /// uses the default provider.
        /// </summary>
        public static Task<IDbUnitOfWork> BeginUnitOfWorkAsync(string connectionString, string providerKey, CancellationToken cancellationToken = default) => ProviderByKeyOrDefault(providerKey).BeginUnitOfWorkAsync(connectionString, cancellationToken);

        public static void CacheParameters(string cacheKey, params DbParameter[] commandParameters) => Default.CacheParameters(cacheKey, commandParameters);
        public static DbParameter[] GetCachedParameters(string cacheKey) => Default.GetCachedParameters(cacheKey);
        public static Task<int> ExecuteNonQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).ExecuteNonQueryAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static Task<int> ExecuteNonQueryAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.ExecuteNonQueryAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static Task<DataSet> ExecuteMultiQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).ExecuteMultiQueryAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static Task<DataSet> ExecuteMultiQueryAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.ExecuteMultiQueryAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static IAsyncEnumerable<IDataRecord> ExecuteReaderAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).ExecuteReaderAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static IAsyncEnumerable<IDataRecord> ExecuteReaderAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.ExecuteReaderAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static IAsyncEnumerable<dynamic> ExecuteDynamicQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).ExecuteDynamicQueryAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static IAsyncEnumerable<dynamic> ExecuteDynamicQueryAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.ExecuteDynamicQueryAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static Task<object> ExecuteScalarAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).ExecuteScalarAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static Task<object> ExecuteScalarAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.ExecuteScalarAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static IAsyncEnumerable<int> ExecuteScriptAsync(DbConnection connection, string scriptText, int? commandTimeout = null) => ForConnection(connection).ExecuteScriptAsync(connection, scriptText, commandTimeout);
        public static DbCommand PrepareCommandAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null) => ForConnection(connection).PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout);
        public static DbCommand PrepareCommandAsync(DbConnection connection, CommandType commandType, string commandText, Parameter[] commandParameters, int? commandTimeout = null)
        {
            IDBUtility provider = ForConnection(connection);
            return provider.PrepareCommandAsync(connection, commandType, commandText, provider.ParseParameters(commandParameters), commandTimeout);
        }
        public static Task<Guid?> BatchInsertAsync<T>(DbConnection connection, IEnumerable<T> batchInsertables, string tableName, int batchSize, bool testRun = false) where T : class, IBatchInsertable => ForConnection(connection).BatchInsertAsync<T>(connection, batchInsertables, tableName, batchSize, testRun);
    }
}
