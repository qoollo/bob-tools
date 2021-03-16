using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.LshwParsing
{
    class LshwParser
    {
        delegate bool OptionalParser(string line, out Token token);
        private static readonly List<OptionalParser> parsers = new List<OptionalParser>
        {
            CreateEOLParser(TokenType.Header, "*-"),
            CreateEOLParser(TokenType.LogicalName, "logical name:"),
            CreateEOLParser(TokenType.PhysicalId, "physical id:"),
            CreateEOLParser(TokenType.Serial, "serial:"),
            CreateSpaceParser(TokenType.GUID, "guid="),
            CreateSpaceParser(TokenType.LastMountPoint, "lastmountpoint="),
            CreateSpaceParser(TokenType.State, "state="),
            CreateSpaceParser(TokenType.MountFsType, "mount.fstype="),
            CreateSpaceParser(TokenType.Filesystem, "filesystem=")
        };

        public List<LshwNode> Parse(IList<string> lines)
        {
            var tokens = Tokenize(lines);
            var res = new List<LshwNode>();
            int i = 0;
            while (i < tokens.Count)
                res.AddRange(ParseHeader(tokens, ref i));
            return res;
        }

        private List<LshwNode> ParseHeader(List<Token> tokens, ref int pos)
        {
            if (tokens[pos].Type != TokenType.Header)
                throw new Exception("Expected header");

            var res = new List<LshwNode>();

            res.Add(new LshwNode(tokens[pos].Value));

            var topIndent = tokens[pos].Indentation;
            pos++;
            for (; pos < tokens.Count && tokens[pos].Type != TokenType.Header; pos++)
                res[0].Tokens.Add(tokens[pos]);

            while (pos < tokens.Count && tokens[pos].Indentation > topIndent)
            {
                var children = ParseHeader(tokens, ref pos);
                res[0].AddChild(children[0]);
                res.AddRange(children);
            }
            return res;
        }


        private List<Token> Tokenize(IList<string> lines)
        {
            var res = new List<Token>();
            foreach (var line in lines)
            {
                foreach (var parser in parsers)
                    if (parser(line, out var token))
                        res.Add(token);
            }
            return res;
        }

        private static OptionalParser CreateEOLParser(TokenType type, string start)
        {
            bool f(string line, out Token token)
            {
                token = new Token();
                var pos = line.IndexOf(start);
                if (pos < 0)
                    return false;

                token = new Token(type, line.Substring(pos + start.Length).Trim(), pos);
                return true;
            }
            return f;
        }

        private static OptionalParser CreateSpaceParser(TokenType type, string start)
        {
            bool f(string line, out Token token)
            {
                token = new Token();
                var startPos = line.IndexOf(start);
                if (startPos < 0)
                    return false;

                var end = line.IndexOf(' ', startPos);
                if (end < 0)
                    end = line.Length;
                int length = end - startPos - start.Length;
                token = new Token(type, line.Substring(startPos + start.Length, length), startPos);

                return true;
            };
            return f;
        }
    }
}
