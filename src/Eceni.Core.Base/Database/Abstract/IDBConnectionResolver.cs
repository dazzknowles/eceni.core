namespace Eceni.Core.Base.Database.Abstract
{
    public interface IDBConnectionResolver
    {
        /// <summary>
        /// Resolves the database connection string based on the provided operation type.
        /// Uses the 'ReadOnly' connection string for read-only operations, the 'ReadWrite'
        /// connection string for read/write operations, and the default connection string
        /// if no specific operation type is provided or if the specific configuration is not set.
        /// </summary>
        /// <param name="key">The type of database operation (read-only or read/write), or null to use the default.</param>
        /// <returns>The resolved database connection string.</returns>
        string ResolveConnectionString(string key);

        /// <summary>
        /// Resolves the database provider key (for example <c>"MySQL"</c>) that owns the
        /// connection string selected for <paramref name="key"/>, so a mixed-engine process can create the correct
        /// connection type. Returns null when no provider is configured, in which case the default registered
        /// provider is used.
        /// </summary>
        /// <param name="key">The type of database operation (read-only or read/write), or null to use the default.</param>
        /// <returns>The resolved provider key, or null to use the default provider.</returns>
        string ResolveProvider(string key);
    }
}
