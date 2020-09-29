using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiskStatusAnalyzer
{
    public class TreeParser
    {
        public TreeParser(string parentDir, IEnumerable<string> treeOutputLines, bool dirsOnly)
        {
            var stack = new Stack<(Entry e, int level)>();
            foreach (var s in treeOutputLines)
            {
                if (!s.StartsWith('|'))
                    continue;
                int startIndex = s.IndexOf("-- ") + 3;
                var filename = s.Substring(startIndex);
                var level = s.Substring(0, startIndex).Count(c => c == ' ') / 3;
                while (stack.Count > 0 && stack.Peek().level >= level)
                    stack.Pop();
                var fullname = stack.Count > 0
                    ? stack.Peek().e.Path + "/" + filename
                    : parentDir + "/" + filename;
                var entry = new Entry(filename, fullname, dirsOnly);
                if (stack.Count == 0)
                    RootEntries.Add(entry);
                else if (stack.Count > 0)
                    stack.Peek().e.Children.Add(entry);
                stack.Push((entry, level));
            }
        }

        public List<Entry> RootEntries { get; } = new List<Entry>();

        public class Entry
        {
            public Entry(string name, string fullname, bool isDir)
            {
                Name = name;
                Path = fullname;
                Children = new List<Entry>();
                IsDir = isDir;
            }

            public string Name { get; }
            public string Path { get; }
            public List<Entry> Children { get; }
            public bool IsDir { get; }

            public override string ToString()
            {
                var res = new StringBuilder();
                res.Append(Path);
                foreach (var child in Children)
                    res.Append(Environment.NewLine + child.ToString());
                return res.ToString();
            }
        }
    }
}
