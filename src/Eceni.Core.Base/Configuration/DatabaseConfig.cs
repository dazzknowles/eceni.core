using System.Collections.Generic;

namespace Eceni.Core.Base.Configuration
{
    /// <summary>
    /// Used to either specify a single ConnectionString; or add an infinite amount of ConnectionStrings, of which there can be N 'ReadOnly' keys, and only 1 'ReadWrite'
    /// Config as  "Database": {
    ///  "ConnectionString": "server=...", // Existing single connection string for backward compatibility
    ///  "ConnectionStrings": [ // Array of connection strings
    ///    {
    ///     "Key": "ReadOnly",
    ///    "ConnectionString": "server=...;readonly1"
    ///    },
    ///    {
    ///     "Key": "ReadOnly",
    ///      "ConnectionString": "server=...;readonly2"
    ///    },
    ///    {
    ///     "Key": "ReadWrite",
    ///     "ConnectionString": "server=...;readwrite"
    ///    }
    ///  ]
    ///  }
    /// </summary>
    public class DatabaseConfig
    {
        public virtual string ConnectionString { get; set; }
        public List<ConnectionStringItem> ConnectionStrings { get; set; }

        /// <summary>
        /// The database engine that owns <see cref="ConnectionString"/> (and the default for any
        /// <see cref="ConnectionStringItem"/> that does not set its own). Matches an <c>IDBUtility.ProviderKey</c>
        /// — <c>"MySQL"</c> currently, case-insensitive. Leave null/empty in a single-engine deployment;
        /// the sole registered provider is then used, so existing configuration is unaffected. It only matters when
        /// more than one provider is registered in the same process.
        /// </summary>
        public string Provider { get; set; }
    }

    public class ConnectionStringItem
    {
        public string Key { get; set; }
        public string ConnectionString { get; set; }

        /// <summary>
        /// The database engine that owns this connection string. Matches an <c>IDBUtility.ProviderKey</c>.
        /// When null/empty the enclosing <see cref="DatabaseConfig.Provider"/> is used, and failing that the
        /// default registered provider. Set it only when different connection strings must target different
        /// engines in the same process.
        /// </summary>
        public string Provider { get; set; }
    }
}
