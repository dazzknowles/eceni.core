using Eceni.Core.Base.Database.Attributes;
using System;

namespace Eceni.Core.Base.Database.Entities
{
    public class BaseDateEffectiveEntity : BaseEntity
    {
        [ColumnName]
        public virtual DateTime? DateFrom { get; set; }

        [ColumnName]
        public virtual DateTime? DateTo { get; set; }
    }
}
