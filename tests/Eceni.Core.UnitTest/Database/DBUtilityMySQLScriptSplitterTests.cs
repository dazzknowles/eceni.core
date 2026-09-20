using System.Collections.Generic;
using System.Linq;
using Eceni.Core.Database.MySQL;

namespace Eceni.Core.UnitTest.Database
{
    /// <summary>
    /// Proves <see cref="DBUtilityMySQL"/>'s statement splitter (used by <c>ExecuteScriptAsync</c>) ignores a
    /// separator hidden inside a string literal, quoted identifier, or comment, and honours the client-side
    /// <c>DELIMITER</c> directive so a routine body (with its own internal semicolons) survives as one statement.
    /// </summary>
    [TestClass]
    public class DBUtilityMySQLScriptSplitterTests
    {
        [TestMethod]
        public void SplitsTopLevelStatements()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 1; SELECT 2; SELECT 3").ToList();

            Assert.AreEqual(3, parts.Count);
            Assert.AreEqual("SELECT 1", parts[0]);
            Assert.AreEqual(" SELECT 2", parts[1]);
            Assert.AreEqual(" SELECT 3", parts[2]);
        }

        [TestMethod]
        public void SemicolonInsideSingleQuotedLiteral_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("INSERT INTO t VALUES ('a;b'); SELECT 1").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("INSERT INTO t VALUES ('a;b')", parts[0]);
            Assert.AreEqual(" SELECT 1", parts[1]);
        }

        [TestMethod]
        public void SemicolonInsideDoubleQuotedLiteral_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT \"a;b\"; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT \"a;b\"", parts[0]);
        }

        [TestMethod]
        public void SemicolonInsideBacktickIdentifier_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT `we;ird`; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT `we;ird`", parts[0]);
        }

        [TestMethod]
        public void BackslashEscapedQuote_DoesNotEndLiteral()
        {
            // The backslash escapes the quote, so the literal continues and the ';' inside it is not a separator.
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 'it\\'s; still one'; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT 'it\\'s; still one'", parts[0]);
        }

        [TestMethod]
        public void SemicolonInDoubleDashComment_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 1 -- a;b\n; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT 1 -- a;b\n", parts[0]);
        }

        [TestMethod]
        public void SemicolonInHashComment_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 1 # a;b\n; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
        }

        [TestMethod]
        public void SemicolonInBlockComment_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 1 /* a; b; c */; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT 1 /* a; b; c */", parts[0]);
        }

        [TestMethod]
        public void BareDoubleDashWithoutSpace_IsNotAComment()
        {
            // MySQL requires whitespace after "--" for it to be a comment; "5--6" is arithmetic, so the ';' splits.
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 5--6; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT 5--6", parts[0]);
        }

        [TestMethod]
        public void TrailingStatementWithoutSemicolon_IsReturned()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("SELECT 1; SELECT 2").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual(" SELECT 2", parts[1]);
        }

        [TestMethod]
        public void EmptyOrNull_YieldsNoStatements()
        {
            Assert.AreEqual(0, DBUtilityMySQL.SplitSqlStatements(string.Empty).ToList().Count);
            Assert.AreEqual(0, DBUtilityMySQL.SplitSqlStatements(null).ToList().Count);
        }

        [TestMethod]
        public void DelimiterDirective_KeepsRoutineBodyIntactAndResets()
        {
            string script =
                "DELIMITER $$\n" +
                "CREATE PROCEDURE foo() BEGIN SELECT 1; SELECT 2; END$$\n" +
                "DELIMITER ;\n" +
                "SELECT 3;";

            List<string> parts = DBUtilityMySQL.SplitSqlStatements(script).ToList();

            // The whole routine (semicolons and all) is one statement; the trailing SELECT is the second. The two
            // DELIMITER lines are consumed, not emitted.
            Assert.AreEqual(2, parts.Count);
            StringAssert.Contains(parts[0], "CREATE PROCEDURE foo()");
            StringAssert.Contains(parts[0], "SELECT 1;");
            StringAssert.Contains(parts[0], "SELECT 2;");
            Assert.AreEqual("SELECT 3", parts[1].Trim());
        }

        [TestMethod]
        public void DelimiterDirective_IsCaseInsensitive()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("delimiter $$\nSELECT 1$$").ToList();

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("SELECT 1", parts[0].Trim());
        }

        [TestMethod]
        public void MultiCharacterSlashDelimiter_Splits()
        {
            // '//' is a delimiter here, not the start of a comment (only '/*' opens a comment).
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("DELIMITER //\nSELECT 1//SELECT 2//").ToList();

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("SELECT 1", parts[0].Trim());
            Assert.AreEqual("SELECT 2", parts[1].Trim());
        }

        [TestMethod]
        public void ActiveDelimiterInsideLiteral_IsNotASeparator()
        {
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("DELIMITER $$\nSELECT '$$' $$ SELECT 2$$").ToList();

            Assert.AreEqual(2, parts.Count);
            StringAssert.Contains(parts[0], "'$$'");
            Assert.AreEqual("SELECT 2", parts[1].Trim());
        }

        [TestMethod]
        public void SemicolonUnderNonSemicolonDelimiter_IsAnOrdinaryCharacter()
        {
            // Under a '$$' delimiter the routine's internal ';' must not split, so this is a single statement.
            List<string> parts = DBUtilityMySQL.SplitSqlStatements("DELIMITER $$\nSELECT 1; SELECT 2; SELECT 3$$").ToList();

            Assert.AreEqual(1, parts.Count);
            StringAssert.Contains(parts[0], "SELECT 1; SELECT 2; SELECT 3");
        }
    }
}
