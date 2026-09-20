using System;

namespace Eceni.Core.Base.Auditing.Attributes
{
    /// <summary>
    /// Decorate the primary key (or one part of a composite key) of an entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class AuditKeyAttribute : Attribute
    {
        public int KeyIndex { get; protected set; }

        public AuditKeyAttribute(int keyIndex)
        {
            KeyIndex = keyIndex;
        }
    }
}
