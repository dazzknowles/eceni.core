using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Encryption.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Eceni.Core.Base.Database.Common
{
    /// <summary>
    /// ORM related data types
    /// </summary>
    public class DBUtilityCommon
    {
        private static readonly ConcurrentDictionary<string, TypeInfo[]> _typeInfoCache = new ConcurrentDictionary<string, TypeInfo[]>();

        /// <summary>
        /// Dispatches a raw DB value to the correct boolean conversion. A native <see cref="bool"/>
        /// is used directly; a byte array (e.g. a MySQL bit) is interpreted as bytes; anything else is
        /// interpreted via its string representation.
        /// </summary>
        /// <param name="value"></param>
        private static bool EceniBooleanConversion(object value)
        {
            if (value is bool)
            {
                return (bool)value;
            }

            if (value is byte[])
            {
                return EceniBooleanConversion((byte[])value);
            }

            return EceniBooleanConversion(value.ToString().Trim());
        }

        /// <summary>
        /// Conversion of verbose DB representations of C# Boolean type in byte format
        /// </summary>
        /// <param name="value"></param>
        private static bool EceniBooleanConversion(byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot convert an empty byte array to a boolean.");
            }

            byte first = value[0];

            // A numeric bit byte (0 / 1) is the common case for MySQL BIT columns.
            if (first == 0)
            {
                return false;
            }

            if (first == 1)
            {
                return true;
            }

            // Otherwise treat the byte as an ASCII-encoded token ('0'/'1'/'y'/'n'/...) and delegate
            // to the string form, which understands the verbose representations.
            return EceniBooleanConversion(((char)first).ToString());
        }

        /// <summary>
        ///  Conversion of verbose DB representations of C# Boolean type from ToString()
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool EceniBooleanConversion(string value)
        {
            string[] yarps = { "true", "yes", "y", "1", "-1" };
            string[] narps = { "false", "no", "n", "0" };

            if (yarps.Contains(value.ToLower()))
            {
                return true;
            }
            else if (narps.Contains(value.ToLower()))
            {
                return false;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value is not a recognised boolean representation.");
            }
        }

        /// <summary>
        /// Handle TimeSpan as DB connector will attempt to parse as System.DateTime
        /// and thus encounter overflows (MySQL TIME fields have a range of -838:59:59 to 838:59:59
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static TimeSpan EceniTimeSpanConversion(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new TimeSpan();
            }

            TimeSpan.TryParse(value, out TimeSpan result);

            return result;
        }

        /// <summary>
        /// Converts a raw DB value to the given (non-nullable) target property type without a lossy,
        /// culture-sensitive string round-trip. When the runtime value is already the target type it
        /// is assigned directly (preserving native precision for decimal/DateTime etc.); otherwise it
        /// is converted with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>, falling
        /// back to a <see cref="System.ComponentModel.TypeConverter"/> (also invariant) only when a
        /// direct/IConvertible conversion is not available.
        /// </summary>
        /// <param name="rawValue">The non-null value read from the data source.</param>
        /// <param name="targetType">The non-nullable CLR type to convert to.</param>
        private static object ConvertToPropertyType(object rawValue, Type targetType)
        {
            if (targetType == typeof(bool))
            {
                return EceniBooleanConversion(rawValue);
            }

            if (targetType == typeof(TimeSpan))
            {
                if (rawValue is TimeSpan)
                {
                    return rawValue;
                }

                return EceniTimeSpanConversion(rawValue.ToString().Trim());
            }

            if (targetType == typeof(string))
            {
                return rawValue.ToString().Trim();
            }

            // Already the target type (or a subtype) - assign directly, preserving native precision.
            if (targetType.IsInstanceOfType(rawValue))
            {
                return rawValue;
            }

            // Enums accept both their numeric and their name form; the EnumConverter handles both.
            if (targetType.IsEnum)
            {
                return System.ComponentModel.TypeDescriptor.GetConverter(targetType)
                    .ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, rawValue.ToString().Trim());
            }

            // Convertible primitives / decimal / DateTime / etc. - invariant, no lossy string round-trip.
            if (rawValue is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
            {
                return Convert.ChangeType(rawValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }

            // Last resort (e.g. Guid from a string): TypeConverter, but with InvariantCulture.
            return System.ComponentModel.TypeDescriptor.GetConverter(targetType)
                .ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, rawValue.ToString().Trim());
        }

        /// <summary>
        /// Accepts a list of IDataRecord and iterates through to bind to model via Data Attribute ColumnName
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="records"></param>
        /// <param name="prefix"></param>
        /// <returns>List of generic object model T</returns>
        public static IEnumerable<T> ModelFromIDataRecordList<T>(IEnumerable<IDataRecord> records, string prefix = null, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            List<T> results = new List<T>();

            foreach (IDataRecord record in records)
            {
                results.Add(ModelFromIDataRecord<T>(record, prefix, encryptionProvider));
            }

            return results;
        }

        /// <summary>
        /// Accepts a list of IDataRecord and iterates through to bind to model via Data Attribute ColumnName
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="records"></param>
        /// <param name="prefix"></param>
        /// <returns>List of generic object model T</returns>
        public static async IAsyncEnumerable<T> ModelFromIDataRecordList<T>(IAsyncEnumerable<IDataRecord> records, string prefix = null, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            await foreach (IDataRecord record in records)
            {
                yield return ModelFromIDataRecord<T>(record, prefix, encryptionProvider);
            }
        }

        /// <summary>
        /// Accepts a IDataRecord and iterates through to bind to model via Data Attribute ColumnName
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="record"></param>
        /// <param name="prefix"></param>
        /// <returns>Generic object model T</returns>
        public static T ModelFromIDataRecord<T>(IDataRecord record, string prefix = null, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            T result = new T();

            // detect results with "." separation i.e. TestData.DataItem
            if (prefix == null || string.IsNullOrEmpty(prefix))
            {
                prefix = typeof(T).Name + ".";
            }
            if (!prefix.EndsWith("."))
            {
                prefix += ".";
            }

            TypeInfo[] properties = GetTypeInfo(result.GetType());

            foreach (TypeInfo typeInfo in properties)
            {
                bool isEncrypted = encryptionProvider != null && typeInfo.PropertyInfo.CustomAttributes.Any(i => i.AttributeType == typeof(EncryptedAttribute));

                string unPrefixedColumnName = typeInfo.ColumnName;
                if (unPrefixedColumnName == null)
                {
                    continue;
                }
                PropertyInfo prop = typeInfo.PropertyInfo;

                string columnName = prefix + unPrefixedColumnName;

                try
                {
                    for (int i = 0; i < record.FieldCount; i++)
                    {
                        if (columnName != record.GetName(i))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(record.GetName(i)) || record.IsDBNull(i))
                        {
                            prop.SetValue(result, null, null);
                        }
                        else
                        {
                            object rawValue = record[columnName];

                            if (isEncrypted && rawValue != null)
                            {
                                string decrypted = encryptionProvider.Decrypt(rawValue.ToString());

                                if (prop.PropertyType == typeof(string))
                                {
                                    prop.SetValue(result, decrypted, null);
                                }
                                else if (decrypted == null)
                                {
                                    prop.SetValue(result, null, null);
                                }
                                else
                                {
                                    Type encryptedTargetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                    prop.SetValue(result, ConvertToPropertyType(decrypted, encryptedTargetType), null);
                                }

                                break;
                            }

                            if (rawValue == null)
                            {
                                prop.SetValue(result, null, null);
                            }
                            else
                            {
                                Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                prop.SetValue(result, ConvertToPropertyType(rawValue, targetType), null);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Error deserialising {columnName} from the database. See the inner exception for more details.", e);
                }
            }

            return result;
        }

        /// <summary>
        /// Used to map ADO DataRow to an object
        /// </summary>
        /// <typeparam name="T">Entity to map</typeparam>
        /// <param name="dataRow"></param>
        /// <param name="prefix">For example "ClassName" will be used to map fields as "ClassName.SomeField"</param>
        /// <returns></returns>
        public static T ModelFromDataRow<T>(DataRow dataRow, string prefix = null, IEncryptionProvider encryptionProvider = null) where T : new()
        {
            T result = new T();
            if (prefix == null || string.IsNullOrEmpty(prefix))
            {
                prefix = typeof(T).Name + ".";
            }

            if (!prefix.EndsWith("."))
            {
                prefix += ".";
            }

            TypeInfo[] properties = GetTypeInfo(result.GetType());

            foreach (TypeInfo typeInfo in properties)
            {
                bool isEncrypted = encryptionProvider != null && typeInfo.PropertyInfo.CustomAttributes.Any(i => i.AttributeType == typeof(EncryptedAttribute));

                string unPrefixedColumnName = typeInfo.ColumnName;
                if (unPrefixedColumnName == null)
                {
                    continue;
                }

                PropertyInfo prop = typeInfo.PropertyInfo;

                string columnName = prefix + unPrefixedColumnName;

                if (dataRow.Table.Columns.Contains(columnName))
                {
                    if (dataRow.IsNull(columnName))
                    {
                        prop.SetValue(result, null, null);
                    }
                    else
                    {
                        object rawValue = dataRow[columnName];

                        if (isEncrypted && rawValue != null)
                        {
                            string decrypted = encryptionProvider.Decrypt(rawValue.ToString());

                            if (prop.PropertyType == typeof(string))
                            {
                                prop.SetValue(result, decrypted, null);
                            }
                            else if (decrypted == null)
                            {
                                prop.SetValue(result, null, null);
                            }
                            else
                            {
                                Type encryptedTargetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                prop.SetValue(result, ConvertToPropertyType(decrypted, encryptedTargetType), null);
                            }

                            continue;
                        }

                        if (rawValue == null)
                        {
                            prop.SetValue(result, null, null);
                        }
                        else
                        {
                            Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            prop.SetValue(result, ConvertToPropertyType(rawValue, targetType), null);
                        }
                    }
                }
            }

            return result;
        }

        private static string GetCustomAttributes(PropertyInfo prop)
        {
            Attribute[] attributes = ((Attribute[])prop.GetCustomAttributes(typeof(ColumnNameAttribute), true))
                .Where(l => l.GetType() == typeof(ColumnNameAttribute)).ToArray();

            //Can only have one ColumnNameAttribute
            Attribute attr = attributes.FirstOrDefault();
            if (attr == null)
            {
                return null;
            }

            string columnName = ((ColumnNameAttribute)attr).Name;

            if (string.IsNullOrEmpty(columnName))
            {
                columnName = prop.Name;
            }

            return columnName;
        }

        /// <summary>
        /// Get Cached Properties Info
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<PropertyInfo> GetPropertyInfo(Type type)
        {
            return GetTypeInfo(type).Select(m => m.PropertyInfo).ToList();
        }

        private static TypeInfo[] GetTypeInfo(Type type)
        {
            //Caching the type info because retrieving the entry from a dictionary
            //is over 10x faster than performing reflection

            string key = type.FullName;

            if (_typeInfoCache.TryGetValue(key, out TypeInfo[] ret))
            {
                return ret;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            List<TypeInfo> typeInfos = new List<TypeInfo>();

            for (int i = 0; i < properties.Length; i++)
            {
                string columnName = GetCustomAttributes(properties[i]);
                if (columnName == null)
                {
                    continue;
                }

                typeInfos.Add(new TypeInfo
                {
                    ColumnName = GetCustomAttributes(properties[i]),
                    PropertyInfo = properties[i]
                });
            }

            TypeInfo[] typeInfosArray = typeInfos.ToArray();
            _typeInfoCache[key] = typeInfosArray;
            return typeInfosArray;
        }

        /// <summary>
        /// Checks given Model T and if null or nothing returns a SQL Null value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="x"></param>
        /// <returns>Anonymous object</returns>
        public static object DbNullIfNothing<T>(T x)
        {
            if (x == null)
            {
                return DBNull.Value;
            }
            return x;
        }

        /// <summary>
        /// Handles null or empty strings
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static object DbNullIfNullOrEmpty(string s)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrWhiteSpace(s))
            {
                return DBNull.Value;
            }

            return s;
        }

        /// <summary>
        /// Handles nullable DateTimes
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static object DbNullIfNullOrEmpty(DateTime? dt)
        {
            if (!dt.HasValue)
            {
                return DBNull.Value;
            }

            return dt;
        }

        /// <summary>
        /// Handles default numeric values
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="x"></param>
        /// <returns></returns>
        public static object DbNullIfZero<T>(T x)
        {
            if (x == null)
            {
                return DBNull.Value;
            }

            // Compare numerically against the type's zero without a culture-sensitive string parse.
            if (x is float || x is double)
            {
                double asDouble = Convert.ToDouble(x, System.Globalization.CultureInfo.InvariantCulture);
                if (asDouble == 0d)
                {
                    return DBNull.Value;
                }

                return x;
            }

            if (x is byte || x is sbyte || x is short || x is ushort || x is int || x is uint
                || x is long || x is ulong || x is decimal)
            {
                decimal asDecimal = Convert.ToDecimal(x, System.Globalization.CultureInfo.InvariantCulture);
                if (asDecimal == 0m)
                {
                    return DBNull.Value;
                }

                return x;
            }

            // Non-numeric types (Guid, string, DateTime, ...) have no "zero" concept - pass through.
            return x;
        }

        private class TypeInfo
        {
            public string ColumnName;
            public PropertyInfo PropertyInfo;
        }
    }
}
