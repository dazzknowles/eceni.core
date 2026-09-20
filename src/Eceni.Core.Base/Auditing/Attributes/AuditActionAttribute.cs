using System;

namespace Eceni.Core.Base.Auditing.Attributes
{
    /// <summary>
    /// Used to decorate entities for auditing. Instructs a future audit service which action IDs to use.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class AuditActionAttribute : Attribute
    {
        public int InsertID { get; protected set; }
        public int UpdateID { get; protected set; }
        public int DeleteID { get; protected set; }

        public AuditActionAttribute(int insertID, int updateID, int deleteID)
        {
            InsertID = insertID;
            UpdateID = updateID;
            DeleteID = deleteID;
        }
    }
}
