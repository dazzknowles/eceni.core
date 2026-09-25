namespace Eceni.Core.Secrets.Abstract
{
    /// <summary>
    /// Implemented by packages that retrieve non-secret variables from a particular source.
    /// </summary>
    public interface IVariableProvider
    {
        string ProviderName { get; }
        string GetVariable(string name);
    }
}
