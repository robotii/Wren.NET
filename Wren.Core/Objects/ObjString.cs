using System;
using System.Collections.Generic;
using System.Text;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjString : Obj
    {
        private static readonly List<ObjString> strings = new List<ObjString>();
        private static bool initCompleted;

        public static void InitClass()
        {
            foreach (ObjString s in strings)
            {
                s.ClassObj = WrenVM.StringClass;
            }
            initCompleted = true;
            strings.Clear();
        }

        // Inline array of the string's bytes followed by a null terminator.

        public ObjString(string s)
        {
            Value = s;
            ClassObj = WrenVM.StringClass;
            if (!initCompleted)
                strings.Add(this);
        }

        public readonly string Value;

        public override string ToString()
        {
            return Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        // Creates a new string containing the UTF-8 encoding of [value].
        public static Value FromCodePoint(int v)
        {
            return new Value("" + Convert.ToChar(v));
        }

        // Creates a new string containing the code point in [string] starting at byte
        // [index]. If [index] points into the middle of a UTF-8 sequence, returns an
        // empty string.
        public Value CodePointAt(int index)
        {
            return index > Value.Length ? new Value() : new Value(Value[index]);
        }

        public byte[] GetBytes()
        {
            return Encoding.UTF8.GetBytes(Value);
        }
    }
}
