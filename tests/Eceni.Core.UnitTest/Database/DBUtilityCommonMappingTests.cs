using System;
using System.Data;
using System.Globalization;
using NSubstitute;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Database.Common;
using Eceni.Core.Base.Encryption.Abstract;

namespace Eceni.Core.UnitTest.Database
{
    /// <summary>
    /// Covers <see cref="DBUtilityCommon"/>'s reflection-based data-mapping behaviour: native-precision value
    /// conversion (no lossy culture-sensitive string round-trip), byte-encoded boolean columns, decrypt-then-convert
    /// for <c>[Encrypted]</c> non-string columns, and the numeric (not string-parsing) <c>DbNullIfZero</c> check.
    /// </summary>
    [TestClass]
    public class DBUtilityCommonMappingTests
    {
        private const string Prefix = "E";

        // ---- Test entities ----

        private sealed class DateEntity
        {
            [ColumnName("WhenValue")]
            public DateTime WhenValue { get; set; }
        }

        private sealed class DecimalEntity
        {
            [ColumnName("AmountValue")]
            public decimal AmountValue { get; set; }
        }

        private sealed class BoolEntity
        {
            [ColumnName("Flag")]
            public bool Flag { get; set; }
        }

        private sealed class EncryptedIntEntity
        {
            [ColumnName("Secret")]
            [Encrypted]
            public int Secret { get; set; }
        }

        private sealed class EncryptedStringEntity
        {
            [ColumnName("SecretText")]
            [Encrypted]
            public string SecretText { get; set; }
        }

        // ---- Helpers ----

        private static DataTable SingleColumnTable(string unprefixedColumn, Type columnType, object value)
        {
            DataTable table = new DataTable();
            string columnName = Prefix + "." + unprefixedColumn;
            table.Columns.Add(columnName, columnType);
            DataRow row = table.NewRow();
            row[columnName] = value;
            table.Rows.Add(row);
            return table;
        }

        private static T MapViaDataRow<T>(DataTable table, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            return DBUtilityCommon.ModelFromDataRow<T>(table.Rows[0], Prefix, encryptionProvider);
        }

        private static T MapViaDataRecord<T>(DataTable table, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            using (DataTableReader reader = table.CreateDataReader())
            {
                reader.Read();
                return DBUtilityCommon.ModelFromIDataRecord<T>(reader, Prefix, encryptionProvider);
            }
        }

        private static void RunWithCulture(string cultureName, Action action)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        // ---- Native precision preserved (DateTime sub-second) ----

        [TestMethod]
        public void ModelFromDataRow_DateTimeWithSubSecondPrecision_IsPreserved()
        {
            DateTime value = new DateTime(2023, 5, 17, 13, 45, 30, 123).AddTicks(4567);
            DataTable table = SingleColumnTable("WhenValue", typeof(DateTime), value);

            DateEntity result = MapViaDataRow<DateEntity>(table);

            Assert.AreEqual(value, result.WhenValue, "DateTime precision (milliseconds/ticks) must survive mapping.");
        }

        [TestMethod]
        public void ModelFromIDataRecord_DateTimeWithSubSecondPrecision_IsPreserved()
        {
            DateTime value = new DateTime(2023, 5, 17, 13, 45, 30, 123).AddTicks(4567);
            DataTable table = SingleColumnTable("WhenValue", typeof(DateTime), value);

            DateEntity result = MapViaDataRecord<DateEntity>(table);

            Assert.AreEqual(value, result.WhenValue, "DateTime precision must survive the IDataRecord mapping path too.");
        }

        // ---- Invariant parsing of string values under a comma-decimal culture ----

        [TestMethod]
        public void ModelFromDataRow_InvariantDecimalString_UnderCommaCulture_ParsesInvariantly()
        {
            RunWithCulture("de-DE", () =>
            {
                DataTable table = SingleColumnTable("AmountValue", typeof(string), "1234.56");

                DecimalEntity result = MapViaDataRow<DecimalEntity>(table);

                Assert.AreEqual(1234.56m, result.AmountValue, "An invariant-formatted decimal string must parse to 1234.56 regardless of the current culture.");
            });
        }

        [TestMethod]
        public void ModelFromIDataRecord_InvariantDecimalString_UnderCommaCulture_ParsesInvariantly()
        {
            RunWithCulture("de-DE", () =>
            {
                DataTable table = SingleColumnTable("AmountValue", typeof(string), "1234.56");

                DecimalEntity result = MapViaDataRecord<DecimalEntity>(table);

                Assert.AreEqual(1234.56m, result.AmountValue, "The IDataRecord path must parse invariant decimal strings invariantly too.");
            });
        }

        // ---- Byte-encoded booleans ----

        [TestMethod]
        public void ModelFromDataRow_BoolFromAsciiOneByte_ReturnsTrue()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[] { 49 });

            BoolEntity result = MapViaDataRow<BoolEntity>(table);

            Assert.IsTrue(result.Flag, "ASCII '1' (byte 49) must map to true.");
        }

        [TestMethod]
        public void ModelFromDataRow_BoolFromAsciiZeroByte_ReturnsFalse()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[] { 48 });

            BoolEntity result = MapViaDataRow<BoolEntity>(table);

            Assert.IsFalse(result.Flag, "ASCII '0' (byte 48) must map to false.");
        }

        [TestMethod]
        public void ModelFromDataRow_BoolFromNumericBitByteOne_ReturnsTrue()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[] { 1 });

            BoolEntity result = MapViaDataRow<BoolEntity>(table);

            Assert.IsTrue(result.Flag, "A numeric bit byte of 1 must map to true.");
        }

        [TestMethod]
        public void ModelFromDataRow_BoolFromNumericBitByteZero_ReturnsFalse()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[] { 0 });

            BoolEntity result = MapViaDataRow<BoolEntity>(table);

            Assert.IsFalse(result.Flag, "A numeric bit byte of 0 must map to false.");
        }

        [TestMethod]
        public void ModelFromIDataRecord_BoolFromAsciiOneByte_ReturnsTrue()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[] { 49 });

            BoolEntity result = MapViaDataRecord<BoolEntity>(table);

            Assert.IsTrue(result.Flag, "The IDataRecord path must also interpret byte-encoded booleans.");
        }

        [TestMethod]
        public void ModelFromDataRow_BoolFromEmptyByteArray_ThrowsWithValueParamName()
        {
            DataTable table = SingleColumnTable("Flag", typeof(byte[]), new byte[0]);

            ArgumentOutOfRangeException ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => MapViaDataRow<BoolEntity>(table));

            Assert.AreEqual("value", ex.ParamName, "The exception must name the real parameter.");
        }

        // ---- Encrypted non-string columns are decrypted then converted ----

        [TestMethod]
        public void ModelFromDataRow_EncryptedIntColumn_IsDecryptedThenConverted()
        {
            IEncryptionProvider encryptionProvider = Substitute.For<IEncryptionProvider>();
            encryptionProvider.Decrypt("cipher").Returns("42");
            DataTable table = SingleColumnTable("Secret", typeof(string), "cipher");

            EncryptedIntEntity result = MapViaDataRow<EncryptedIntEntity>(table, encryptionProvider);

            Assert.AreEqual(42, result.Secret, "An [Encrypted] int column must be decrypted and then converted to int.");
            encryptionProvider.Received(1).Decrypt("cipher");
        }

        [TestMethod]
        public void ModelFromIDataRecord_EncryptedIntColumn_IsDecryptedThenConverted()
        {
            IEncryptionProvider encryptionProvider = Substitute.For<IEncryptionProvider>();
            encryptionProvider.Decrypt("cipher").Returns("42");
            DataTable table = SingleColumnTable("Secret", typeof(string), "cipher");

            EncryptedIntEntity result = MapViaDataRecord<EncryptedIntEntity>(table, encryptionProvider);

            Assert.AreEqual(42, result.Secret, "The IDataRecord path must also decrypt-then-convert an [Encrypted] non-string column.");
            encryptionProvider.Received(1).Decrypt("cipher");
        }

        [TestMethod]
        public void ModelFromDataRow_EncryptedStringColumn_StillDecrypts()
        {
            IEncryptionProvider encryptionProvider = Substitute.For<IEncryptionProvider>();
            encryptionProvider.Decrypt("cipherText").Returns("plaintext");
            DataTable table = SingleColumnTable("SecretText", typeof(string), "cipherText");

            EncryptedStringEntity result = MapViaDataRow<EncryptedStringEntity>(table, encryptionProvider);

            Assert.AreEqual("plaintext", result.SecretText, "An [Encrypted] string column must still be decrypted.");
        }

        // ---- DbNullIfZero without a culture-sensitive parse ----

        [TestMethod]
        public void DbNullIfZero_NonZeroDecimal_ReturnsValue()
        {
            object result = DBUtilityCommon.DbNullIfZero(1234.56m);

            Assert.AreEqual(1234.56m, result, "A non-zero decimal must be returned unchanged.");
        }

        [TestMethod]
        public void DbNullIfZero_ZeroDecimal_ReturnsDbNull()
        {
            object result = DBUtilityCommon.DbNullIfZero(0m);

            Assert.AreEqual(DBNull.Value, result, "A zero decimal must map to DBNull.");
        }

        [TestMethod]
        public void DbNullIfZero_LongBeyondIntRange_ReturnsValue()
        {
            long value = 5000000000L;

            object result = DBUtilityCommon.DbNullIfZero(value);

            Assert.AreEqual(value, result, "A long outside int range must be returned unchanged, not overflow.");
        }

        [TestMethod]
        public void DbNullIfZero_ZeroLong_ReturnsDbNull()
        {
            object result = DBUtilityCommon.DbNullIfZero(0L);

            Assert.AreEqual(DBNull.Value, result, "A zero long must map to DBNull.");
        }

        [TestMethod]
        public void DbNullIfZero_NonZeroDouble_ReturnsValue()
        {
            object result = DBUtilityCommon.DbNullIfZero(3.14d);

            Assert.AreEqual(3.14d, result, "A non-zero double must be returned unchanged.");
        }

        [TestMethod]
        public void DbNullIfZero_Guid_ReturnsValue()
        {
            Guid value = Guid.NewGuid();

            object result = DBUtilityCommon.DbNullIfZero(value);

            Assert.AreEqual(value, result, "A Guid must be returned unchanged rather than throwing.");
        }

        [TestMethod]
        public void DbNullIfZero_ZeroInt_ReturnsDbNull()
        {
            object result = DBUtilityCommon.DbNullIfZero(0);

            Assert.AreEqual(DBNull.Value, result, "A zero int must still map to DBNull.");
        }

        [TestMethod]
        public void DbNullIfZero_NonZeroInt_ReturnsValue()
        {
            object result = DBUtilityCommon.DbNullIfZero(5);

            Assert.AreEqual(5, result, "A non-zero int must be returned unchanged.");
        }
    }
}
