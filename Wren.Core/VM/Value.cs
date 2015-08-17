using System.Collections.Generic;
using Wren.Core.Objects;

namespace Wren.Core.VM
{
    public class ValueComparer : IEqualityComparer<Value>
    {
        public bool Equals(Value x, Value y)
        {
            return x != null && Value.Equals(x, y);
        }

        public int GetHashCode(Value obj)
        {
            return obj.GetHashCode();
        }
    }

    public class Value
    {
        public static Value Null = new Value(ValueType.Null);

        public Value()
        {
            Type = ValueType.Undefined;
        }

        public Value(ValueType t)
        {
            Type = t;
        }

        public Value(Value v)
        {
            Type = v.Type;
            Num = v.Num;
            Obj = v.Obj;
        }

        public Value(Obj o)
        {
            Type = ValueType.Obj;
            Obj = o;
        }

        public Value(double n)
        {
            Type = ValueType.Num;
            Num = n;
        }

        public Value(string s)
        {
            Type = ValueType.Obj;
            Obj = new ObjString(s);
        }

        public Value(bool b)
        {
            Type = b ? ValueType.True : ValueType.False;
        }

        public readonly ValueType Type;
        public readonly double Num;
        public readonly Obj Obj;

        public ObjClass GetClass()
        {
            switch (Type)
            {
                case ValueType.True:
                case ValueType.False:
                    return WrenVM.BoolClass;
                case ValueType.Num:
                    return WrenVM.NumClass;
                case ValueType.Null:
                case ValueType.Undefined:
                    return WrenVM.NullClass;
                default:
                    return Obj.ClassObj;
            }
        }

        // Returns true if [a] and [b] are equivalent. Immutable values (null, bools,
        // numbers, ranges, and strings) are equal if they have the same data. All
        // other values are equal if they are identical objects.
        public static bool Equals(Value a, Value b)
        {
            if (a.Type != b.Type) return false;
            if (a.Type == ValueType.Num) return a.Num == b.Num;
            if (a.Obj == b.Obj) return true;

            // If we get here, it's only possible for two heap-allocated immutable objects
            // to be equal.
            if (a.Type != ValueType.Obj) return false;

            Obj aObj = a.Obj;
            Obj bObj = b.Obj;

            // Must be the same type.
            if (aObj.Type != bObj.Type) return false;

            if (aObj.Type == ObjType.String)
            {
                ObjString aString = (ObjString)aObj;
                ObjString bString = (ObjString)bObj;
                return aString.Value.Equals(bString.Value);
            }

            if (aObj.Type == ObjType.Range)
            {
                ObjRange aRange = (ObjRange)aObj;
                ObjRange bRange = (ObjRange)bObj;
                return ObjRange.Equals(aRange, bRange);
            }
            // All other types are only equal if they are same, which they aren't if
            // we get here.
            return false;
        }

        public override int GetHashCode()
        {
            switch (Type)
            {
                case ValueType.Num:
                    return Num.GetHashCode();
                case ValueType.Obj:
                    return Obj.GetHashCode();
                default:
                    return Type.GetHashCode();
            }
        }
    }

    public enum ValueType
    {
        False,
        Null,
        Num,
        True,
        Undefined,
        Obj
    };
}
