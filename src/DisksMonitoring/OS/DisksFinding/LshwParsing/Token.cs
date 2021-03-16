using System;

namespace DisksMonitoring.OS.DisksFinding.LshwParsing
{
    public struct Token
    {
        public Token(TokenType type, string value, int indentation)
        {
            Type = type;
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Indentation = indentation;
        }

        public TokenType Type { get; }
        public string Value { get; }
        public int Indentation { get; }

        public override string ToString()
        {
            return Enum.GetName(typeof(TokenType), Type) + " " + Indentation + " \"" + Value + "\"";
        }
    }
}
