﻿namespace Wren.Core.Objects
{
    public class ObjUpvalue : Obj
    {
        // Pointer to the variable this upvalue is referencing.

        // If the upvalue is closed (i.e. the local variable it was pointing too has
        // been popped off the stack) then the closed-over value will be hoisted out
        // of the stack into here. [value] will then be changed to point to this.

        // Open upvalues are stored in a linked list by the fiber. This points to the
        // next upvalue in that list.

        // Creates a new open upvalue pointing to [value] on the stack.
        public ObjUpvalue(Obj c, int index)
        {
            Container = c;
            Next = null;
            Index = index;
        }

        public Obj Container;
        public ObjUpvalue Next;
        public int Index;
    }
}
