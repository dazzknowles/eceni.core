using System;

namespace Eceni.Core.Base.Database.Attributes
{
    /// <summary>
    /// Used by DBUtility to reflect on object fields. Eceni ORM.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ColumnNameAttribute : Attribute
    {
        public string Name { get; protected set; }

        public ColumnNameAttribute()
        {
            Name = string.Empty;
        }

        public ColumnNameAttribute(string columnName)
        {
            Name = columnName;
        }
    }
}
