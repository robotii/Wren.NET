namespace Wren.Core.VM
{
    // This defines the bytecode instructions used by the VM.

    // Note that the order of instructions here affects the order of the dispatch
    // table in the VM's interpreter loop. That in turn affects caching which
    // affects overall performance. Take care to run benchmarks if you change the
    // order here.
    internal enum Instruction
    {
        // Load the constant at index [arg].
        CONSTANT,

        // Push null onto the stack.
        NULL,

        // Push false onto the stack.
        FALSE,

        // Push true onto the stack.
        TRUE,

        // Pushes the value in the given local slot.
        LOAD_LOCAL_0,
        LOAD_LOCAL_1,
        LOAD_LOCAL_2,
        LOAD_LOCAL_3,
        LOAD_LOCAL_4,
        LOAD_LOCAL_5,
        LOAD_LOCAL_6,
        LOAD_LOCAL_7,
        LOAD_LOCAL_8,

        // Note: The compiler assumes the following _STORE instructions always
        // immediately follow their corresponding _LOAD ones.

        // Pushes the value in local slot [arg].
        LOAD_LOCAL,

        // Stores the top of stack in local slot [arg]. Does not pop it.
        STORE_LOCAL,

        // Pushes the value in upvalue [arg].
        LOAD_UPVALUE,

        // Stores the top of stack in upvalue [arg]. Does not pop it.
        STORE_UPVALUE,

        // Pushes the value of the top-level variable in slot [arg].
        LOAD_MODULE_VAR,

        // Stores the top of stack in top-level variable slot [arg]. Does not pop it.
        STORE_MODULE_VAR,

        // Pushes the value of the field in slot [arg] of the receiver of the current
        // function. This is used for regular field accesses on "this" directly in
        // methods. This instruction is faster than the more general CODE_LOAD_FIELD
        // instruction.
        LOAD_FIELD_THIS,

        // Stores the top of the stack in field slot [arg] in the receiver of the
        // current value. Does not pop the value. This instruction is faster than the
        // more general CODE_LOAD_FIELD instruction.
        STORE_FIELD_THIS,

        // Pops an instance and pushes the value of the field in slot [arg] of it.
        LOAD_FIELD,

        // Pops an instance and stores the subsequent top of stack in field slot
        // [arg] in it. Does not pop the value.
        STORE_FIELD,

        // Pop and discard the top of stack.
        POP,

        // Push a copy of the value currently on the top of the stack.
        DUP,

        // Invoke the method with symbol [arg]. The number indicates the number of
        // arguments (not including the receiver,.
        CALL_0,
        CALL_1,
        CALL_2,
        CALL_3,
        CALL_4,
        CALL_5,
        CALL_6,
        CALL_7,
        CALL_8,
        CALL_9,
        CALL_10,
        CALL_11,
        CALL_12,
        CALL_13,
        CALL_14,
        CALL_15,
        CALL_16,

        // Invoke a superclass method with symbol [arg]. The number indicates the
        // number of arguments (not including the receiver,.
        SUPER_0,
        SUPER_1,
        SUPER_2,
        SUPER_3,
        SUPER_4,
        SUPER_5,
        SUPER_6,
        SUPER_7,
        SUPER_8,
        SUPER_9,
        SUPER_10,
        SUPER_11,
        SUPER_12,
        SUPER_13,
        SUPER_14,
        SUPER_15,
        SUPER_16,

        // Jump the instruction pointer [arg] forward.
        JUMP,

        // Jump the instruction pointer [arg] backward. Pop and discard the top of
        // the stack.
        LOOP,

        // Pop and if not truthy then jump the instruction pointer [arg] forward.
        JUMP_IF,

        // If the top of the stack is false, jump [arg] forward. Otherwise, pop and
        // continue.
        AND,

        // If the top of the stack is non-false, jump [arg] forward. Otherwise, pop
        // and continue.
        OR,

        // Close the upvalue for the local on the top of the stack, then pop it.
        CLOSE_UPVALUE,

        // Exit from the current function and return the value on the top of the
        // stack.
        RETURN,

        // Creates a closure for the function stored at [arg] in the constant table.
        //
        // Following the function argument is a number of arguments, two for each
        // upvalue. The first is true if the variable being captured is a local (as
        // opposed to an upvalue,, and the second is the index of the local or
        // upvalue being captured.
        //
        // Pushes the created closure.
        CLOSURE,

        // Creates a new instance of a class.
        //
        // Assumes the class object is in slot zero, and replaces it with the new
        // uninitialized instance of that class. This opcode is only emitted by the
        // compiler-generated constructor metaclass methods.
        CONSTRUCT,

        // Creates a new instance of a foreign class.
        //
        // Assumes the class object is in slot zero, and replaces it with the new
        // uninitialized instance of that class. This opcode is only emitted by the
        // compiler-generated constructor metaclass methods.
        FOREIGN_CONSTRUCT,

        // Creates a class. Top of stack is the superclass. Below that is a string for
        // the name of the class. Byte [arg] is the number of fields in the class.
        CLASS,

        // Creates a foreign class. Top of stack is the superclass. Below that is a
        // string for the name of the class.
        FOREIGN_CLASS,

        // Define a method for symbol [arg]. The class receiving the method is popped
        // off the stack, then the function defining the body is popped.
        METHOD_INSTANCE,

        // Define a method for symbol [arg]. The class whose metaclass will receive
        // the method is popped off the stack, then the function defining the body is
        // popped.
        METHOD_STATIC,

        // Load the module whose name is stored in string constant [arg]. Pushes
        // NULL onto the stack. If the module has already been loaded, does nothing
        // else. Otherwise, it creates a fiber to run the desired module and switches
        // to that. When that fiber is done, the current one is resumed.
        LOAD_MODULE,

        // Reads a top-level variable from another module. [arg1] is a string
        // constant for the name of the module, and [arg2] is a string constant for
        // the variable name. Pushes the variable if found, or generates a runtime
        // error otherwise.
        IMPORT_VARIABLE,

        // This pseudo-instruction indicates the end of the bytecode. It should
        // always be preceded by a `CODE_RETURN`, so is never actually executed.
        END
    };
}
