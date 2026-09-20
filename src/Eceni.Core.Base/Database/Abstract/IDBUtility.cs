using Eceni.Core.Base.Database.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Eceni.Core.Base.Database.Abstract
{
    public interface IDBUtility
    {
        /// <summary>
        /// Creates a private instance of DbConnection (used when calling the database asynchronously)
        /// </summary>
        /// <returns></returns>
        DbConnection CreateConnectionAsync(string connectionString, bool enableStatistics = false);

        /// <summary>
        /// Prevents multiple connections within the same transaction
        /// </summary>
        /// <param name="transactionScopeOption">The scope option for the transaction - defaults to "Required" (uses ambient transaction if available)</param>
        /// <returns></returns>
        TransactionScope CreateTransactionScopeAsync(TransactionScopeOption transactionScopeOption = TransactionScopeOption.Required, TransactionScopeAsyncFlowOption transactionScopeAsyncFlowOption = TransactionScopeAsyncFlowOption.Enabled);

        /// <summary>
        /// Opens a new connection for <paramref name="connectionString"/>, begins a plain local transaction on it,
        /// and returns the unit of work wrapping both. Deliberately not <c>System.Transactions</c>/<c>TransactionScope</c>
        /// — see <see cref="IDbUnitOfWork"/>. Disposing the returned unit of work without calling
        /// <see cref="IDbUnitOfWork.CommitAsync"/> rolls the transaction back and closes the connection.
        /// </summary>
        Task<IDbUnitOfWork> BeginUnitOfWorkAsync(string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add a set of parameters to the cache
        /// </summary>
        /// <param name="cacheKey">Key value to look up the parameters</param>
        /// <param name="commandParameters">Actual parameters to cached</param>
        void CacheParameters(string cacheKey, params DbParameter[] commandParameters);

        /// <summary>
        /// Fetch parameters from the cache
        /// </summary>
        /// <param name="cacheKey">Key to look up the parameters</param>
        DbParameter[] GetCachedParameters(string cacheKey);

        /// <summary>
        /// Execute a database query which does not include a select
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        Task<int> ExecuteNonQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Since corefx v. 2.0.1 the DataSet and ADO classes have been fully implemented.
        /// This can be used in multi select use cases.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="commandParameters"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        Task<DataSet> ExecuteMultiQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Execute a select query that will return a result set
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="commandParameters"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        IAsyncEnumerable<IDataRecord> ExecuteReaderAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Use when you wish to do the reflection within your repository
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="commandParameters"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        IAsyncEnumerable<dynamic> ExecuteDynamicQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Execute a command that returns the first column of the first record against the database specified in the connection string
        /// using the provided parameters.
        /// </summary>
        /// <example>
        /// Object obj = await ExecuteScalarAsync(connection, CommandType.StoredProcedure, "espCoreUserGet", new[] { new MySqlParameter("in_userid", 10) });
        /// </example>
        /// <param name="connection">Database connection object</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc..)</param>
        /// <param name="commandText">the stored procedure name or SQL command</param>
        /// <param name="commandTimeout">Command timeout (seconds)</param>
        /// <param name="commandParameters">an array of DbParameters used to execute the command</param>
        /// <returns>An object that should be converted to the expected type using Convert.To{Type}</returns>
        Task<object> ExecuteScalarAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Execute a script
        /// </summary>
        /// <param name="connection">Database connection object</param>
        /// <param name="scriptText">The script text</param>
        /// <param name="commandTimeout">Command timeout (seconds)</param>
        IAsyncEnumerable<int> ExecuteScriptAsync(DbConnection connection, string scriptText, int? commandTimeout = null);

        /// <summary>
        /// Internal function to prepare a command for execution by the database
        /// </summary>
        /// <param name="connection">Database connection object</param>
        /// <param name="commandType">Command type, e.g. stored procedure</param>
        /// <param name="commandText">Command test</param>
        /// <param name="commandParameters">Parameters for the command</param>
        /// <param name="commandTimeout">Command timeout (seconds)</param>
        DbCommand PrepareCommandAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null);

        /// <summary>
        /// Inserts a batch of objects (which must implement IBatchInsertable) into the specified table using a
        /// real parameterized multi-row INSERT.
        /// </summary>
        /// <typeparam name="T">A collection of entities that implement IBatchInsertable and are decorated with the BatchInsertColumn attribute</typeparam>
        /// <param name="connection">The database connection to use</param>
        /// <param name="batchInsertables">The items to insert</param>
        /// <param name="tableName">The table name to insert into</param>
        /// <param name="batchSize">The maximum number of rows per insert statement generated</param>
        /// <param name="testRun">If true, the commands are not sent to the database, only output to the console</param>
        /// <returns>The new Guid for the batch, or null if no data or columns were found</returns>
        Task<Guid?> BatchInsertAsync<T>(DbConnection connection, IEnumerable<T> batchInsertables, string tableName, int batchSize, bool testRun = false) where T : class, IBatchInsertable;

        DbParameter[] ParseParameters(Parameter[] parameters);

        /// <summary>
        /// The concrete <see cref="DbConnection"/> types this provider handles (for example
        /// <c>MySqlConnection</c> for the MySQL provider). The static <c>DBUtility</c> facade routes each
        /// connection-carrying call to the provider that owns the connection's type, which is what lets more than
        /// one provider be registered in the same process.
        /// </summary>
        IReadOnlyCollection<Type> SupportedConnectionTypes { get; }

        /// <summary>
        /// A short, stable key naming this provider's engine (for example <c>"MySQL"</c>). The static
        /// <c>DBUtility</c> facade uses it to create a connection (or unit of work) for a specific engine when
        /// more than one provider is registered, matching the <c>Provider</c> value configured for a connection
        /// string.
        /// </summary>
        string ProviderKey { get; }
    }
}
