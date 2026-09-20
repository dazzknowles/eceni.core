using System;

namespace Eceni.Core.Base.Database.Abstract
{
    public interface IBaseEntity
    {
        bool Deleted { get; set; }
        DateTime CreateDate { get; set; }
        DateTime LastActionDate { get; set; }
        int CreateUserID { get; set; }
        int LastActionUserID { get; set; }
    }
}
