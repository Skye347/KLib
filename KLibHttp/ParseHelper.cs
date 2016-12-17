using System;
using System.Collections.Generic;
using System.Linq;

namespace KLib.HTTP.ParseHelper
{
    public static class ByteOp
    {
        public static int PatternFind(byte[] data, byte[] pattern)
        {
            return PatternFind(data, pattern, 0);
        }

        public static int PatternFind(byte[] data, byte[] pattern, int start)
        {
            for (int i = start; i < data.Length; i++)
            {
                if (data[i] == pattern[0])
                {
                    var paritalArray = (IEnumerable<byte>)(new ArraySegment<byte>(data, i, pattern.Length));
                    if (pattern.SequenceEqual(paritalArray))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public static byte[] SubArray(byte[] data, int start, int length)
        {
            var paritalArray = (IEnumerable<byte>)(new ArraySegment<byte>(data, start, length));
            return paritalArray.ToArray();
        }
    }

    public class StringOp
    {
        //http://stackoverflow.com/questions/2606368/how-to-get-specific-line-from-a-string-in-c
        //public static string GetLine(string text, int lineNo)
        //{
        //    string[] lines = text.Replace("\r", "").Split('\n');
        //    return lines.Length >= lineNo ? lines[lineNo - 1] : null;
        //}
        private string _text;
        private string[] _lines;

        public void LoadText(string text)
        {
            _text = text;
        }

        public void ClearText()
        {
            _text = null;
        }

        public string GetLine(int lineNo)
        {
            if (_lines == null)
            {
                _lines = _text.Replace("\r", "").Split('\n');
            }
            return _lines.Length >= lineNo ? _lines[lineNo - 1] : null;
        }
    }

    public static class ContentOp
    {
        public static bool isBinaryType(string contentType)
        {
            return !contentType.Contains("text");
        }
    }
}