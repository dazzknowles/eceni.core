using Eceni.Core.Database.MySQL;

namespace Eceni.Core.UnitTest.Database
{
    /// <summary>
    /// The MySQL provider must recognise its engine's duplicate-key error numbers so a UNIQUE-constraint
    /// violation is mapped to the engine-agnostic <c>EceniUniqueConstraintException</c>. This tests the exact
    /// predicate used in the provider's catch filters.
    /// </summary>
    [TestClass]
    public class UniqueConstraintClassificationTests
    {
        [TestMethod]
        public void RecognisesDuplicateEntry()
        {
            Assert.IsTrue(DBUtilityMySQL.IsUniqueConstraintViolation(1062), "1062 (ER_DUP_ENTRY) is a unique-key violation.");
            Assert.IsTrue(DBUtilityMySQL.IsUniqueConstraintViolation(1586), "1586 (ER_DUP_ENTRY_WITH_KEY_NAME) is a unique-key violation reported with the key name.");
            Assert.IsTrue(DBUtilityMySQL.IsUniqueConstraintViolation(1022), "1022 (ER_DUP_KEY) is a duplicate-key violation on write.");
            Assert.IsTrue(DBUtilityMySQL.IsUniqueConstraintViolation(1169), "1169 (ER_DUP_UNIQUE) is a duplicate for a unique index that may not contain duplicates.");
        }

        [TestMethod]
        public void DoesNotMisclassifyOtherErrors()
        {
            Assert.IsFalse(DBUtilityMySQL.IsUniqueConstraintViolation(1452), "1452 is a foreign-key failure, not a unique violation.");
            Assert.IsFalse(DBUtilityMySQL.IsUniqueConstraintViolation(1213), "1213 is a deadlock, not a unique violation.");
            Assert.IsFalse(DBUtilityMySQL.IsUniqueConstraintViolation(0), "0 is not a unique violation.");
        }
    }
}
