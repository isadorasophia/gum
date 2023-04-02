namespace Whispers.Blackboards
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BlackboardAttribute : Attribute
    {
        public readonly string Name;

        public BlackboardAttribute(string name) => Name = name;
    }
}
