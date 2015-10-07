using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjComparer : IEqualityComparer<Obj>
    {
        public bool Equals(Obj x, Obj y)
        {
            return x != null && Obj.Equals(x, y);
        }

        public int GetHashCode(Obj obj)
        {
            return obj.GetHashCode();
        }
    }

    // Base struct for all heap-allocated objects.
    public class Obj
    {
        public static Obj Null = new Obj(ValueType.Null);

        public Obj()
        {
            Type = ValueType.Undefined;
        }

        public Obj(ValueType t)
        {
            Type = t;
        }

        public Obj(double n)
        {
            Type = ValueType.Num;
            Num = n;
        }

        public static Obj MakeString(string s)
        {
            return new ObjString(s);
        }

        public Obj(bool b)
        {
            Type = b ? ValueType.True : ValueType.False;
        }

        public readonly ValueType Type;
        public readonly double Num;

        // The object's class.
        public ObjClass ClassObj;

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
                    return ClassObj;
            }
        }

        // Returns true if [a] and [b] are equivalent. Immutable values (null, bools,
        // numbers, ranges, and strings) are equal if they have the same data. All
        // other values are equal if they are identical objects.
        public static bool Equals(Obj a, Obj b)
        {
            if (a == b) return true;
            if (a.Type != b.Type) return false;
            if (a.Type == ValueType.Num) return a.Num == b.Num;


            // If we get here, it's only possible for two heap-allocated immutable objects
            // to be equal.
            if (a.Type != ValueType.Obj) return true;

            // Must be the same type.
            if (a.GetType() != b.GetType()) return false;

            ObjString aString = a as ObjString;
            if (aString != null)
            {
                ObjString bString = (ObjString)b;
                return aString.Str.Equals(bString.Str);
            }

            ObjRange aRange = a as ObjRange;
            if (aRange != null)
            {
                ObjRange bRange = (ObjRange)b;
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
                    return base.GetHashCode();
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
