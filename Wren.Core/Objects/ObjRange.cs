using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjRange : Obj
    {
        // The beginning of the range.

        // The end of the range. May be greater or less than [from].

        // True if [to] is included in the range.

        // Creates a new range from [from] to [to].
        public ObjRange(double from, double to, bool isInclusive)
            : base(ObjType.Obj)
        {
            From = from;
            To = to;
            IsInclusive = isInclusive;
            ClassObj = WrenVM.RangeClass;
        }

        public readonly double From;

        public readonly double To;

        public readonly bool IsInclusive;

        public static bool Equals(ObjRange a, ObjRange b)
        {
            return a != null && b != null && a.From == b.From && a.To == b.To && a.IsInclusive == b.IsInclusive;
        }

        public override int GetHashCode()
        {
            return From.GetHashCode() + To.GetHashCode() + IsInclusive.GetHashCode();
        }
    }
}
