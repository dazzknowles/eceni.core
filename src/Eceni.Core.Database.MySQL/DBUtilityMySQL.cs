using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Database.Common;
using Eceni.Core.Base.Database.Entities;
using Eceni.Core.Base.Database.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using MySqlConnector;

namespace Eceni.Core.Database.MySQL
{
    /// <summary>
    /// MySQL/MariaDB data-access provider
    /// </summary>
    public class DBUtilityMySQL : IDBUtility
    {
        public const string ParameterPrefix = "in_";

        /// <summary>
        /// The provider key naming this engine, as used by <c>DatabaseConfig.Provider</c> / <c>ConnectionStringItem.Provider</c>.
        /// </summary>
        public const string ProviderName = "MySQL";

        private static readonly IReadOnlyCollection<Type> _supportedConnectionTypes = new Type[] { typeof(MySqlConnection) };

        /// <inheritdoc />
        public IReadOnlyCollection<Type> SupportedConnectionTypes
        {
            get { return _supportedConnectionTypes; }
        }

        /// <inheritdoc />
        public string ProviderKey
        {
            get { return ProviderName; }
        }

        /// <summary>
        /// Creates Hashtable of reflected fields
        /// </summary>
        protected readonly Hashtable ParamCache = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// Creates a private instance of DbConnection (used when calling the database asynchronously)
        /// </summary>
        /// <returns></returns>
        public DbConnection CreateConnectionAsync(string connectionString, bool enableStatistics = false)
        {
            return new MySqlConnection(connectionString);
        }

        /// <summary>
        /// Prevents multiple connections within the same transaction
        /// </summary>
        /// <returns></returns>
        public TransactionScope CreateTransactionScopeAsync(TransactionScopeOption transactionScopeOption = TransactionScopeOption.Required, TransactionScopeAsyncFlowOption transactionScopeAsyncFlowOption = TransactionScopeAsyncFlowOption.Enabled)
        {
            TransactionOptions transactionOptions = new TransactionOptions
            {
                IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                Timeout = TransactionManager.DefaultTimeout
            };

            return new TransactionScope(transactionScopeOption, transactionOptions, transactionScopeAsyncFlowOption);
        }

        /// <summary>
        /// Opens a new <see cref="MySqlConnection"/>, begins a plain local <see cref="MySqlTransaction"/> on it,
        /// and returns the unit of work wrapping both.
        /// </summary>
        public async Task<IDbUnitOfWork> BeginUnitOfWorkAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
                return new MySqlUnitOfWork(connection, transaction);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        private const string UniqueViolationMessage = "The database rejected the write because it violates a unique constraint.";

        /// <summary>
        /// True for the MySQL error numbers that indicate a unique-constraint / unique-index violation:
        /// 1062 (ER_DUP_ENTRY, a duplicate value for a unique key), 1586 (ER_DUP_ENTRY_WITH_KEY_NAME, the same
        /// duplicate reported together with the key name), 1022 (ER_DUP_KEY, a duplicate key on write), and
        /// 1169 (ER_DUP_UNIQUE, a duplicate for a unique index that may not contain duplicates).
        /// </summary>
        public static bool IsUniqueConstraintViolation(int errorNumber)
        {
            return errorNumber == 1062 || errorNumber == 1586 || errorNumber == 1022 || errorNumber == 1169;
        }

        public async Task<int> ExecuteNonQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null)
        {
            using (MySqlCommand command = (MySqlCommand)PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout))
            {
                try
                {
                    await connection.OpenAsync();

                    return await command.ExecuteNonQueryAsync();
                }
                catch (MySqlException ex) when (IsUniqueConstraintViolation(ex.Number))
                {
                    throw new EceniUniqueConstraintException(UniqueViolationMessage, ex);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public async Task<DataSet> ExecuteMultiQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null)
        {
            using (MySqlCommand command = (MySqlCommand)PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout))
            {
                try
                {
                    await connection.OpenAsync();

                    using MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                    DataSet dataSet = new();

                    await Task.Run(() => adapter.Fill(dataSet));

                    return dataSet;
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public async IAsyncEnumerable<IDataRecord> ExecuteReaderAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null)
        {
            using (MySqlCommand command = (MySqlCommand)PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout))
            {
                DbDataReader reader = null;

                try
                {
                    await connection.OpenAsync();

                    //Execute the query, stating that the connection should close when the resulting data reader has been read
                    reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);

                    while (await reader.ReadAsync())
                    {
                        yield return reader;
                    }
                }
                finally
                {
                    // The finally runs when the iterator is disposed, including when the consumer abandons
                    // enumeration early, so the reader and connection are always released.
                    if (reader != null)
                    {
                        await reader.DisposeAsync();
                    }

                    connection.Close();
                }
            }
        }

        public async IAsyncEnumerable<dynamic> ExecuteDynamicQueryAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null)
        {
            using (MySqlCommand command = (MySqlCommand)PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout))
            {
                try
                {
                    await connection.OpenAsync();

                    using MySqlDataReader dataReader = await command.ExecuteReaderAsync();
                    while (dataReader.Read())
                    {
                        IDictionary<string, object> dataRow = new ExpandoObject() as IDictionary<string, object>;

                        for (int fieldNum = 0; fieldNum < dataReader.FieldCount; fieldNum++)
                        {
                            dataRow.Add(dataReader.GetName(fieldNum), dataReader[fieldNum]);
                        }

                        yield return dataRow;
                    }
                }
                finally
                {
                    connection.Close();
                }

                command.Parameters.Clear();
            }
        }

        public async Task<object> ExecuteScalarAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters = null, int? commandTimeout = null)
        {
            using (MySqlCommand command = (MySqlCommand)PrepareCommandAsync(connection, commandType, commandText, commandParameters, commandTimeout))
            {
                try
                {
                    await connection.OpenAsync();

                    return await command.ExecuteScalarAsync();
                }
                catch (MySqlException ex) when (IsUniqueConstraintViolation(ex.Number))
                {
                    throw new EceniUniqueConstraintException(UniqueViolationMessage, ex);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public async IAsyncEnumerable<int> ExecuteScriptAsync(DbConnection connection, string scriptText, int? commandTimeout = null)
        {
            try
            {
                await connection.OpenAsync();

                using (MySqlCommand command = new MySqlCommand())
                {
                    command.Connection = (MySqlConnection)connection;
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = (int)(commandTimeout ?? connection?.ConnectionTimeout);

                    foreach (string commandText in SplitSqlStatements(scriptText))
                    {
                        if (!string.IsNullOrWhiteSpace(commandText))
                        {
                            command.CommandText = commandText;
                            yield return await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Splits a multi-statement MySQL script into individual statements on the current statement delimiter,
        /// honouring the client-side <c>DELIMITER</c> directive that MySQL routines rely on. The delimiter starts
        /// as <c>;</c>; a line whose only content is <c>DELIMITER &lt;token&gt;</c> (case-insensitive) switches the
        /// terminator to that token — typically <c>$$</c> or <c>//</c> — for the statements that follow and is
        /// itself consumed, and a later <c>DELIMITER ;</c> switches it back. Because a stored-routine body is
        /// wrapped in such a delimiter change, its internal <c>BEGIN ... END</c> semicolons are ordinary characters
        /// under the active (non-<c>;</c>) delimiter and the whole <c>CREATE PROCEDURE</c>/<c>FUNCTION</c>/
        /// <c>TRIGGER</c> is returned as one statement. The lexer ignores a delimiter that appears inside string
        /// literals (<c>'...'</c> and <c>"..."</c>), backtick-quoted identifiers, or comments (<c>--</c> followed by
        /// whitespace to end of line, <c>#</c> to end of line, and <c>/* ... */</c> blocks). Note that bare
        /// <c>BEGIN ... END</c> block detection is deliberately NOT attempted: in MySQL a bare <c>BEGIN</c> is also
        /// a transaction-control statement, so depth-counting it would misread ordinary transactions — the
        /// <c>DELIMITER</c> directive, which every MySQL client requires for routines, is the correct mechanism.
        /// </summary>
        /// <param name="scriptText">The raw, multi-statement script</param>
        /// <returns>The individual statements, in order, each without its trailing delimiter</returns>
        internal static IEnumerable<string> SplitSqlStatements(string scriptText)
        {
            List<string> statements = new List<string>();
            if (string.IsNullOrEmpty(scriptText))
            {
                return statements;
            }

            StringBuilder current = new StringBuilder();
            string delimiter = ";";
            int length = scriptText.Length;
            int i = 0;
            bool atLineStart = true;
            while (i < length)
            {
                // A DELIMITER directive is only ever the first token on a clean line (one that is not the
                // continuation of a string literal or block comment carried over from an earlier line).
                if (atLineStart)
                {
                    if (TryMatchDelimiterDirective(scriptText, i, out string newDelimiter, out int afterDirective))
                    {
                        delimiter = newDelimiter;
                        i = afterDirective;
                        atLineStart = true;
                        continue;
                    }

                    atLineStart = false;
                }

                char c = scriptText[i];

                if ((c == '-' && i + 1 < length && scriptText[i + 1] == '-' && (i + 2 >= length || char.IsWhiteSpace(scriptText[i + 2]))) || c == '#')
                {
                    while (i < length && scriptText[i] != '\n')
                    {
                        current.Append(scriptText[i]);
                        i++;
                    }

                    continue;
                }

                if (c == '/' && i + 1 < length && scriptText[i + 1] == '*')
                {
                    current.Append(scriptText[i]);
                    current.Append(scriptText[i + 1]);
                    i += 2;
                    while (i < length && !(scriptText[i] == '*' && i + 1 < length && scriptText[i + 1] == '/'))
                    {
                        current.Append(scriptText[i]);
                        i++;
                    }

                    if (i + 1 < length)
                    {
                        current.Append(scriptText[i]);
                        current.Append(scriptText[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        while (i < length)
                        {
                            current.Append(scriptText[i]);
                            i++;
                        }
                    }

                    continue;
                }

                if (c == '\'' || c == '"' || c == '`')
                {
                    char quote = c;
                    current.Append(c);
                    i++;
                    while (i < length)
                    {
                        char q = scriptText[i];
                        if (q == '\\' && quote != '`' && i + 1 < length)
                        {
                            current.Append(q);
                            current.Append(scriptText[i + 1]);
                            i += 2;
                            continue;
                        }

                        current.Append(q);
                        i++;
                        if (q == quote)
                        {
                            if (i < length && scriptText[i] == quote)
                            {
                                current.Append(scriptText[i]);
                                i++;
                                continue;
                            }

                            break;
                        }
                    }

                    continue;
                }

                if (MatchesDelimiterAt(scriptText, i, delimiter))
                {
                    statements.Add(current.ToString());
                    current.Clear();
                    i += delimiter.Length;
                    continue;
                }

                if (c == '\n')
                {
                    current.Append(c);
                    i++;
                    atLineStart = true;
                    continue;
                }

                current.Append(c);
                i++;
            }

            if (current.Length > 0)
            {
                statements.Add(current.ToString());
            }

            return statements;
        }

        /// <summary>
        /// Determines whether the current statement delimiter occurs verbatim at <paramref name="index"/>. The
        /// delimiter is matched literally and case-sensitively, which lets a multi-character delimiter such as
        /// <c>$$</c> or <c>//</c> terminate a statement.
        /// </summary>
        /// <param name="s">The script</param>
        /// <param name="index">The position to test</param>
        /// <param name="delimiter">The active delimiter</param>
        /// <returns><c>true</c> when the delimiter starts at <paramref name="index"/></returns>
        private static bool MatchesDelimiterAt(string s, int index, string delimiter)
        {
            if (index + delimiter.Length > s.Length)
            {
                return false;
            }

            for (int k = 0; k < delimiter.Length; k++)
            {
                if (s[index + k] != delimiter[k])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the line beginning at <paramref name="start"/> is a <c>DELIMITER</c> directive. Such
        /// a line is optional leading horizontal whitespace, the word <c>DELIMITER</c> (case-insensitive), at least
        /// one space or tab, and a delimiter token (a run of non-whitespace characters); the remainder of the line
        /// is consumed. The directive itself is not emitted as a statement.
        /// </summary>
        /// <param name="s">The script</param>
        /// <param name="start">Index of the first character of the line</param>
        /// <param name="delimiter">The new delimiter token</param>
        /// <param name="afterDirective">Index of the first character after the consumed directive line</param>
        /// <returns><c>true</c> when the line is a DELIMITER directive</returns>
        private static bool TryMatchDelimiterDirective(string s, int start, out string delimiter, out int afterDirective)
        {
            delimiter = null;
            afterDirective = start;
            int length = s.Length;
            int i = start;

            while (i < length && (s[i] == ' ' || s[i] == '\t'))
            {
                i++;
            }

            const string keyword = "DELIMITER";
            if (i + keyword.Length > length)
            {
                return false;
            }

            for (int k = 0; k < keyword.Length; k++)
            {
                if (char.ToUpperInvariant(s[i + k]) != keyword[k])
                {
                    return false;
                }
            }

            i += keyword.Length;

            // The keyword must be followed by at least one horizontal space or tab, both as a word boundary (so
            // DELIMITERX is not matched) and as the separator before the token.
            if (i >= length || !(s[i] == ' ' || s[i] == '\t'))
            {
                return false;
            }

            while (i < length && (s[i] == ' ' || s[i] == '\t'))
            {
                i++;
            }

            int tokenStart = i;
            while (i < length && !char.IsWhiteSpace(s[i]))
            {
                i++;
            }

            if (i == tokenStart)
            {
                return false;
            }

            delimiter = s.Substring(tokenStart, i - tokenStart);

            while (i < length && s[i] != '\n')
            {
                i++;
            }

            if (i < length && s[i] == '\n')
            {
                i++;
            }

            afterDirective = i;
            return true;
        }

        /// <summary>
        /// Add a set of parameters to the cache
        /// </summary>
        /// <param name="cacheKey">Key value to look up the parameters</param>
        /// <param name="commandParameters">Actual parameters to cached</param>
        public void CacheParameters(string cacheKey, params DbParameter[] commandParameters)
        {
            if (commandParameters == null)
            {
                ParamCache[cacheKey] = null;
                return;
            }

            // Store an independent clone snapshot: keeping the caller's array by reference would let a later
            // mutation of the caller's parameters silently corrupt the cache, and would leave a DbParameter[] in
            // the cache that GetCachedParameters cannot read.
            MySqlParameter[] cachedParameters = new MySqlParameter[commandParameters.Length];
            for (int i = 0; i < commandParameters.Length; i++)
            {
                cachedParameters[i] = ((MySqlParameter)commandParameters[i]).Clone();
            }

            ParamCache[cacheKey] = cachedParameters;
        }

        /// <summary>
        /// Internal function to prepare a command for execution by the database
        /// </summary>
        /// <param name="connection">Database connection object</param>
        /// <param name="commandType">Command type, e.g. stored procedure</param>
        /// <param name="commandText">Command test</param>
        /// <param name="commandTimeout">Command timeout (seconds)</param>
        /// <param name="commandParameters">Parameters for the command</param>
        public DbCommand PrepareCommandAsync(DbConnection connection, CommandType commandType, string commandText, DbParameter[] commandParameters, int? commandTimeout = null)
        {
            MySqlCommand cmd = new MySqlCommand
            {
                Connection = (MySqlConnection)connection,
                CommandText = commandText,
                CommandType = commandType
            };

            cmd.Parameters.Clear();

            // Bind a clone of each parameter passed in, and normalise null on the clone, so the caller's own
            // parameter instances are never mutated and can be reused across commands.
            if (commandParameters != null)
            {
                foreach (MySqlParameter param in commandParameters)
                {
                    MySqlParameter clone = param.Clone();
                    if (clone.Value == null)
                    {
                        clone.Value = DBNull.Value;
                    }

                    cmd.Parameters.Add(clone);
                }
            }

            if (commandTimeout.HasValue)
            {
                cmd.CommandTimeout = commandTimeout.Value;
            }

            return cmd;
        }

        /// <summary>
        /// Fetch parameters from the cache
        /// </summary>
        /// <param name="cacheKey">Key to look up the parameters</param>
        public DbParameter[] GetCachedParameters(string cacheKey)
        {
            MySqlParameter[] cachedParms = ParamCache[cacheKey] as MySqlParameter[];

            if (cachedParms == null)
            {
                return null;
            }

            // If the parameters are in the cache
            MySqlParameter[] clonedParms = new MySqlParameter[cachedParms.Length];

            // return a copy of the parameters
            for (int i = 0, j = cachedParms.Length; i < j; i++)
            {
                clonedParms[i] = cachedParms[i].Clone();
            }

            return clonedParms;
        }

        /// <summary>
        /// Inserts a batch of objects (which must implement IBatchInsertable) into the specified table using a
        /// real parameterized multi-row INSERT — every cell is bound as a <see cref="MySqlParameter"/>, never
        /// embedded as a SQL literal.
        /// </summary>
        /// <typeparam name="T">A collection of entities that implement IBatchInsertable and are decorated with the BatchInsertColumn attribute</typeparam>
        /// <param name="connection">The database connection to use</param>
        /// <param name="batchInsertables">The items to insert</param>
        /// <param name="tableName">The table name to insert into</param>
        /// <param name="batchSize">The maximum number of rows per insert statement generated</param>
        /// <param name="testRun">If true, the commands are not sent to the database, only output to the console</param>
        /// <returns>The new Guid for the batch, or null if no data or columns were found</returns>
        public async Task<Guid?> BatchInsertAsync<T>(DbConnection connection, IEnumerable<T> batchInsertables, string tableName, int batchSize, bool testRun = false) where T : class, IBatchInsertable
        {
            List<T> entities = batchInsertables.ToList();
            if (entities.Count == 0)
            {
                return null;
            }

            Guid batchID = Guid.NewGuid();
            foreach (T entity in entities)
            {
                entity.BatchGuid = batchID;
            }

            List<PropertyInfo> properties = new List<PropertyInfo>();
            List<string> columnNames = new List<string>();
            foreach (PropertyInfo property in DBUtilityCommon.GetPropertyInfo(typeof(T)))
            {
                Attribute attribute =
                    ((Attribute[])property.GetCustomAttributes(typeof(BatchInsertColumnAttribute), true))
                    .SingleOrDefault(a => a.GetType() == typeof(BatchInsertColumnAttribute));

                if (attribute == null)
                {
                    continue;
                }

                string columnName = ((BatchInsertColumnAttribute)attribute).Name ?? property.Name;
                columnNames.Add(columnName);
                properties.Add(property);
            }

            if (properties.Count == 0 || columnNames.Count == 0)
            {
                return null;
            }

            for (int start = 0; start < entities.Count; start += batchSize)
            {
                T[] currentEntities = entities.Skip(start).Take(batchSize).ToArray();

                (string sql, MySqlParameter[] parameters) = BuildParameterizedInsert(tableName, columnNames, properties, currentEntities);

                if (testRun)
                {
                    Console.WriteLine(sql);
                    foreach (MySqlParameter parameter in parameters)
                    {
                        Console.WriteLine($"{parameter.ParameterName} = {parameter.Value}");
                    }
                }

                if (!testRun && connection != null)
                {
                    await ExecuteNonQueryAsync(connection, CommandType.Text, sql, parameters);
                }
            }

            return batchID;
        }

        /// <summary>
        /// Builds a single parameterized multi-row <c>INSERT</c> statement for <paramref name="entities"/>: the
        /// table name and every column name are identifier-quoted (<see cref="QuoteTableName"/>/
        /// <see cref="QuoteIdentifier"/>), and every cell value is bound as its own <see cref="MySqlParameter"/>
        /// (named <c>in_p{row}_{col}</c>) rather than embedded as a SQL literal.
        /// </summary>
        internal static (string Sql, MySqlParameter[] Parameters) BuildParameterizedInsert(string tableName, List<string> columnNames, List<PropertyInfo> properties, object[] entities)
        {
            StringBuilder command = new StringBuilder();

            command.Append($"INSERT INTO {QuoteTableName(tableName)} (");
            for (int i = 0; i < columnNames.Count; i++)
            {
                command.Append(QuoteIdentifier(columnNames[i]));

                if (i != columnNames.Count - 1)
                {
                    command.Append(", ");
                }
            }

            command.Append(") VALUES ");

            List<MySqlParameter> parameters = new List<MySqlParameter>();

            for (int row = 0; row < entities.Length; row++)
            {
                object entity = entities[row];
                command.Append("(");

                for (int col = 0; col < properties.Count; col++)
                {
                    string parameterName = $"{ParameterPrefix}p{row}_{col}";
                    object value = properties[col].GetValue(entity) ?? DBNull.Value;
                    parameters.Add(new MySqlParameter(parameterName, value));

                    command.Append("@").Append(parameterName);

                    if (col != properties.Count - 1)
                    {
                        command.Append(", ");
                    }
                }

                command.Append(")");

                if (row != entities.Length - 1)
                {
                    command.Append(", ");
                }
            }

            return (command.ToString(), parameters.ToArray());
        }

        /// <summary>
        /// Quotes a single MySQL identifier by wrapping it in backticks and doubling any embedded backtick. This
        /// prevents an identifier that contains a delimiter (or otherwise attacker-controlled text) from breaking
        /// out of the identifier and injecting SQL. An identifier that is ALREADY backtick-delimited is first
        /// unwrapped (outer backticks stripped, any doubled inner backtick collapsed) so a pre-delimited name
        /// round-trips to the same single-quoted form instead of being double-quoted; a raw name — including one
        /// that merely contains a backtick — is quoted and escaped as usual.
        /// </summary>
        /// <param name="identifier">The identifier (a single, unqualified name; raw or already backtick-delimited)</param>
        /// <returns>The backtick-quoted identifier</returns>
        internal static string QuoteIdentifier(string identifier)
        {
            string raw = identifier;
            if (identifier.Length >= 2 && identifier[0] == '`' && identifier[identifier.Length - 1] == '`')
            {
                raw = identifier.Substring(1, identifier.Length - 2).Replace("``", "`");
            }

            return "`" + raw.Replace("`", "``") + "`";
        }

        /// <summary>
        /// Quotes a possibly schema-qualified table name by backtick-quoting each dot-separated part (for example
        /// <c>mydb.MyTable</c> becomes <c>`mydb`.`MyTable`</c>). A dot is treated as a qualifier separator, not as
        /// part of an identifier; an identifier containing a literal dot is not supported.
        /// </summary>
        /// <param name="tableName">The raw, possibly schema-qualified table name</param>
        /// <returns>The backtick-quoted, dot-separated table name</returns>
        internal static string QuoteTableName(string tableName)
        {
            string[] parts = tableName.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = QuoteIdentifier(parts[i]);
            }

            return string.Join(".", parts);
        }

        /// <summary>
        /// Converter to use Boolean data type with MySql
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns></returns>
        public static string MySqlBit(bool value)
        {
            return value ? "TRUE" : "FALSE";
        }

        /// <summary>
        /// Converter to use Boolean data type with MySql
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns></returns>
        public static bool MySqlBool(string value)
        {
            return value.ToUpper().Equals("TRUE");
        }

        /// <summary>
        /// Convert generic Parameter data type to DB-specific parameter data type
        /// </summary>
        /// <param name="parameters">Generic parameters to convert</param>
        /// <returns></returns>
        public DbParameter[] ParseParameters(Parameter[] parameters)
        {
            MySqlParameter[] mySqlParameters = new MySqlParameter[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                mySqlParameters[i] = new MySqlParameter($"{ParameterPrefix}{parameters[i].Name}", parameters[i].Value);
            }

            return mySqlParameters;
        }
    }
}
