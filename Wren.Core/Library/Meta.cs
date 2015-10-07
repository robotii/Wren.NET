using Wren.Core.Bytecode;
using Wren.Core.Objects;
using Wren.Core.VM;

namespace Wren.Core.Library
{
    class Meta
    {
        const string MetaLibSource = "class Meta {}\n";

        static PrimitiveResult Eval(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1] is ObjString)
            {

                // Eval the code in the module where the calling function was defined.
                Obj callingFn = vm.Fiber.GetFrame().Fn;
                ObjModule module = (callingFn is ObjFn)
                    ? ((ObjFn)callingFn).Module
                    : ((ObjClosure)callingFn).Function.Module;

                // Compile it.
                ObjFn fn = Compiler.Compile(vm, module, "", args[stackStart + 1].ToString(), false);

                if (fn == null)
                {
                    args[stackStart] = Obj.MakeString("Could not compile source code.");
                    return PrimitiveResult.Error;
                }

                // TODO: Include the compile errors in the runtime error message.

                // Create a fiber to run the code in.
                ObjFiber evalFiber = new ObjFiber(fn) { Caller = vm.Fiber };

                // Switch to the fiber.
                args[stackStart] = evalFiber;

                return PrimitiveResult.RunFiber;
            }

            args[stackStart] = Obj.MakeString("Source code must be a string.");
            return PrimitiveResult.Error;
        }

        public static void LoadLibrary(WrenVM vm)
        {
            vm.Interpret("", "", MetaLibSource);

            ObjClass meta = (ObjClass)vm.FindVariable("Meta");
            vm.Primitive(meta.ClassObj, "eval(_)", Eval);
        }
    }
}
