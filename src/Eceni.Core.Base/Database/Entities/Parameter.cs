namespace Eceni.Core.Base.Database.Entities
{
    public class Parameter
    {
        public virtual string Name { get; set; }
        public virtual object Value { get; set; }

        public Parameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => $"{Name} = {Value}";
    }
}
