using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Exceptions;
using MySqlConnector;
using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Eceni.Core.Database.MySQL
{
    /// <summary>
    /// One open <see cref="MySqlConnection"/> and one plain local <see cref="MySqlTransaction"/> — deliberately
    /// not <c>System.Transactions</c>/<c>TransactionScope</c> (see SOL-T-1301: MySqlConnector's <c>TransactionScope</c>
    /// support defaults to full XA/two-phase-commit transactions, which are unnecessary and carry an
    /// orphaned-prepared-transaction recovery risk for a single-connection, single-database write). Every
    /// <see cref="ExecuteAsync"/>/<see cref="ExecuteScalarAsync{T}"/> call runs a stored procedure against this
    /// connection and transaction. Disposal rolls back unless <see cref="CommitAsync"/> was called first.
    /// </summary>
    internal sealed class MySqlUnitOfWork : IDbUnitOfWork
    {
        private const string UniqueViolationMessage = "The database rejected the write because it violates a unique constraint.";

        private readonly MySqlConnection _connection;
        private readonly MySqlTransaction _transaction;
        private bool _committed;
        private bool _disposed;

        internal MySqlUnitOfWork(MySqlConnection connection, MySqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public async Task ExecuteAsync(string storedProcedure, DbParameter[] parameters, CancellationToken cancellationToken = default)
        {
            using (MySqlCommand command = CreateCommand(storedProcedure, parameters))
            {
                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (MySqlException ex) when (DBUtilityMySQL.IsUniqueConstraintViolation(ex.Number))
                {
                    throw new EceniUniqueConstraintException(UniqueViolationMessage, ex);
                }
            }
        }

        public async Task<T> ExecuteScalarAsync<T>(string storedProcedure, DbParameter[] parameters, CancellationToken cancellationToken = default)
        {
            using (MySqlCommand command = CreateCommand(storedProcedure, parameters))
            {
                object result;
                try
                {
                    result = await command.ExecuteScalarAsync(cancellationToken);
                }
                catch (MySqlException ex) when (DBUtilityMySQL.IsUniqueConstraintViolation(ex.Number))
                {
                    throw new EceniUniqueConstraintException(UniqueViolationMessage, ex);
                }

                if (result == null || result == DBNull.Value)
                {
                    return default;
                }

                return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
                await _connection.DisposeAsync();
            }
        }

        private MySqlCommand CreateCommand(string storedProcedure, DbParameter[] parameters)
        {
            MySqlCommand command = _connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = storedProcedure;
            command.Transaction = _transaction;

            if (parameters != null)
            {
                foreach (MySqlParameter parameter in parameters)
                {
                    MySqlParameter clone = parameter.Clone();
                    if (clone.Value == null)
                    {
                        clone.Value = DBNull.Value;
                    }

                    command.Parameters.Add(clone);
                }
            }

            return command;
        }
    }
}
