using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DisksMonitoring.OS.DisksFinding.LshwParsing
{
    class LshwNode
    {
        private readonly List<LshwNode> children = new List<LshwNode>();

        public LshwNode(string name)
        {
            Name = name;
            _ = Enum.TryParse(name, true, out NodeType type)
                || (name.Contains(':') && Enum.TryParse(name.Substring(0, name.IndexOf(':')), true, out type))
                || Enum.TryParse(Regex.Replace(name, @"\d+", string.Empty), true, out type);
            Type = type;
        }

        public LshwNode Parent { get; private set; } = null;
        public IList<LshwNode> Children { get => children; }
        public NodeType Type { get; }
        public string Name { get; }
        public List<Token> Tokens { get; } = new List<Token>();

        public void AddChild(LshwNode node)
        {
            node.Parent = this;
            Children.Add(node);
        }

        public string FindSingleValue(Func<Token, bool> f) => Tokens.SingleOrDefault(f).Value;

        public string FindSingleValue(TokenType type) => FindSingleValue(t => t.Type == type);

        public override string ToString()
        {
            return $"{Name} ({Enum.GetName(typeof(NodeType), Type)}), {Children.Count} children";
        }
    }
}
