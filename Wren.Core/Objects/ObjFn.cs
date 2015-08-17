using Wren.Core.VM;

namespace Wren.Core.Objects
{
    // A first-class function object. A raw ObjFn can be used and invoked directly
    // if it has no upvalues (i.e. [numUpvalues] is zero). If it does use upvalues,
    // it must be wrapped in an [ObjClosure] first. The compiler is responsible for
    // emitting code to ensure that that happens.
    public class ObjFn : Obj
    {
        public byte[] Bytecode;

        // The module where this function was defined.

        public int NumUpvalues;
        public int NumConstants;

        // TODO: The argument list here is getting a bit gratuitous.
        // Creates a new function object with the given code and constants. The new
        // function will take over ownership of [bytecode] and [sourceLines]. It will
        // copy [constants] into its own array.
        public ObjFn(ObjModule module,
            Value[] constants,
            int numUpvalues, int arity,
            byte[] bytecode, ObjString debugSourcePath,
            string debugName, int[] sourceLines)
        {
            Bytecode = bytecode;
            Constants = constants;
            Module = module;
            NumUpvalues = numUpvalues;
            NumConstants = constants.Length;
            Arity = arity;
            Type = ObjType.Fn;

            /* Debug Information */
            SourcePath = debugSourcePath;
            Name = debugName;
            SourceLines = sourceLines;

            ClassObj = WrenVM.FnClass;
        }

        public string Name;

        public ObjModule Module;
        public ObjString SourcePath;

        public Value[] Constants;

        public int Arity;
        public int[] SourceLines;
    }
}
