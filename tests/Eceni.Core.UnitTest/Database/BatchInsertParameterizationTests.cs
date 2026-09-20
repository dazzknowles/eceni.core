using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Database.MySQL;
using MySqlConnector;

namespace Eceni.Core.UnitTest.Database
{
    /// <summary>
    /// <see cref="DBUtilityMySQL.BatchInsertAsync{T}"/> must never embed a cell value as a SQL literal — every
    /// value is bound as its own <see cref="MySqlParameter"/>, and the table/column identifiers are still
    /// backtick-quoted so a delimiter (or otherwise attacker-controlled text) in either cannot break out.
    /// </summary>
    [TestClass]
    public class BatchInsertParameterizationTests
    {
        // ---- QuoteIdentifier / QuoteTableName ----

        [TestMethod]
        public void QuoteIdentifier_NormalName_IsBackticked()
        {
            Assert.AreEqual("`MyTable`", DBUtilityMySQL.QuoteIdentifier("MyTable"));
        }

        [TestMethod]
        public void QuoteIdentifier_EmbeddedBacktick_IsDoubled()
        {
            Assert.AreEqual("`Col``Name`", DBUtilityMySQL.QuoteIdentifier("Col`Name"));
        }

        [TestMethod]
        public void QuoteIdentifier_InjectionPayload_CannotBreakOut()
        {
            string escaped = DBUtilityMySQL.QuoteIdentifier("x`; DROP TABLE Users;--");

            Assert.AreEqual("`x``; DROP TABLE Users;--`", escaped);
        }

        [TestMethod]
        public void QuoteTableName_SchemaQualified_QuotesEachPart()
        {
            Assert.AreEqual("`mydb`.`MyTable`", DBUtilityMySQL.QuoteTableName("mydb.MyTable"));
        }

        [TestMethod]
        public void QuoteIdentifier_AlreadyQuoted_IsIdempotent()
        {
            Assert.AreEqual("`Foo`", DBUtilityMySQL.QuoteIdentifier("`Foo`"));
            Assert.AreEqual("`Col``Name`", DBUtilityMySQL.QuoteIdentifier("`Col``Name`"));
        }

        // ---- BuildParameterizedInsert: SQL shape and parameter binding ----

        private sealed class TwoColumnEntity : IBatchInsertable
        {
            public Guid BatchGuid { get; set; }

            [ColumnName]
            [BatchInsertColumn("Col`Name")]
            public int Delimited { get; set; }

            [ColumnName]
            [BatchInsertColumn("NormalCol")]
            public string Normal { get; set; }
        }

        private static (List<string> ColumnNames, List<PropertyInfo> Properties) DiscoverColumns()
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            List<string> columnNames = new List<string>();

            foreach (PropertyInfo property in typeof(TwoColumnEntity).GetProperties())
            {
                BatchInsertColumnAttribute attribute = property.GetCustomAttribute<BatchInsertColumnAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                columnNames.Add(attribute.Name ?? property.Name);
                properties.Add(property);
            }

            return (columnNames, properties);
        }

        [TestMethod]
        public void BuildParameterizedInsert_QuotesTableAndColumns_AndBindsEveryCellAsAParameter()
        {
            (List<string> columnNames, List<PropertyInfo> properties) = DiscoverColumns();

            object[] rows = new object[]
            {
                new TwoColumnEntity { Delimited = 1, Normal = "it's a value; DROP TABLE Users;--" },
                new TwoColumnEntity { Delimited = 2, Normal = null }
            };

            (string sql, MySqlParameter[] parameters) = DBUtilityMySQL.BuildParameterizedInsert("My`Table", columnNames, properties, rows);

            StringAssert.Contains(sql, "INSERT INTO `My``Table` (");
            StringAssert.Contains(sql, "`Col``Name`");
            StringAssert.Contains(sql, "`NormalCol`");

            // No raw cell value is ever embedded in the SQL text - the injection payload and the apostrophe must
            // not appear anywhere in the generated statement, only in a parameter's value.
            Assert.IsFalse(sql.Contains("DROP TABLE"), "A cell value must never be embedded as a SQL literal.");
            Assert.IsFalse(sql.Contains("'"), "Generated SQL must contain no string literals at all - everything is parameterized.");

            Assert.AreEqual(4, parameters.Length, "Two rows of two columns must produce four bound parameters.");
            Assert.AreEqual(1, parameters[0].Value);
            Assert.AreEqual("it's a value; DROP TABLE Users;--", parameters[1].Value);
            Assert.AreEqual(2, parameters[2].Value);
            Assert.AreEqual(DBNull.Value, parameters[3].Value, "A null cell must be bound as DBNull, not SQL NULL text.");

            // Every parameter placeholder referenced in the SQL must correspond to a bound parameter, and vice versa.
            foreach (MySqlParameter parameter in parameters)
            {
                StringAssert.Contains(sql, "@" + parameter.ParameterName);
            }
        }

        [TestMethod]
        public void BuildParameterizedInsert_RespectsBatchBoundary_RowAndColumnIndexedParameterNames()
        {
            (List<string> columnNames, List<PropertyInfo> properties) = DiscoverColumns();

            object[] rows = new object[]
            {
                new TwoColumnEntity { Delimited = 10, Normal = "a" },
                new TwoColumnEntity { Delimited = 20, Normal = "b" },
                new TwoColumnEntity { Delimited = 30, Normal = "c" }
            };

            (string sql, MySqlParameter[] parameters) = DBUtilityMySQL.BuildParameterizedInsert("T", columnNames, properties, rows);

            Assert.AreEqual(6, parameters.Length);
            CollectionAssert.AreEquivalent(
                new[] { "in_p0_0", "in_p0_1", "in_p1_0", "in_p1_1", "in_p2_0", "in_p2_1" },
                parameters.Select(p => p.ParameterName).ToArray());

            StringAssert.Contains(sql, "(@in_p0_0, @in_p0_1)");
            StringAssert.Contains(sql, "(@in_p1_0, @in_p1_1)");
            StringAssert.Contains(sql, "(@in_p2_0, @in_p2_1)");
        }

        // ---- End to end: BatchInsertAsync's generated SQL, captured via testRun ----

        [TestMethod]
        public async Task BatchInsertAsync_TestRun_PrintsParameterizedSqlAndBoundValues()
        {
            List<TwoColumnEntity> rows = new List<TwoColumnEntity>
            {
                new TwoColumnEntity { Delimited = 1, Normal = "plain" }
            };

            string output = await CaptureConsoleAsync(
                () => new DBUtilityMySQL().BatchInsertAsync(null, rows, "My`Table", 10, testRun: true));

            StringAssert.Contains(output, "INSERT INTO `My``Table` (");
            StringAssert.Contains(output, "`Col``Name`");
            StringAssert.Contains(output, "`NormalCol`");
            StringAssert.Contains(output, "in_p0_0 = 1");
            StringAssert.Contains(output, "in_p0_1 = plain");
        }

        private static async Task<string> CaptureConsoleAsync(Func<Task<Guid?>> action)
        {
            TextWriter original = Console.Out;
            StringWriter writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await action();
            }
            finally
            {
                Console.SetOut(original);
            }

            return writer.ToString();
        }
    }
}
