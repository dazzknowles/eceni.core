using System;

namespace Eceni.Core.Base.Database.Exceptions
{
    /// <summary>
    /// Engine-agnostic signal that a write was rejected by a database UNIQUE constraint (or unique
    /// index). Each <c>IDBUtility</c> provider maps its native duplicate-key error to this type so that callers
    /// in the engine-independent layers can catch a single exception without referencing a specific ADO.NET
    /// provider. The original provider exception is preserved as <see cref="Exception.InnerException"/>.
    /// </summary>
    public class EceniUniqueConstraintException : Exception
    {
        public EceniUniqueConstraintException(string message)
            : base(message)
        {
        }

        public EceniUniqueConstraintException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
