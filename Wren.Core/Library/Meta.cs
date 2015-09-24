using Wren.Core.Bytecode;
using Wren.Core.Objects;
using Wren.Core.VM;

namespace Wren.Core.Library
{
    class Meta
    {
        const string MetaLibSource = "class Meta {}\n";

        static PrimitiveResult Eval(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1] != null && args[1].Obj is ObjString)
            {

                // Eval the code in the module where the calling function was defined.
                Obj callingFn = fiber.GetFrame().Fn;
                ObjModule module = (callingFn is ObjFn)
                    ? ((ObjFn)callingFn).Module
                    : ((ObjClosure)callingFn).Function.Module;

                // Compile it.
                ObjFn fn = Compiler.Compile(vm, module, "", args[1].Obj.ToString(), false);

                if (fn == null)
                {
                    args[0] = new Value("Could not compile source code.");
                    return PrimitiveResult.Error;
                }

                // TODO: Include the compile errors in the runtime error message.

                // Create a fiber to run the code in.
                ObjFiber evalFiber = new ObjFiber(fn) { Caller = fiber };

                // Switch to the fiber.
                args[0] = new Value(evalFiber);

                return PrimitiveResult.RunFiber;
            }

            args[0] = new Value("Source code must be a string.");
            return PrimitiveResult.Error;
        }

        public static void LoadMetaLibrary(WrenVM vm)
        {
            vm.Interpret("", MetaLibSource);

            ObjClass meta = (ObjClass)vm.FindVariable("Meta").Obj;
            vm.Primitive(meta.ClassObj, "eval(_)", Eval);
        }
    }
}
