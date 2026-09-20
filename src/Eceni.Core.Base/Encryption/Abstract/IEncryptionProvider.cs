namespace Eceni.Core.Base.Encryption.Abstract
{
    /// <summary>
    /// Contract for field-level encryption at rest. No implementation ships in Eceni.Core yet — this exists so
    /// entities can be decorated with <see cref="Eceni.Core.Base.Database.Attributes.EncryptedAttribute"/> and
    /// <see cref="Eceni.Core.Base.Database.Common.DBUtilityCommon"/> can decrypt through it once a project needs
    /// one.
    /// </summary>
    public interface IEncryptionProvider
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedText);
    }
}
