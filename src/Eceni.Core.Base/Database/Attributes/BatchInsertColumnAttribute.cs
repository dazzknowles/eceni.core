using System;

namespace Eceni.Core.Base.Database.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class BatchInsertColumnAttribute : Attribute
    {
        public string Name { get; }

        public BatchInsertColumnAttribute()
        {
            Name = null;
        }

        public BatchInsertColumnAttribute(string columnName)
        {
            Name = columnName;
        }
    }
}
