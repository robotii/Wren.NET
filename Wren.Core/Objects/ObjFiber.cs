using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjFiber : Obj
    {
        internal Obj[] Stack;

        public List<CallFrame> Frames;

        private const int InitialStackSize = 1024;

        public int Capacity;

        // Pointer to the first node in the linked list of open upvalues that are
        // pointing to values still on the stack. The head of the list will be the
        // upvalue closest to the top of the stack, and then the list works downwards.
        public ObjUpvalue OpenUpvalues;

        // The fiber that ran this one. If this fiber is yielded, control will resume
        // to this one. May be null.
        public ObjFiber Caller;

        public int NumFrames;

        // If the fiber failed because of a runtime error, this will contain the
        // error message. Otherwise, it will be null.
        public Obj Error;

        // This will be true if the caller that called this fiber did so using "try".
        // In that case, if this fiber fails with an error, the error will be given
        // to the caller.
        public bool CallerIsTrying;

        public int StackTop;

        // Creates a new fiber object that will invoke [fn], which can be a function or
        // closure.
        public ObjFiber(Obj fn)
        {
            ResetFiber(fn);
            ClassObj = WrenVM.FiberClass;
        }

        public CallFrame GetFrame()
        {
            return Frames[NumFrames - 1];
        }

        // Resets [fiber] back to an initial state where it is ready to invoke [fn].
        private void ResetFiber(Obj fn)
        {
            Stack = new Obj[InitialStackSize];
            Capacity = InitialStackSize;
            Frames = new List<CallFrame>();

            // Push the stack frame for the function.
            StackTop = 0;
            NumFrames = 1;
            OpenUpvalues = null;
            Caller = null;
            Error = null;
            CallerIsTrying = false;

            CallFrame frame = new CallFrame { Fn = fn, StackStart = 0, Ip = 0 };
            Frames.Add(frame);
        }

        public Obj GetReceiver(int numArgs)
        {
            return Stack[StackTop - numArgs];
        }

        public void Discard(int numArgs)
        {
            StackTop -= numArgs;
        }

        public void SetStackSize(int stackSize)
        {
            StackTop = stackSize;
        }

        // Captures the local variable [local] into an [Upvalue]. If that local is
        // already in an upvalue, the existing one will be used. (This is important to
        // ensure that multiple closures closing over the same variable actually see
        // the same variable.) Otherwise, it will create a new open upvalue and add it
        // the fiber's list of upvalues.
        public ObjUpvalue CaptureUpvalue(Obj local)
        {
            // If there are no open upvalues at all, we must need a new one.
            if (OpenUpvalues == null)
            {
                OpenUpvalues = new ObjUpvalue(local);
                return OpenUpvalues;
            }

            ObjUpvalue prevUpvalue = null;
            ObjUpvalue upvalue = OpenUpvalues;

            // Walk towards the bottom of the stack until we find a previously existing
            // upvalue or pass where it should be.
            while (upvalue != null && upvalue.Container != local)
            {
                prevUpvalue = upvalue;
                upvalue = upvalue.Next;
            }

            // Found an existing upvalue for this local.
            if (upvalue != null && upvalue.Container == local) return upvalue;

            // We've walked past this local on the stack, so there must not be an
            // upvalue for it already. Make a new one and link it in in the right
            // place to keep the list sorted.
            ObjUpvalue createdUpvalue = new ObjUpvalue(local);
            if (prevUpvalue == null)
            {
                // The new one is the first one in the list.
                OpenUpvalues = createdUpvalue;
            }
            else
            {
                prevUpvalue.Next = createdUpvalue;
            }

            createdUpvalue.Next = upvalue;
            return createdUpvalue;
        }

        public void CloseUpvalue()
        {
            if (OpenUpvalues == null)
                return;

            ObjUpvalue upvalue = OpenUpvalues;

            // Move the value into the upvalue itself and point the upvalue to it.
            //upvalue.Container = upvalue.Container;

            // Remove it from the open upvalue list.
            OpenUpvalues = upvalue.Next;
        }

        // Puts [fiber] into a runtime failed state because of [error].
        //
        // Returns the fiber that should receive the error or `NULL` if no fiber
        // caught it.

        public ObjFiber RuntimeError(Obj error)
        {
            // Store the error in the fiber so it can be accessed later.
            Error = error as ObjString;

            // If the caller ran this fiber using "try", give it the error.
            if (CallerIsTrying)
            {
                // Make the caller's try method return the error message.
                Caller.SetReturnValue(error);
                return Caller;
            }

            // If we got here, nothing caught the error, so show the stack trace.
            // TODO: Fix me
            //DebugPrintStackTrace(fiber);
            return null;
        }

        // Pushes [function] onto [fiber]'s callstack and invokes it. Expects [numArgs]
        // arguments (including the receiver) to be on the top of the stack already.
        // [function] can be an `ObjFn` or `ObjClosure`.
        public void CallFunction(Obj function, int numArgs)
        {
            CallFrame frame = new CallFrame { Fn = function, StackStart = StackTop - numArgs, Ip = 0 };
            Frames.Add(frame);
            NumFrames++;
        }

        public Obj Return()
        {
            Frames.RemoveAt(--NumFrames);
            return Pop();
        }

        public void SetReturnValue(Obj v)
        {
            Stack[StackTop - 1] = v;
        }

        public void Push(Obj c)
        {
            if (StackTop >= Capacity)
                IncreaseStack();
            Stack[StackTop++] = c;
        }

        public void Dup()
        {
            Push(Stack[StackTop - 1]);
        }

        public Obj Pop()
        {
            return Stack[--StackTop];
        }

        public void Drop()
        {
            StackTop--;
        }

        public Obj Peek()
        {
            return Stack[StackTop - 1];
        }

        public Obj Peek2()
        {
            return Stack[StackTop - 2];
        }

        public void StoreValue(int index, Obj v)
        {
            Stack[StackTop + index] = v;
        }

        public Obj[] IncreaseStack()
        {
            Obj[] v = new Obj[Capacity * 2];
            Stack.CopyTo(v, 0);
            Stack = v;
            return Stack;
        }
    }

    public class CallFrame
    {
        // Pointer to the current (really next-to-be-executed) instruction in the
        // function's bytecode.
        public int Ip;

        // The function or closure being executed.
        public Obj Fn;

        // Pointer to the first stack slot used by this call frame. This will contain
        // the receiver, followed by the function's parameters, then local variables
        // and temporaries.
        public int StackStart;
    };
}
