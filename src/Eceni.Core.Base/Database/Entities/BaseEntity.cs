using Eceni.Core.Base.Auditing.Attributes;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using System;
using System.Text.Json.Serialization;

namespace Eceni.Core.Base.Database.Entities
{
    public class BaseEntity : IBaseEntity
    {
        [ColumnName]
        [AuditExclude]
        [JsonIgnore]
        public virtual bool Deleted { get; set; }

        [ColumnName]
        [AuditExclude]
        [JsonIgnore]
        public virtual DateTime CreateDate { get; set; }

        [ColumnName]
        [AuditExclude]
        [JsonIgnore]
        public virtual DateTime LastActionDate { get; set; }

        [ColumnName]
        [AuditExclude]
        [JsonIgnore]
        public virtual int CreateUserID { get; set; }

        [ColumnName]
        [AuditExclude]
        [JsonIgnore]
        public virtual int LastActionUserID { get; set; }
    }
}
