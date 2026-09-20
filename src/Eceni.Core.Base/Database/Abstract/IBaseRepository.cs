using System.Data.Common;

namespace Eceni.Core.Base.Database.Abstract
{
    public interface IBaseRepository
    {
        DbConnection CreateConnectionAsync(string key = null, bool enableStatistics = false);
    }
}
