using Wren.Core.VM;

namespace Wren.Core.Objects
{
    // An instance of a first-class function and the environment it has closed over.
    // Unlike [ObjFn], this has captured the upvalues that the function accesses.
    public class ObjClosure : Obj
    {
        // Creates a new closure object that invokes [fn]. Allocates room for its
        // upvalues, but assumes outside code will populate it.
        public ObjClosure(ObjFn fn)
            : base(ObjType.Obj)
        {
            Function = fn;
            Upvalues = new ObjUpvalue[fn.NumUpvalues];
            ClassObj = WrenVM.FnClass;
        }

        // The function that this closure is an instance of.
        public ObjFn Function;

        // The upvalues this function has closed over.
        public ObjUpvalue[] Upvalues;
    }
}
