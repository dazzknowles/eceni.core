using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Eceni.Core.Base.Database.Abstract
{
    /// <summary>
    /// An explicit, local unit of work: one open connection and one plain database transaction, deliberately not
    /// <c>System.Transactions</c>/<c>TransactionScope</c>. A provider's <see cref="IDBUtility.BeginUnitOfWorkAsync"/>
    /// opens the connection and begins the transaction; every <see cref="ExecuteAsync"/>/<see cref="ExecuteScalarAsync{T}"/>
    /// call runs a stored procedure against that same connection and transaction. Call <see cref="CommitAsync"/> to
    /// commit; if it is never called, disposal rolls back.
    /// </summary>
    public interface IDbUnitOfWork : IAsyncDisposable
    {
        /// <summary>
        /// Executes a stored procedure that does not return a result set.
        /// </summary>
        Task ExecuteAsync(string storedProcedure, DbParameter[] parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure and returns the first column of the first row, converted to <typeparamref name="T"/>.
        /// </summary>
        Task<T> ExecuteScalarAsync<T>(string storedProcedure, DbParameter[] parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the underlying transaction. If this is never called before disposal, the transaction is
        /// rolled back instead.
        /// </summary>
        Task CommitAsync(CancellationToken cancellationToken = default);
    }
}
