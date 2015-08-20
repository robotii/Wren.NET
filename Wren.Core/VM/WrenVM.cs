using System;
using System.Collections.Generic;
using Wren.Core.Bytecode;
using Wren.Core.Library;
using Wren.Core.Objects;

namespace Wren.Core.VM
{
    public delegate string WrenLoadModuleFn(string name);

    public enum InterpretResult
    {
        Success = 0,
        CompileError = 65,
        RuntimeError = 70
    } ;

    public class WrenVM
    {
        public static ObjClass BoolClass;
        public static ObjClass ClassClass;
        public static ObjClass FiberClass;
        public static ObjClass FnClass;
        public static ObjClass ListClass;
        public static ObjClass MapClass;
        public static ObjClass NullClass;
        public static ObjClass NumClass;
        public static ObjClass ObjectClass;
        public static ObjClass RangeClass;
        public static ObjClass StringClass;

        // The fiber that is currently running.
        ObjFiber fiber;

        readonly ObjMap modules;

        public WrenVM()
        {
            MethodNames = new List<string>();
            ObjString name = new ObjString("core");

            // Implicitly create a "core" module for the built in libraries.
            ObjModule coreModule = new ObjModule(name);

            modules = new ObjMap();
            modules.Set(new Value(ValueType.Null), new Value(coreModule));

            CoreLibrary core = new CoreLibrary(this);
            core.InitializeCore();

            // Load in System functions
            Library.IO.LoadIOLibrary(this);
        }

        public List<string> MethodNames;

        public Compiler Compiler { get; set; }

        public WrenLoadModuleFn LoadModuleFn { get; set; }

        // Defines [methodValue] as a method on [classObj].
        private static Value BindMethod(MethodType methodType, int symbol, ObjClass classObj, Value methodContainer)
        {
            ObjFn methodFn = methodContainer.Obj as ObjFn ?? ((ObjClosure)methodContainer.Obj).Function;

            // Methods are always bound against the class, and not the metaclass, even
            // for static methods, because static methods don't have instance fields
            // anyway.
            Compiler.BindMethodCode(classObj, methodFn);

            Method method = new Method { mType = MethodType.Block, obj = methodContainer.Obj };

            if (methodType == MethodType.Static)
                classObj = classObj.ClassObj;

            //classObj.Methods[symbol] = method;
            classObj.BindMethod(symbol, method);
            return new Value(ValueType.Null);
        }

        // Creates a string containing an appropriate method not found error for a
        // method with [symbol] on [classObj].
        static Value MethodNotFound(WrenVM vm, ObjClass classObj, int symbol)
        {
            return new Value(string.Format("{0} does not implement '{1}'.", classObj.Name, vm.MethodNames[symbol]));
        }

        // Looks up the previously loaded module with [name].
        // Returns null if no module with that name has been loaded.
        private ObjModule GetModule(Value name)
        {
            Value moduleContainer = modules.Get(name);
            return moduleContainer.Type == ValueType.Undefined ? null : moduleContainer.Obj as ObjModule;
        }

        // Looks up the core module in the module map.
        private ObjModule GetCoreModule()
        {
            return GetModule(new Value(ValueType.Null));
        }

        private ObjFiber LoadModule(Value name, string source)
        {
            ObjModule module = GetModule(name);

            // See if the module has already been loaded.
            if (module == null)
            {
                module = new ObjModule(name.Obj as ObjString);

                // Store it in the VM's module registry so we don't load the same module
                // multiple times.
                modules.Set(name, new Value(module));

                // Implicitly import the core module.
                ObjModule coreModule = GetCoreModule();
                foreach (ModuleVariable t in coreModule.Variables)
                {
                    DefineVariable(module, t.Name, t.Container);
                }
            }

            ObjFn fn = Compiler.Compile(this, module, name.Obj.ToString(), source, true);
            if (fn == null)
            {
                // TODO: Should we still store the module even if it didn't compile?
                return null;
            }

            ObjFiber moduleFiber = new ObjFiber(fn);


            // Return the fiber that executes the module.
            return moduleFiber;
        }

        private Value ImportModule(Value name)
        {
            // If the module is already loaded, we don't need to do anything.
            if (modules.Get(name).Type != ValueType.Undefined) return new Value(ValueType.Null);

            // Load the module's source code from the embedder.
            string source = LoadModuleFn(name.Obj.ToString());
            if (source == null)
            {
                // Couldn't load the module.
                return new Value(string.Format("Could not find module '{0}'.", name.Obj));
            }

            ObjFiber moduleFiber = LoadModule(name, source);

            // Return the fiber that executes the module.
            return new Value(moduleFiber);
        }


        private bool ImportVariable(Value moduleName, Value variableName, out Value result)
        {
            ObjModule module = GetModule(moduleName);
            if (module == null)
            {
                result = new Value("Could not load module");
                return false; // Should only look up loaded modules
            }

            ObjString variable = variableName.Obj as ObjString;
            if (variable == null)
            {
                result = new Value("Variable name must be a string");
                return false;
            }

            int variableEntry = module.Variables.FindIndex(v => v.Name == variable.ToString());

            // It's a runtime error if the imported variable does not exist.
            if (variableEntry != -1)
            {
                result = new Value(module.Variables[variableEntry].Container);
                return true;
            }

            result = new Value(string.Format("Could not find a variable named '{0}' in module '{1}'.", variableName.Obj, moduleName.Obj));
            return false;
        }

        // Verifies that [superclass] is a valid object to inherit from. That means it
        // must be a class and cannot be the class of any built-in type.
        //
        // If successful, returns null. Otherwise, returns a string for the runtime
        // error message.
        private static Value ValidateSuperclass(Value name, Value superclassContainer)
        {
            // Make sure the superclass is a class.
            if (!(superclassContainer.Obj is ObjClass))
            {
                return new Value(string.Format("Class '{0}' cannot inherit from a non-class object.", name.Obj));
            }

            // Make sure it doesn't inherit from a sealed built-in type. Primitive methods
            // on these classes assume the instance is one of the other Obj___ types and
            // will fail horribly if it's actually an ObjInstance.
            ObjClass superclass = superclassContainer.Obj as ObjClass;

            return superclass.IsSealed ? new Value(string.Format("Class '{0}' cannot inherit from built-in class '{1}'.", name.Obj as ObjString, (superclass.Name))) : null;
        }

        // The main bytecode interpreter loop. This is where the magic happens. It is
        // also, as you can imagine, highly performance critical. Returns `true` if the
        // fiber completed without error.
        private bool RunInterpreter()
        {
            Instruction instruction;
            int index;

            /* Load Frame */
            CallFrame frame = fiber.Frames[fiber.NumFrames - 1];
            int ip = frame.ip;
            int stackStart = frame.StackStart;
            Value[] stack = fiber.Stack;
            Value[] args = new Value[17];

            ObjFn fn = frame.fn as ObjFn ?? ((ObjClosure)frame.fn).Function;
            byte[] bytecode = fn.Bytecode;

            while (true)
            {
                switch (instruction = (Instruction)bytecode[ip++])
                {
                    case Instruction.LOAD_LOCAL_0:
                    case Instruction.LOAD_LOCAL_1:
                    case Instruction.LOAD_LOCAL_2:
                    case Instruction.LOAD_LOCAL_3:
                    case Instruction.LOAD_LOCAL_4:
                    case Instruction.LOAD_LOCAL_5:
                    case Instruction.LOAD_LOCAL_6:
                    case Instruction.LOAD_LOCAL_7:
                    case Instruction.LOAD_LOCAL_8:
                        index = stackStart + instruction - Instruction.LOAD_LOCAL_0;
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = stack[index];
                        break;

                    case Instruction.LOAD_LOCAL:
                        index = stackStart + bytecode[ip++];
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = stack[index];
                        break;

                    case Instruction.LOAD_FIELD_THIS:
                        {
                            byte field = bytecode[ip++];
                            Value receiver = stack[stackStart];
                            ObjInstance instance = receiver.Obj as ObjInstance;
                            if (fiber.StackTop >= fiber.Capacity)
                                fiber.IncreaseStack();
                            stack[fiber.StackTop++] = instance.Fields[field];
                            break;
                        }

                    case Instruction.POP:
                        fiber.StackTop--;
                        break;
                    case Instruction.DUP:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop] = stack[fiber.StackTop - 1];
                        fiber.StackTop++;
                        break;
                    case Instruction.NULL:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = new Value(ValueType.Null);
                        break;
                    case Instruction.FALSE:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = new Value(ValueType.False);
                        break;
                    case Instruction.TRUE:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = new Value(ValueType.True);
                        break;

                    case Instruction.CALL_0:
                    case Instruction.CALL_1:
                    case Instruction.CALL_2:
                    case Instruction.CALL_3:
                    case Instruction.CALL_4:
                    case Instruction.CALL_5:
                    case Instruction.CALL_6:
                    case Instruction.CALL_7:
                    case Instruction.CALL_8:
                    case Instruction.CALL_9:
                    case Instruction.CALL_10:
                    case Instruction.CALL_11:
                    case Instruction.CALL_12:
                    case Instruction.CALL_13:
                    case Instruction.CALL_14:
                    case Instruction.CALL_15:
                    case Instruction.CALL_16:
                    // Handle Super calls
                    case Instruction.SUPER_0:
                    case Instruction.SUPER_1:
                    case Instruction.SUPER_2:
                    case Instruction.SUPER_3:
                    case Instruction.SUPER_4:
                    case Instruction.SUPER_5:
                    case Instruction.SUPER_6:
                    case Instruction.SUPER_7:
                    case Instruction.SUPER_8:
                    case Instruction.SUPER_9:
                    case Instruction.SUPER_10:
                    case Instruction.SUPER_11:
                    case Instruction.SUPER_12:
                    case Instruction.SUPER_13:
                    case Instruction.SUPER_14:
                    case Instruction.SUPER_15:
                    case Instruction.SUPER_16:
                        {
                            int numArgs = instruction - (instruction >= Instruction.SUPER_0 ? Instruction.SUPER_0 : Instruction.CALL_0) + 1;
                            int symbol = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;

                            // The receiver is the first argument.
                            int argStart = fiber.StackTop - numArgs;
                            Value receiver = stack[argStart];
                            ObjClass classObj;

                            if (instruction < Instruction.SUPER_0)
                            {
                                if (receiver.Type == ValueType.Obj)
                                {
                                    classObj = receiver.Obj.ClassObj;
                                }
                                else if (receiver.Type == ValueType.Num)
                                {
                                    classObj = NumClass;
                                }
                                else if (receiver.Type == ValueType.True || receiver.Type == ValueType.False)
                                {
                                    classObj = BoolClass;
                                }
                                else
                                {
                                    classObj = NullClass;
                                }
                            }
                            else
                            {
                                // The superclass is stored in a constant.
                                classObj = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]].Obj as ObjClass;
                                ip += 2;
                            }

                            Method[] methods = classObj.Methods;

                            // If the class's method table doesn't include the symbol, bail.
                            if (symbol < methods.Length)
                            {
                                Method method = methods[symbol];

                                if (method != null)
                                {
                                    if (method.mType == MethodType.Primitive)
                                    {
                                        for (int i = 0; i < numArgs; i++)
                                            args[i] = stack[argStart + i];
                                        // After calling this, the result will be in the first arg slot.
                                        PrimitiveResult result = method.primitive(this, fiber, args);

                                        if (result == PrimitiveResult.Value)
                                        {
                                            fiber.StackTop = argStart + 1;
                                            stack[argStart] = args[0];
                                            Instruction next = (Instruction)bytecode[ip];
                                            if (next == Instruction.STORE_LOCAL)
                                            {
                                                index = stackStart + bytecode[ip + 1];
                                                stack[index] = args[0];
                                                ip += 2;
                                            }
                                            break;
                                        }

                                        frame.ip = ip;

                                        switch (result)
                                        {
                                            case PrimitiveResult.RunFiber:

                                                // If we don't have a fiber to switch to, stop interpreting.
                                                if (args[0].Type == ValueType.Null) return true;

                                                fiber = args[0].Obj as ObjFiber;
                                                /* Load Frame */
                                                frame = fiber.Frames[fiber.NumFrames - 1];
                                                ip = frame.ip;
                                                stackStart = frame.StackStart;
                                                stack = fiber.Stack;
                                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                                bytecode = fn.Bytecode;
                                                break;

                                            case PrimitiveResult.Call:
                                                fiber.Frames.Add(frame = new CallFrame { fn = receiver.Obj, StackStart = argStart, ip = 0 });
                                                fiber.NumFrames++;

                                                /* Load Frame */
                                                ip = 0;
                                                stackStart = argStart;
                                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                                bytecode = fn.Bytecode;
                                                break;

                                            case PrimitiveResult.Error:
                                                RUNTIME_ERROR(fiber, args[0]);
                                                if (fiber == null)
                                                    return false;
                                                frame = fiber.Frames[fiber.NumFrames - 1];
                                                ip = frame.ip;
                                                stackStart = frame.StackStart;
                                                stack = fiber.Stack;
                                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                                bytecode = fn.Bytecode;
                                                break;
                                        }
                                        break;
                                    }

                                    if (method.mType == MethodType.Block)
                                    {
                                        Obj mObj = method.obj;
                                        frame.ip = ip;
                                        fiber.Frames.Add(frame = new CallFrame { fn = mObj, StackStart = argStart, ip = 0 });
                                        fiber.NumFrames++;
                                        /* Load Frame */
                                        ip = 0;
                                        stackStart = argStart;
                                        fn = (mObj as ObjFn) ?? (mObj as ObjClosure).Function;
                                        bytecode = fn.Bytecode;
                                        break;
                                    }
                                }
                            }

                            /* Method not found */
                            frame.ip = ip;
                            RUNTIME_ERROR(fiber, MethodNotFound(this, classObj, symbol));
                            if (fiber == null)
                                return false;
                            frame = fiber.Frames[fiber.NumFrames - 1];
                            ip = frame.ip;
                            stackStart = frame.StackStart;
                            stack = fiber.Stack;
                            fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                            bytecode = fn.Bytecode;

                        }
                        break;

                    case Instruction.STORE_LOCAL:
                        index = stackStart + bytecode[ip++];
                        stack[index] = stack[fiber.StackTop - 1];
                        break;

                    case Instruction.CONSTANT:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                        ip += 2;
                        break;

                    case Instruction.LOAD_UPVALUE:
                        {
                            if (fiber.StackTop >= fiber.Capacity)
                                stack = fiber.IncreaseStack();
                            stack[fiber.StackTop++] = ((ObjClosure)frame.fn).Upvalues[bytecode[ip++]].Container;
                            break;
                        }

                    case Instruction.STORE_UPVALUE:
                        {
                            ObjUpvalue[] upvalues = ((ObjClosure)frame.fn).Upvalues;
                            upvalues[bytecode[ip++]].Container = stack[fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.LOAD_MODULE_VAR:
                        if (fiber.StackTop >= fiber.Capacity)
                            stack = fiber.IncreaseStack();
                        stack[fiber.StackTop++] = fn.Module.Variables[(bytecode[ip] << 8) + bytecode[ip + 1]].Container;
                        ip += 2;
                        break;

                    case Instruction.STORE_MODULE_VAR:
                        fn.Module.Variables[(bytecode[ip] << 8) + bytecode[ip + 1]].Container = stack[fiber.StackTop - 1];
                        ip += 2;
                        break;

                    case Instruction.STORE_FIELD_THIS:
                        {
                            byte field = bytecode[ip++];
                            Value receiver = stack[stackStart];
                            ObjInstance instance = receiver.Obj as ObjInstance;
                            instance.Fields[field] = stack[fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.LOAD_FIELD:
                        {
                            byte field = bytecode[ip++];
                            Value receiver = stack[--fiber.StackTop];
                            ObjInstance instance = receiver.Obj as ObjInstance;
                            if (fiber.StackTop >= fiber.Capacity)
                                stack = fiber.IncreaseStack();
                            stack[fiber.StackTop++] = instance.Fields[field];
                            break;
                        }

                    case Instruction.STORE_FIELD:
                        {
                            byte field = bytecode[ip++];
                            Value receiver = stack[--fiber.StackTop];
                            ObjInstance instance = receiver.Obj as ObjInstance;
                            instance.Fields[field] = stack[fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.JUMP:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += offset + 2;
                            break;
                        }

                    case Instruction.LOOP:
                        {
                            // Jump back to the top of the loop.
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ip -= offset;
                            break;
                        }

                    case Instruction.JUMP_IF:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ValueType condition = stack[--fiber.StackTop].Type;

                            if (condition == ValueType.False || condition == ValueType.Null) ip += offset;
                            break;
                        }

                    case Instruction.AND:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ValueType condition = stack[fiber.StackTop - 1].Type;

                            switch (condition)
                            {
                                case ValueType.Null:
                                case ValueType.False:
                                    ip += offset;
                                    break;
                                default:
                                    fiber.StackTop--;
                                    break;
                            }
                            break;
                        }

                    case Instruction.OR:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            Value condition = stack[fiber.StackTop - 1];

                            switch (condition.Type)
                            {
                                case ValueType.Null:
                                case ValueType.False:
                                    fiber.StackTop--;
                                    break;
                                default:
                                    ip += offset;
                                    break;
                            }
                            break;
                        }

                    case Instruction.CLOSE_UPVALUE:
                        fiber.CloseUpvalue();
                        fiber.StackTop--;
                        break;

                    case Instruction.RETURN:
                        {
                            fiber.Frames.RemoveAt(--fiber.NumFrames);
                            Value result = stack[--fiber.StackTop];
                            // Close any upvalues still in scope.
                            if (fiber.StackTop > stackStart)
                            {
                                Value first = stack[stackStart];
                                while (fiber.OpenUpvalues != null &&
                                       fiber.OpenUpvalues.Container != first)
                                {
                                    fiber.CloseUpvalue();
                                }
                                fiber.CloseUpvalue();
                            }

                            // If the fiber is complete, end it.
                            if (fiber.NumFrames == 0)
                            {
                                // If this is the main fiber, we're done.
                                if (fiber.Caller == null) return true;

                                // We have a calling fiber to resume.
                                fiber = fiber.Caller;
                                stack = fiber.Stack;
                                // Store the result in the resuming fiber.
                                stack[fiber.StackTop - 1] = result;
                            }
                            else
                            {
                                // Discard the stack slots for the call frame (leaving one slot for the result).
                                fiber.StackTop = stackStart + 1;

                                // Store the result of the block in the first slot, which is where the
                                // caller expects it.
                                stack[fiber.StackTop - 1] = result;
                            }

                            /* Load Frame */
                            frame = fiber.Frames[fiber.NumFrames - 1];
                            ip = frame.ip;
                            stackStart = frame.StackStart;
                            fn = frame.fn as ObjFn ?? (frame.fn as ObjClosure).Function;
                            bytecode = fn.Bytecode;
                            break;
                        }

                    case Instruction.CLOSURE:
                        {
                            ObjFn prototype = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]].Obj as ObjFn;
                            ip += 2;

                            // Create the closure and push it on the stack before creating upvalues
                            // so that it doesn't get collected.
                            ObjClosure closure = new ObjClosure(prototype);
                            if (fiber.StackTop >= fiber.Capacity)
                                stack = fiber.IncreaseStack();
                            stack[fiber.StackTop++] = new Value(closure);

                            // Capture upvalues.
                            for (int i = 0; i < prototype.NumUpvalues; i++)
                            {
                                byte isLocal = bytecode[ip++];
                                index = bytecode[ip++];
                                if (isLocal > 0)
                                {
                                    // Make an new upvalue to close over the parent's local variable.
                                    closure.Upvalues[i] = fiber.CaptureUpvalue(stack[stackStart + index]);
                                }
                                else
                                {
                                    // Use the same upvalue as the current call frame.
                                    closure.Upvalues[i] = ((ObjClosure)frame.fn).Upvalues[index];
                                }
                            }

                            break;
                        }

                    case Instruction.CLASS:
                        {
                            Value name = stack[fiber.StackTop - 2];
                            ObjClass superclass = ObjectClass;

                            // Use implicit Object superclass if none given.
                            if (stack[fiber.StackTop - 1].Type != ValueType.Null)
                            {
                                Value error = ValidateSuperclass(name, stack[fiber.StackTop - 1]);
                                if (error != null)
                                {
                                    frame.ip = ip;
                                    RUNTIME_ERROR(fiber, error);
                                    if (fiber == null)
                                        return false;
                                    /* Load Frame */
                                    frame = fiber.Frames[fiber.NumFrames - 1];
                                    ip = frame.ip;
                                    stackStart = frame.StackStart;
                                    stack = fiber.Stack;
                                    fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                    bytecode = fn.Bytecode;
                                    break;
                                }
                                superclass = stack[fiber.StackTop - 1].Obj as ObjClass;
                            }

                            int numFields = bytecode[ip++];

                            Value classObj = new Value(new ObjClass(superclass, numFields, name.Obj as ObjString));

                            // Don't pop the superclass and name off the stack until the subclass is
                            // done being created, to make sure it doesn't get collected.
                            fiber.StackTop -= 2;

                            // Now that we know the total number of fields, make sure we don't overflow.
                            if (superclass.NumFields + numFields > Compiler.MAX_FIELDS)
                            {
                                frame.ip = ip;
                                RUNTIME_ERROR(fiber, new Value(string.Format("Class '{0}' may not have more than 255 fields, including inherited ones.", name.Obj)));
                                if (fiber == null)
                                    return false;
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            if (fiber.StackTop >= fiber.Capacity)
                                stack = fiber.IncreaseStack();
                            stack[fiber.StackTop++] = classObj;
                            break;
                        }

                    case Instruction.METHOD_INSTANCE:
                    case Instruction.METHOD_STATIC:
                        {
                            int symbol = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ObjClass classObj = stack[fiber.StackTop - 1].Obj as ObjClass;
                            Value method = stack[fiber.StackTop - 2];
                            MethodType methodType = instruction == Instruction.METHOD_INSTANCE ? MethodType.None : MethodType.Static;
                            Value error = BindMethod(methodType, symbol, classObj, method);
                            if ((error.Obj is ObjString))
                            {
                                frame.ip = ip;
                                RUNTIME_ERROR(fiber, error);
                                if (fiber == null)
                                    return false;
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }
                            fiber.StackTop -= 2;
                            break;
                        }

                    case Instruction.LOAD_MODULE:
                        {
                            Value name = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Value result = ImportModule(name);

                            // If it returned a string, it was an error message.
                            if ((result.Obj is ObjString))
                            {
                                frame.ip = ip;
                                RUNTIME_ERROR(fiber, result);
                                if (fiber == null)
                                    return false;
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            // Make a slot that the module's fiber can use to store its result in.
                            // It ends up getting discarded, but CODE_RETURN expects to be able to
                            // place a value there.
                            if (fiber.StackTop >= fiber.Capacity)
                                stack = fiber.IncreaseStack();
                            stack[fiber.StackTop++] = Value.Null;

                            // If it returned a fiber to execute the module body, switch to it.
                            if (result.Obj is ObjFiber)
                            {
                                // Return to this module when that one is done.
                                (result.Obj as ObjFiber).Caller = fiber;

                                frame.ip = ip;
                                fiber = (result.Obj as ObjFiber);
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                            }

                            break;
                        }

                    case Instruction.IMPORT_VARIABLE:
                        {
                            Value module = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Value variable = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Value result;
                            if (ImportVariable(module, variable, out result))
                            {
                                if (fiber.StackTop >= fiber.Capacity)
                                    stack = fiber.IncreaseStack();
                                stack[fiber.StackTop++] = result;
                            }
                            else
                            {
                                frame.ip = ip;
                                RUNTIME_ERROR(fiber, result);
                                if (fiber == null)
                                    return false;
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                            }
                            break;
                        }

                    case Instruction.CONSTRUCT:
                        {
                            int stackPosition = fiber.StackTop - 1 + (Instruction.CALL_0 - (Instruction)bytecode[ip]);
                            ObjClass v = stack[stackPosition].Obj as ObjClass;
                            if (v == null)
                            {
                                RUNTIME_ERROR(fiber, new Value("'this' should be a class."));
                                if (fiber == null)
                                    return false;
                                /* Load Frame */
                                frame = fiber.Frames[fiber.NumFrames - 1];
                                ip = frame.ip;
                                stackStart = frame.StackStart;
                                stack = fiber.Stack;
                                fn = (frame.fn as ObjFn) ?? (frame.fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }
                            stack[stackPosition] = new Value(new ObjInstance(v));
                        }
                        break;

                    case Instruction.FOREIGN_CLASS:
                        // Not yet implemented
                        break;

                    case Instruction.FOREIGN_CONSTRUCT:
                        // Not yet implemented
                        break;

                    case Instruction.END:
                        // A CODE_END should always be preceded by a CODE_RETURN. If we get here,
                        // the compiler generated wrong code.
                        return false;
                }
            }

            // We should only exit this function from an explicit return from CODE_RETURN
            // or a runtime error.
        }

        // Execute [source] in the context of the core module.
        private InterpretResult LoadIntoCore(string source)
        {
            ObjModule coreModule = GetCoreModule();

            ObjFn fn = Compiler.Compile(this, coreModule, "", source, true);
            if (fn == null) return InterpretResult.CompileError;

            fiber = new ObjFiber(fn);

            return RunInterpreter() ? InterpretResult.Success : InterpretResult.RuntimeError;
        }

        public InterpretResult Interpret(string sourcePath, string source)
        {
            if (sourcePath.Length == 0) return LoadIntoCore(source);

            // TODO: Better module name.
            Value name = new Value("main");

            ObjFiber f = LoadModule(name, source);
            if (f == null)
            {
                return InterpretResult.CompileError;
            }

            fiber = f;

            bool succeeded = RunInterpreter();

            return succeeded ? InterpretResult.Success : InterpretResult.RuntimeError;
        }

        public Value FindVariable(string name)
        {
            ObjModule coreModule = GetCoreModule();
            int symbol = coreModule.Variables.FindIndex(v => v.Name == name);
            return coreModule.Variables[symbol].Container;
        }

        internal int DeclareVariable(ObjModule module, string name)
        {
            if (module == null) module = GetCoreModule();
            if (module.Variables.Count == ObjModule.MAX_MODULE_VARS) return -2;

            module.Variables.Add(new ModuleVariable { Name = name, Container = new Value() });
            return module.Variables.Count - 1;
        }

        internal int DefineVariable(ObjModule module, string name, Value c)
        {
            if (module == null) module = GetCoreModule();
            if (module.Variables.Count == ObjModule.MAX_MODULE_VARS) return -2;

            // See if the variable is already explicitly or implicitly declared.
            int symbol = module.Variables.FindIndex(m => m.Name == name);

            if (symbol == -1)
            {
                // Brand new variable.
                module.Variables.Add(new ModuleVariable { Name = name, Container = c });
                symbol = module.Variables.Count - 1;
            }
            else if (module.Variables[symbol].Container.Type == ValueType.Undefined)
            {
                // Explicitly declaring an implicitly declared one. Mark it as defined.
                module.Variables[symbol].Container = c;
            }
            else
            {
                // Already explicitly declared.
                symbol = -1;
            }

            return symbol;
        }

        /* Dirty Hack */
        private void RUNTIME_ERROR(ObjFiber f, Value v)
        {
            if (f.Error != null)
            {
                Console.Error.WriteLine("Can only fail once.");
                return;
            }

            if (f.CallerIsTrying)
            {
                f.Caller.SetReturnValue(v);
                fiber = f.Caller;
                f.Error = v.Obj as ObjString;
                return;
            }
            fiber = null;

            // TODO: Fix this so that there is no dependancy on the console
            if (v == null || v.Obj == null || v.Obj.Type != ObjType.String)
            {
                v = new Value("Error message must be a string.");
            }
            f.Error = v.Obj as ObjString;
            Console.Error.WriteLine(v.Obj as ObjString);
        }

        /* Anotehr Dirty Hack */
        public void Primitive(ObjClass objClass, string s, Primitive func)
        {
            if (!MethodNames.Contains(s))
            {
                MethodNames.Add(s);
            }
            int symbol = MethodNames.IndexOf(s);

            Method m = new Method { primitive = func, mType = MethodType.Primitive };
            //objClass.BindMethod(symbol, m);
            objClass.BindMethod(symbol, m);
        }

    }
}
