using System;

namespace Eceni.Core.Base.Database.Attributes
{
    /// <summary>
    /// Used by DBUtility to flag a column to be encrypted at rest
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class EncryptedAttribute : Attribute
    {
        public string Name { get; protected set; }

        public EncryptedAttribute()
        {
            Name = string.Empty;
        }

        public EncryptedAttribute(string columnName)
        {
            Name = columnName;
        }
    }
}
