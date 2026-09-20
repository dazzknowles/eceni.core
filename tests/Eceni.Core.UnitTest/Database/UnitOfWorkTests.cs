using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Database.MySQL;
using MySqlConnector;

namespace Eceni.Core.UnitTest.Database
{
    /// <summary>
    /// Exercises <see cref="MySqlUnitOfWork"/>'s commit / rollback-on-dispose behaviour against a real
    /// MySQL/MariaDB instance. Skipped (Inconclusive) when <c>ECENI_TEST_MYSQL_CONNECTION_STRING</c> is not set —
    /// no local MySQL/MariaDB is assumed to be available in CI, so these do not run there; set the environment
    /// variable to exercise them locally against a throwaway database.
    /// </summary>
    [TestClass]
    public class UnitOfWorkTests
    {
        private static string ConnectionString => Environment.GetEnvironmentVariable("ECENI_TEST_MYSQL_CONNECTION_STRING");

        private static void SkipIfNoLocalDatabase()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Assert.Inconclusive("Set ECENI_TEST_MYSQL_CONNECTION_STRING to run unit-of-work tests against a real MySQL/MariaDB instance.");
            }
        }

        private static string UniqueTableName() => "eceni_uow_test_" + Guid.NewGuid().ToString("N");

        private static async Task CreateScratchTableAndProceduresAsync(string tableName, string insertProcName, string countProcName)
        {
            DBUtilityMySQL dbUtility = new DBUtilityMySQL();
            DbConnection connection = dbUtility.CreateConnectionAsync(ConnectionString);

            string script =
                $"CREATE TABLE {tableName} (ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY, Value VARCHAR(64) NOT NULL);\n" +
                "DELIMITER $$\n" +
                $"CREATE PROCEDURE {insertProcName}(IN in_value VARCHAR(64)) BEGIN INSERT INTO {tableName} (Value) VALUES (in_value); END$$\n" +
                $"CREATE PROCEDURE {countProcName}() BEGIN SELECT COUNT(*) FROM {tableName}; END$$\n" +
                "DELIMITER ;\n";

            await foreach (int _ in dbUtility.ExecuteScriptAsync(connection, script))
            {
                // Draining is enough; nothing to assert per statement.
            }
        }

        private static async Task DropScratchObjectsAsync(string tableName, string insertProcName, string countProcName)
        {
            DBUtilityMySQL dbUtility = new DBUtilityMySQL();
            DbConnection connection = dbUtility.CreateConnectionAsync(ConnectionString);

            string script =
                $"DROP PROCEDURE IF EXISTS {insertProcName};\n" +
                $"DROP PROCEDURE IF EXISTS {countProcName};\n" +
                $"DROP TABLE IF EXISTS {tableName};\n";

            await foreach (int _ in dbUtility.ExecuteScriptAsync(connection, script))
            {
            }
        }

        private static async Task<long> CountRowsAsync(string countProcName)
        {
            DBUtilityMySQL dbUtility = new DBUtilityMySQL();
            DbConnection connection = dbUtility.CreateConnectionAsync(ConnectionString);
            object result = await dbUtility.ExecuteScalarAsync(connection, CommandType.StoredProcedure, countProcName);
            return Convert.ToInt64(result);
        }

        [TestMethod]
        public async Task CommitAsync_PersistsTheWrite()
        {
            SkipIfNoLocalDatabase();

            string tableName = UniqueTableName();
            string insertProcName = tableName + "_ins";
            string countProcName = tableName + "_cnt";
            await CreateScratchTableAndProceduresAsync(tableName, insertProcName, countProcName);

            try
            {
                DBUtilityMySQL dbUtility = new DBUtilityMySQL();
                await using (IDbUnitOfWork unitOfWork = await dbUtility.BeginUnitOfWorkAsync(ConnectionString))
                {
                    await unitOfWork.ExecuteAsync(insertProcName, new[] { new MySqlParameter("in_value", "committed") });
                    await unitOfWork.CommitAsync();
                }

                Assert.AreEqual(1L, await CountRowsAsync(countProcName), "A committed unit of work must persist its write.");
            }
            finally
            {
                await DropScratchObjectsAsync(tableName, insertProcName, countProcName);
            }
        }

        [TestMethod]
        public async Task DisposeWithoutCommit_RollsBackTheWrite()
        {
            SkipIfNoLocalDatabase();

            string tableName = UniqueTableName();
            string insertProcName = tableName + "_ins";
            string countProcName = tableName + "_cnt";
            await CreateScratchTableAndProceduresAsync(tableName, insertProcName, countProcName);

            try
            {
                DBUtilityMySQL dbUtility = new DBUtilityMySQL();
                await using (IDbUnitOfWork unitOfWork = await dbUtility.BeginUnitOfWorkAsync(ConnectionString))
                {
                    await unitOfWork.ExecuteAsync(insertProcName, new[] { new MySqlParameter("in_value", "not committed") });
                    // No CommitAsync call - disposal must roll back.
                }

                Assert.AreEqual(0L, await CountRowsAsync(countProcName), "A unit of work disposed without CommitAsync must roll back its write.");
            }
            finally
            {
                await DropScratchObjectsAsync(tableName, insertProcName, countProcName);
            }
        }
    }
}
