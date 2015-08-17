namespace Wren.Core.Objects
{
    // Identifies which specific type a heap-allocated object is.
    public enum ObjType
    {
        Class,
        Closure,
        Fiber,
        Fn,
        Instance,
        List,
        Map,
        Module,
        Range,
        String,
        Upvalue
    };

    // Base struct for all heap-allocated objects.
    public class Obj
    {
        // The object's class.
        public ObjType Type;
        public ObjClass ClassObj;
    }
}
