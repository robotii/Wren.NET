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
        public static Obj Null = new Obj(ObjType.Null);
        public static Obj False = new Obj(ObjType.False);
        public static Obj True = new Obj(ObjType.True);
        public static Obj Undefined = new Obj(ObjType.Undefined);

        public Obj(ObjType t)
        {
            Type = t;
        }

        public Obj(double n)
        {
            Type = ObjType.Num;
            Num = n;
        }

        public static Obj MakeString(string s)
        {
            return new ObjString(s);
        }

        public static Obj Bool(bool b)
        {
            return b ? True : False;
        }

        public readonly ObjType Type;
        public readonly double Num;

        // The object's class.
        public ObjClass ClassObj;

        public ObjClass GetClass()
        {
            switch (Type)
            {
                case ObjType.True:
                case ObjType.False:
                    return WrenVM.BoolClass;
                case ObjType.Num:
                    return WrenVM.NumClass;
                case ObjType.Null:
                case ObjType.Undefined:
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
            if (a.Type == ObjType.Num) return a.Num == b.Num;


            // If we get here, it's only possible for two heap-allocated immutable objects
            // to be equal.
            if (a.Type != ObjType.Obj) return true;

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
                case ObjType.Num:
                    return Num.GetHashCode();
                case ObjType.Obj:
                    return base.GetHashCode();
                default:
                    return Type.GetHashCode();
            }
        }
    }

    public enum ObjType
    {
        False,
        Null,
        Num,
        True,
        Undefined,
        Obj
    };
}
