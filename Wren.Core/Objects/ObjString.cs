using System;
using System.Collections.Generic;
using System.Text;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjString : Obj
    {
        private static readonly List<ObjString> Strings = new List<ObjString>();
        private static bool _initCompleted;

        public static void InitClass()
        {
            foreach (ObjString s in Strings)
            {
                s.ClassObj = WrenVM.StringClass;
            }
            _initCompleted = true;
            Strings.Clear();
        }

        // Inline array of the string's bytes followed by a null terminator.

        public ObjString(string s)
            : base(ObjType.Obj)
        {
            Str = s;
            ClassObj = WrenVM.StringClass;
            if (!_initCompleted)
                Strings.Add(this);
        }

        public readonly string Str;

        public override string ToString()
        {
            return Str;
        }

        public override int GetHashCode()
        {
            return Str.GetHashCode();
        }

        // Creates a new string containing the UTF-8 encoding of [value].
        public static Obj FromCodePoint(int v)
        {
            return MakeString("" + Convert.ToChar(v));
        }

        // Creates a new string containing the code point in [string] starting at byte
        // [index]. If [index] points into the middle of a UTF-8 sequence, returns an
        // empty string.
        public Obj CodePointAt(int index)
        {
            return index > Str.Length ? new Obj() : new Obj(Str[index]);
        }

        public byte[] GetBytes()
        {
            return Encoding.UTF8.GetBytes(Str);
        }
    }
}
