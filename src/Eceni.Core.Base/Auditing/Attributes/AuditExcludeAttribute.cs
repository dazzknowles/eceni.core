using System;

namespace Eceni.Core.Base.Auditing.Attributes
{
    /// <summary>
    /// Used to exclude a field from a future audit log entry when serializing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class AuditExcludeAttribute : Attribute
    {
    }
}
