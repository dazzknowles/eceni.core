using Microsoft.Extensions.DependencyInjection;
using Eceni.Core.Base.Database.Concrete;

namespace Eceni.Core.Database.MySQL
{
    public static class EceniCoreDatabaseMySQLStartup
    {
        /// <summary>
        /// Registers the MySQL-specific DBUtility class
        /// </summary>
        public static IServiceCollection AddEceniCoreDatabaseMySQL(this IServiceCollection services)
        {
            DBUtility.RegisterDbUtility(new DBUtilityMySQL());

            return services;
        }
    }
}
