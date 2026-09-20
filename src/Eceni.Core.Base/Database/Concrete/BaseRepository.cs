using Microsoft.Extensions.Options;
using Eceni.Core.Base.Configuration;
using Eceni.Core.Base.Database.Abstract;
using System;
using System.Data.Common;

namespace Eceni.Core.Base.Database.Concrete
{
    public abstract class BaseRepository : IBaseRepository
    {
        protected readonly DatabaseConfig _databaseConfig;
        protected readonly IDBConnectionResolver _connectionResolver;

        /// <summary>
        /// Primary constructor. <paramref name="connectionResolver"/> is optional so a repository can be
        /// constructed from just a <see cref="DatabaseConfig"/> when no per-key (ReadOnly/ReadWrite) resolution
        /// is needed.
        /// </summary>
        protected BaseRepository(IOptions<DatabaseConfig> databaseConfig,
                                 IDBConnectionResolver connectionResolver = null)
        {
            _databaseConfig = databaseConfig?.Value;
            _connectionResolver = connectionResolver;
        }

        /// <summary>
        /// Used to create a new DB connection from the host application's IOptions
        /// Accepts key for selection of database (ReadOnly or ReadWrite)
        /// </summary>
        /// <returns></returns>
        public virtual DbConnection CreateConnectionAsync(string key = null, bool enableStatistics = false)
        {
            if (_connectionResolver == null)
            {
                if (_databaseConfig == null)
                {
                    throw new InvalidOperationException("BaseRepository cannot create a connection: neither an IDBConnectionResolver nor a DatabaseConfig was supplied. Register a DatabaseConfig via IOptions<DatabaseConfig> or an IDBConnectionResolver, and pass it through the derived repository's base constructor call.");
                }

                return DBUtility.CreateConnectionAsync(_databaseConfig.ConnectionString, _databaseConfig.Provider, enableStatistics);
            }

            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(key), _connectionResolver.ResolveProvider(key), enableStatistics);
        }
    }
}
