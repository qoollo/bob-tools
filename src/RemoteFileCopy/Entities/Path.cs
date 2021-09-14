namespace RemoteFileCopy.Entites
{
    public class Path
    {
        internal Path(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }
}