using System;
using System.Globalization;
using Wren.Core.Objects;
using Wren.Core.VM;
using ValueType = Wren.Core.VM.ValueType;

namespace Wren.Core.Library
{
    class CoreLibrary
    {
        private readonly WrenVM vm;

        // This string literal is generated automatically from core. Do not edit.
        const string coreLibSource =
        "class Bool {}\n"
        + "class Fiber {}\n"
        + "class Fn {}\n"
        + "class Null {}\n"
        + "class Num {}\n"
        + "\n"
        + "class Sequence {\n"
        + "  all(f) {\n"
        + "    var result = true\n"
        + "    for (element in this) {\n"
        + "      result = f.call(element)\n"
        + "      if (!result) return result\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  any(f) {\n"
        + "    var result = false\n"
        + "    for (element in this) {\n"
        + "      result = f.call(element)\n"
        + "      if (result) return result\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  contains(element) {\n"
        + "    for (item in this) {\n"
        + "      if (element == item) return true\n"
        + "    }\n"
        + "    return false\n"
        + "  }\n"
        + "\n"
        + "  count {\n"
        + "    var result = 0\n"
        + "    for (element in this) {\n"
        + "      result = result + 1\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  count(f) {\n"
        + "    var result = 0\n"
        + "    for (element in this) {\n"
        + "      if (f.call(element)) result = result + 1\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  each(f) {\n"
        + "    for (element in this) {\n"
        + "      f.call(element)\n"
        + "    }\n"
        + "  }\n"
        + "\n"
        + "  map(transformation) { new MapSequence(this, transformation) }\n"
        + "\n"
        + "  where(predicate) { new WhereSequence(this, predicate) }\n"
        + "\n"
        + "  reduce(acc, f) {\n"
        + "    for (element in this) {\n"
        + "      acc = f.call(acc, element)\n"
        + "    }\n"
        + "    return acc\n"
        + "  }\n"
        + "\n"
        + "  reduce(f) {\n"
        + "    var iter = iterate(null)\n"
        + "    if (!iter) Fiber.abort(\"Can't reduce an empty sequence.\")\n"
        + "\n"
        + "    // Seed with the first element.\n"
        + "    var result = iteratorValue(iter)\n"
        + "    while (iter = iterate(iter)) {\n"
        + "      result = f.call(result, iteratorValue(iter))\n"
        + "    }\n"
        + "\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  join { join(\"\") }\n"
        + "\n"
        + "  join(sep) {\n"
        + "    var first = true\n"
        + "    var result = \"\"\n"
        + "\n"
        + "    for (element in this) {\n"
        + "      if (!first) result = result + sep\n"
        + "      first = false\n"
        + "      result = result + element.toString\n"
        + "    }\n"
        + "\n"
        + "    return result\n"
        + "  }\n"
        + "\n"
        + "  toList {\n"
        + "    var result = new List\n"
        + "    for (element in this) {\n"
        + "      result.add(element)\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "}\n"
        + "\n"
        + "class MapSequence is Sequence {\n"
        + "  new(sequence, fn) {\n"
        + "    _sequence = sequence\n"
        + "    _fn = fn\n"
        + "  }\n"
        + "\n"
        + "  iterate(iterator) { _sequence.iterate(iterator) }\n"
        + "  iteratorValue(iterator) { _fn.call(_sequence.iteratorValue(iterator)) }\n"
        + "}\n"
        + "\n"
        + "class WhereSequence is Sequence {\n"
        + "  new(sequence, fn) {\n"
        + "    _sequence = sequence\n"
        + "    _fn = fn\n"
        + "  }\n"
        + "\n"
        + "  iterate(iterator) {\n"
        + "    while (iterator = _sequence.iterate(iterator)) {\n"
        + "      if (_fn.call(_sequence.iteratorValue(iterator))) break\n"
        + "    }\n"
        + "    return iterator\n"
        + "  }\n"
        + "\n"
        + "  iteratorValue(iterator) { _sequence.iteratorValue(iterator) }\n"
        + "}\n"
        + "\n"
        + "class String is Sequence {}\n"
        + "\n"
        + "class List is Sequence {\n"
        + "  addAll(other) {\n"
        + "    for (element in other) {\n"
        + "      add(element)\n"
        + "    }\n"
        + "    return other\n"
        + "  }\n"
        + "\n"
        + "  toString { \"[\" + join(\", \") + \"]\" }\n"
        + "\n"
        + "  +(other) {\n"
        + "    var result = this[0..-1]\n"
        + "    for (element in other) {\n"
        + "      result.add(element)\n"
        + "    }\n"
        + "    return result\n"
        + "  }\n"
        + "}\n"
        + "\n"
        + "class Map {\n"
        + "  keys { new MapKeySequence(this) }\n"
        + "  values { new MapValueSequence(this) }\n"
        + "\n"
        + "  toString {\n"
        + "    var first = true\n"
        + "    var result = \"{\"\n"
        + "\n"
        + "    for (key in keys) {\n"
        + "      if (!first) result = result + \", \"\n"
        + "      first = false\n"
        + "      result = result + key.toString + \": \" + this[key].toString\n"
        + "    }\n"
        + "\n"
        + "    return result + \"}\"\n"
        + "  }\n"
        + "}\n"
        + "\n"
        + "class MapKeySequence is Sequence {\n"
        + "  new(map) {\n"
        + "    _map = map\n"
        + "  }\n"
        + "\n"
        + "  iterate(n) { _map.iterate_(n) }\n"
        + "  iteratorValue(iterator) { _map.keyIteratorValue_(iterator) }\n"
        + "}\n"
        + "\n"
        + "class MapValueSequence is Sequence {\n"
        + "  new(map) {\n"
        + "    _map = map\n"
        + "  }\n"
        + "\n"
        + "  iterate(n) { _map.iterate_(n) }\n"
        + "  iteratorValue(iterator) { _map.valueIteratorValue_(iterator) }\n"
        + "}\n"
        + "\n"
        + "class Range is Sequence {}\n";

        static PrimitiveResult prim_bool_not(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(args[0].Type != ValueType.True);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_bool_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[0].Type == ValueType.True)
            {
                args[0] = new Value("true");
            }
            else
            {
                args[0] = new Value("false");
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_class_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(new ObjInstance(args[0].Obj as ObjClass));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_class_name(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(((ObjClass)args[0].Obj).Name);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_class_supertype(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjClass classObj = (ObjClass)args[0].Obj;

            // Object has no superclass.
            if (classObj.Superclass == null)
            {
                args[0] = new Value (ValueType.Null);
            }
            else
            {
                args[0] = new Value(classObj.Superclass);
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // Return the Fiber class itself. When we then call "new" on it, it will
            // create the fiber.
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_new(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            Obj o = args[1].Obj;
            if (o.Type == ObjType.Fn || o.Type == ObjType.Closure)
            {
                ObjFiber newFiber = new ObjFiber(o);

                // The compiler expects the first slot of a function to hold the receiver.
                // Since a fiber's stack is invoked directly, it doesn't have one, so put it
                // in here.
                newFiber.Push(Value.Null);

                args[0] = new Value(newFiber);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a function.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_abort(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            Obj o = args[1].Obj;
            args[0] = o != null && o.Type == ObjType.String ? args[1] : new Value("Error message must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_call(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = args[0].Obj as ObjFiber;

            if (runFiber != null)
            {
                if (runFiber.NumFrames != 0)
                {
                    if (runFiber.Caller == null)
                    {
                        runFiber.Caller = fiber;

                        // If the fiber was yielded, make the yield call return null.
                        if (runFiber.StackTop > 0)
                        {
                            runFiber.StoreValue(-1, new Value (ValueType.Null));
                        }

                        return PrimitiveResult.RunFiber;
                    }

                    // Remember who ran it.
                    args[0] = new Value("Fiber has already been called.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Cannot call a finished fiber.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Trying to call a non-fiber");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_call1(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = args[0].Obj as ObjFiber;

            if (runFiber != null)
            {
                if (runFiber.NumFrames != 0)
                {
                    if (runFiber.Caller == null)
                    {
                        // Remember who ran it.
                        runFiber.Caller = fiber;

                        // If the fiber was yielded, make the yield call return the value passed to
                        // run.
                        if (runFiber.StackTop > 0)
                        {
                            runFiber.StoreValue(-1, args[1]);
                        }

                        // When the calling fiber resumes, we'll store the result of the run call
                        // in its stack. Since fiber.run(value) has two arguments (the fiber and the
                        // value) and we only need one slot for the result, discard the other slot
                        // now.
                        fiber.StackTop--;

                        return PrimitiveResult.RunFiber;
                    }

                    args[0] = new Value("Fiber has already been called.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Cannot call a finished fiber.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Trying to call a non-fiber");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_current(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(fiber);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_error(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;
            args[0] = runFiber.Error == null ? new Value (ValueType.Null) : new Value(runFiber.Error);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_isDone(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;
            args[0] = new Value(runFiber.NumFrames == 0 || runFiber.Error != null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_run(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, new Value (ValueType.Null));
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = fiber.Caller;

                return PrimitiveResult.RunFiber;
            }

            // If the fiber was yielded, make the yield call return null.
            args[0] = new Value("Cannot run a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_run1(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, args[1]);
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = fiber.Caller;

                return PrimitiveResult.RunFiber;
            }

            // If the fiber was yielded, make the yield call return the value passed to
            // run.
            args[0] = new Value("Cannot run a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_try(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null)
                {
                    runFiber.Caller = fiber;
                    runFiber.CallerIsTrying = true;

                    // If the fiber was yielded, make the yield call return null.
                    if (runFiber.StackTop > 0)
                    {
                        runFiber.StoreValue(-1, new Value (ValueType.Null));
                    }

                    return PrimitiveResult.RunFiber;
                }

                // Remember who ran it.
                args[0] = new Value("Fiber has already been called.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Cannot try a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_yield(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = fiber.Caller;
            fiber.Caller = null;
            fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[0] = new Value (ValueType.Null);
            }
            else
            {
                // Make the caller's run method return null.
                caller.StoreValue(-1, new Value (ValueType.Null));

                // Return the fiber to resume.
                args[0] = new Value(caller);
            }

            return PrimitiveResult.RunFiber;
        }

        static PrimitiveResult prim_fiber_yield1(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = fiber.Caller;
            fiber.Caller = null;
            fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[0] = new Value (ValueType.Null);
            }
            else
            {
                // Make the caller's run method return the argument passed to yield.
                caller.StoreValue(-1, args[1]);

                // When the yielding fiber resumes, we'll store the result of the yield call
                // in its stack. Since Fiber.yield(value) has two arguments (the Fiber class
                // and the value) and we only need one slot for the result, discard the other
                // slot now.
                fiber.StackTop--;

                // Return the fiber to resume.
                args[0] = new Value(caller);
            }

            return PrimitiveResult.RunFiber;
        }

        static PrimitiveResult prim_fn_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // Return the Fn class itself. When we then call "new" on it, it will return
            // the block.
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fn_new(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Obj == null || args[1].Obj.Type != ObjType.Fn && args[1].Obj.Type != ObjType.Closure)
            {
                args[0] = new Value("Argument must be a function.");
                return PrimitiveResult.Error;
            }

            // The block argument is already a function, so just return it.
            args[0] = args[1];
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fn_arity(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjFn fn = args[0].Obj as ObjFn;
            args[0] = fn != null ? new Value(fn.Arity) : new Value(0.0);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult CallFn(Value[] args, int numArgs)
        {
            ObjFn fn = args[0].Obj as ObjFn;
            ObjClosure c = args[0].Obj as ObjClosure;
            if (c != null)
            {
                fn = c.Function;
            }

            if (fn != null)
            {
                if (numArgs >= fn.Arity)
                {
                    return PrimitiveResult.Call;
                }

                args[0] = new Value("Function expects more arguments.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Object should be a function or closure");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fn_call0(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 0); }
        static PrimitiveResult prim_fn_call1(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 1); }
        static PrimitiveResult prim_fn_call2(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 2); }
        static PrimitiveResult prim_fn_call3(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 3); }
        static PrimitiveResult prim_fn_call4(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 4); }
        static PrimitiveResult prim_fn_call5(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 5); }
        static PrimitiveResult prim_fn_call6(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 6); }
        static PrimitiveResult prim_fn_call7(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 7); }
        static PrimitiveResult prim_fn_call8(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 8); }
        static PrimitiveResult prim_fn_call9(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 9); }
        static PrimitiveResult prim_fn_call10(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 10); }
        static PrimitiveResult prim_fn_call11(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 11); }
        static PrimitiveResult prim_fn_call12(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 12); }
        static PrimitiveResult prim_fn_call13(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 13); }
        static PrimitiveResult prim_fn_call14(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 14); }
        static PrimitiveResult prim_fn_call15(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 15); }
        static PrimitiveResult prim_fn_call16(WrenVM vm, ObjFiber fiber, Value[] args) { return CallFn(args, 16); }

        static PrimitiveResult prim_fn_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value("<fn>");
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(new ObjList(0));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_add(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list == null)
            {
                args[0] = new Value("Trying to add to a non-list");
                return PrimitiveResult.Error;
            }
            list.Add(args[1]);
            args[0] = args[1];
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_clear(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list == null)
            {
                args[0] = new Value("Trying to clear a non-list");
                return PrimitiveResult.Error;
            }
            list.Clear();

            args[0] = new Value (ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_count(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list != null)
            {
                args[0] = new Value(list.Count());
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Trying to clear a non-list");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_insert(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list != null)
            {
                if (args[1].Type == ValueType.Num)
                {
                    if (args[1].Num == (int)args[1].Num)
                    {
                        int index = (int)args[1].Num;

                        if (index < 0)
                            index += list.Count() + 1;
                        if (index >= 0 && index <= list.Count())
                        {
                            list.Insert(args[2], index);
                            args[0] = args[2];
                            return PrimitiveResult.Value;
                        }
                        args[0] = new Value("Index out of bounds.");
                        return PrimitiveResult.Error;
                    }

                    // count + 1 here so you can "insert" at the very end.
                    args[0] = new Value("Index must be an integer.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Index must be a number.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("List cannot be null");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_iterate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (list.Count() != 0)
                {
                    args[0] = new Value(0.0);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num == ((int)args[1].Num))
                {
                    double index = args[1].Num;
                    if (!(index < 0) && !(index >= list.Count() - 1))
                    {
                        args[0] = new Value(index + 1);
                        return PrimitiveResult.Value;
                    }

                    // Otherwise, move to the next index.
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }

                // Stop if we're out of bounds.
                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_iteratorValue(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num == ((int)args[1].Num))
                {
                    int index = (int)args[1].Num;

                    if (index >= 0 && index < list.Count())
                    {
                        args[0] = list.Get(index);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Iterator out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_removeAt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;

            if (list != null)
            {
                if (args[1].Type == ValueType.Num)
                {
                    if (args[1].Num == ((int)args[1].Num))
                    {
                        int index = (int)args[1].Num;
                        if (index < 0)
                            index += list.Count();
                        if (index >= 0 && index < list.Count())
                        {
                            args[0] = list.RemoveAt(index);
                            return PrimitiveResult.Value;
                        }

                        args[0] = new Value("Index out of bounds.");
                        return PrimitiveResult.Error;
                    }

                    args[0] = new Value("Index must be an integer.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Index must be a number.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("List cannot be null");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_subscript(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;

            if (list == null)
                return PrimitiveResult.Error;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }
                    if (index >= 0 && index < list.Count())
                    {
                        args[0] = list.Get(index);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }

            ObjRange r = args[1].Obj as ObjRange;

            if (r == null)
            {
                args[0] = new Value("Subscript must be a number or a range.");
                return PrimitiveResult.Error;
            }

            // TODO: This is seriously broken and needs a rewrite
            int from = (int)r.From;
            if (from != r.From)
            {
                args[0] = new Value("Range start must be an integer.");
                return PrimitiveResult.Error;
            }
            int to = (int)r.To;
            if (to != r.To)
            {
                args[0] = new Value("Range end must be an integer.");
                return PrimitiveResult.Error;
            }

            if (from < 0)
                from += list.Count();
            if (to < 0)
                to += list.Count();

            int step = to < from ? -1 : 1;

            if (step > 0 && r.IsInclusive)
                to += 1;
            if (step < 0 && !r.IsInclusive)
                to += 1;

            // Handle copying an empty list
            if (list.Count() == 0 && to == (r.IsInclusive ? -1 : 0))
            {
                to = 0;
                step = 1;
            }

            int count = (to - from) * step + (step < 0 ? 1 : 0);

            if (to < 0 || from + (count * step) > list.Count())
            {
                args[0] = new Value("Range end out of bounds.");
                return PrimitiveResult.Error;
            }
            if (from < 0 || (from >= list.Count() && from > 0))
            {
                args[0] = new Value("Range start out of bounds.");
                return PrimitiveResult.Error;
            }

            ObjList result = new ObjList(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(list.Get(from + (i * step)));
            }

            args[0] = new Value(result);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_subscriptSetter(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;
            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }

                    if (list != null && index >= 0 && index < list.Count())
                    {
                        list.Set(args[2], index);
                        args[0] = args[2];
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Subscript must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(new ObjMap());
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_map_subscript(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = args[0].Obj as ObjMap;

            if (ValidateKey(args[1]))
            {
                if (map != null)
                {
                    args[0] = map.Get(args[1]);
                    if (args[0].Type == ValueType.Undefined)
                    {
                        args[0] = new Value (ValueType.Null);
                    }
                }
                else
                {
                    args[0] = new Value (ValueType.Null);
                }
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_subscriptSetter(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = args[0].Obj as ObjMap;

            if (ValidateKey(args[1]))
            {
                if (map != null)
                {
                    map.Set(args[1], args[2]);
                }
                args[0] = args[2];
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_clear(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap m = args[0].Obj as ObjMap;
            if (m != null)
                m.Clear();
            args[0] = new Value (ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_map_containsKey(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (ValidateKey(args[1]))
            {
                Value v = map.Get(args[1]);

                args[0] = new Value(v.Type != ValueType.Undefined);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_count(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap m = (ObjMap)args[0].Obj;
            args[0] = new Value(m.Count());
            return PrimitiveResult.Value;
        }

        private static PrimitiveResult prim_map_iterate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (map.Count() == 0)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            // Start one past the last entry we stopped at.
            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num < 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    args[0] = index > map.Count() || map.Get(index).Type == ValueType.Undefined ? new Value(false) : new Value(index + 1);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            // If we're starting the iteration, start at the first used entry.
            if (args[1].Type == ValueType.Null)
            {
                args[0] = new Value(1);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_remove(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (ValidateKey(args[1]))
            {
                args[0] = map != null ? map.Remove(args[1]) : new Value (ValueType.Null);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_keyIteratorValue(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (map != null && index >= 0)
                    {
                        args[0] = map.GetKey(index - 1);
                        return PrimitiveResult.Value;
                    }
                    args[0] = new Value("Error in prim_map_keyIteratorValue.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_valueIteratorValue(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (map != null && index >= 0 && index < map.Count())
                    {
                        args[0] = map.Get(index - 1);
                        return PrimitiveResult.Value;
                    }
                    args[0] = new Value("Error in prim_map_valueIteratorValue.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_null_not(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(true);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_null_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value("null");
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_fromString(WrenVM vm, ObjFiber fiber, Value[] args)
        {

            ObjString s = args[1].Obj as ObjString;

            if (s != null)
            {
                if (s.Value.Length != 0)
                {
                    double n;

                    if (double.TryParse(s.Value, out n))
                    {
                        args[0] = new Value(n);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value (ValueType.Null);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value (ValueType.Null);
                return PrimitiveResult.Value;
            }

            // Corner case: Can't parse an empty string.
            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_pi(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(3.14159265358979323846);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_minus(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num - args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_plus(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num + args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_multiply(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num * args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_divide(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num / args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_lt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num < args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_gt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num > args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_lte(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num <= args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_gte(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num >= args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_And(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num & (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_Or(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num | (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_Xor(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num ^ (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_LeftShift(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num << (int)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_RightShift(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num >> (int)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_abs(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Abs(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_acos(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Acos(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_asin(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Asin(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_atan(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Atan(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_ceil(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Ceiling(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_cos(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Cos(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_floor(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Floor(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_negate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(-args[0].Num);
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_sin(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Sin(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_sqrt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Sqrt(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_tan(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Tan(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_mod(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num % args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_eqeq(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num == (args[1].Num));
                return PrimitiveResult.Value;
            }

            args[0] = new Value(false);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_bangeq(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num != args[1].Num);
                return PrimitiveResult.Value;
            }

            args[0] = new Value(true);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_bitwiseNot(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(~(Int64)args[0].Num);
            // Bitwise operators always work on 64-bit signed ints.
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_dotDot(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                double from = args[0].Num;
                double to = args[1].Num;
                args[0] = new Value(new ObjRange(@from, to, true));
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right hand side of range must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_dotDotDot(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                double from = args[0].Num;
                double to = args[1].Num;
                args[0] = new Value(new ObjRange(from, to, false));
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right hand side of range must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_atan2(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Atan2(args[0].Num, args[1].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_fraction(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(args[0].Num - Math.Truncate(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_isNan(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(double.IsNaN(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_sign(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            double value = args[0].Num;
            args[0] = new Value(Math.Sign(value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(args[0].Num.ToString(CultureInfo.InvariantCulture));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_truncate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Math.Truncate(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_same(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Value.Equals(args[1], args[2]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_not(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(false);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_eqeq(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Value.Equals(args[0], args[1]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_bangeq(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(!Value.Equals(args[0], args[1]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_is(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Obj as ObjClass != null)
            {
                ObjClass classObj = args[0].GetClass();
                ObjClass baseClassObj = args[1].Obj as ObjClass;

                // Walk the superclass chain looking for the class.
                do
                {
                    if (baseClassObj == classObj)
                    {
                        args[0] = new Value(true);
                        return PrimitiveResult.Value;
                    }

                    classObj = classObj.Superclass;
                } while (classObj != null);

                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right operand must be a class.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_object_new(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // This is the default argument-less constructor that all objects inherit.
            // It just returns "this".
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjClass cClass = args[0].Obj as ObjClass;
            ObjInstance instance = args[0].Obj as ObjInstance;
            if (cClass != null)
            {
                args[0] = new Value(cClass.Name);
            }
            else if (instance != null)
            {
                ObjString name = instance.ClassObj.Name;
                args[0] = new Value(string.Format("instance of {0}", name));
            }
            else
            {
                args[0] = new Value("<object>");
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_type(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(args[0].GetClass());
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value("Must provide a class to 'new' to construct.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_instantiate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value("");
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_from(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).From);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_to(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_min(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;
            args[0] = range.From < range.To ? new Value(range.From) : new Value(range.To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_max(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;
            args[0] = range.From > range.To ? new Value(range.From) : new Value(range.To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_isInclusive(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).IsInclusive);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_iterate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;

            // Special case: empty range.
            if (range.From == range.To && !range.IsInclusive)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            // Start the iteration.
            if (args[1].Type == ValueType.Num)
            {
                double iterator = args[1].Num;

                // Iterate towards [to] from [from].
                if (range.From < range.To)
                {
                    iterator++;
                    if (iterator > range.To)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }
                }
                else
                {
                    iterator--;
                    if (iterator < range.To)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }
                }

                if (!range.IsInclusive && iterator == range.To)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value(iterator);
                return PrimitiveResult.Value;
            }
            if (args[1].Type == ValueType.Null)
            {
                args[0] = new Value(range.From);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_range_iteratorValue(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            // Assume the iterator is a number so that is the value of the range.
            args[0] = args[1];
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjRange range = args[0].Obj as ObjRange;

            if (range != null)
                args[0] = new Value(string.Format("{0}{1}{2}", range.From, range.IsInclusive ? ".." : "...", range.To));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_fromCodePoint(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                int codePoint = (int)args[1].Num;

                if (codePoint == args[1].Num)
                {
                    if (codePoint >= 0)
                    {
                        if (codePoint <= 0x10ffff)
                        {
                            args[0] = ObjString.FromCodePoint(codePoint);
                            return PrimitiveResult.Value;
                        }

                        args[0] = new Value("Code point cannot be greater than 0x10ffff.");
                        return PrimitiveResult.Error;
                    }
                    args[0] = new Value("Code point cannot be negative.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Code point must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Code point must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_byteAt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = args[0].Obj as ObjString;

            if (s == null)
            {
                return PrimitiveResult.Error;
            }

            int index = (int)(args[0].Type == ValueType.Num ? args[0].Num : 0);

            args[0] = new Value(s.ToString()[index]);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_codePointAt(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = args[0].Obj as ObjString;

            if (s == null)
            {
                return PrimitiveResult.Error;
            }

            if (args[1].Type != ValueType.Num)
            {
                args[0] = new Value("Index must be a number.");
                return PrimitiveResult.Error;
            }

            int index = (int)args[1].Num;

            if (index != args[1].Num)
            {
                args[0] = new Value("Index must be an integer.");
                return PrimitiveResult.Error;
            }

            if (index < 0 || index >= s.Value.Length)
            {
                args[0] = new Value("Index out of bounds.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value[index]);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_contains(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search == null)
            {
                args[0] = new Value("Argument must be a string.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value.Contains(search.Value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_count(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(args[0].Obj.ToString().Length);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_endsWith(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search == null)
            {
                args[0] = new Value("Argument must be a string.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value.EndsWith(search.Value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_indexOf(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search != null)
            {
                int index = s.Value.IndexOf(search.Value, StringComparison.Ordinal);
                args[0] = new Value(index);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_iterate(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (s.Value.Length != 0)
                {
                    args[0] = new Value(0.0);
                    return PrimitiveResult.Value;
                }
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num < 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    index++;
                    if (index >= s.Value.Length)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value(index);
                    return PrimitiveResult.Value;
                }

                // Advance to the beginning of the next UTF-8 sequence.
                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_iterateByte(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (s.Value.Length == 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                args[0] = new Value(0.0);
                return PrimitiveResult.Value;
            }

            if (args[1].Type != ValueType.Num) return PrimitiveResult.Error;

            if (args[1].Num < 0)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }
            int index = (int)args[1].Num;

            // Advance to the next byte.
            index++;
            if (index >= s.Value.Length)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            args[0] = new Value(index);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_iteratorValue(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < s.Value.Length && index >= 0)
                    {
                        args[0] = new Value("" + s.Value[index]);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Iterator out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_startsWith(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search != null)
            {
                args[0] = new Value(s.Value.StartsWith(search.Value));
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_toString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_plus(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            ObjString s1 = args[1].Obj as ObjString;
            if (s1 != null)
            {
                args[0] = new Value(((ObjString)args[0].Obj).Value + s1.Value);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right operand must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_subscript(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            string s = ((ObjString)args[0].Obj).Value;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += s.Length;
                    }

                    if (index >= 0 && index < s.Length)
                    {
                        args[0] = ObjString.FromCodePoint(s[index]);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }

            if (args[1].Obj as ObjRange != null)
            {
                args[0] = new Value("Subscript ranges for strings are not implemented yet.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Subscript must be a number or a range.");
            return PrimitiveResult.Error;
        }

        // Creates either the Object or Class class in the core library with [name].
        static ObjClass DefineClass(WrenVM vm, string name)
        {
            ObjString nameString = new ObjString(name);

            ObjClass classObj = new ObjClass(0, nameString);

            vm.DefineVariable(null, name, new Value(classObj));

            return classObj;
        }

        static bool ValidateKey(Value arg)
        {
            return arg.Type == ValueType.False
                   || arg.Type == ValueType.True
                   || arg.Type == ValueType.Num
                   || arg.Type == ValueType.Null
                   || arg.Obj is ObjClass || arg.Obj is ObjFiber
                   || arg.Obj is ObjRange || arg.Obj is ObjString;
        }

        public CoreLibrary(WrenVM v)
        {
            vm = v;
        }

        public void InitializeCore()
        {
            // Define the root Object class. This has to be done a little specially
            // because it has no superclass.
            WrenVM.ObjectClass = DefineClass(vm, "Object");
            vm.Primitive(WrenVM.ObjectClass, "!", prim_object_not);
            vm.Primitive(WrenVM.ObjectClass, "==(_)", prim_object_eqeq);
            vm.Primitive(WrenVM.ObjectClass, "!=(_)", prim_object_bangeq);
            vm.Primitive(WrenVM.ObjectClass, "new", prim_object_new);
            vm.Primitive(WrenVM.ObjectClass, "is(_)", prim_object_is);
            vm.Primitive(WrenVM.ObjectClass, "toString", prim_object_toString);
            vm.Primitive(WrenVM.ObjectClass, "type", prim_object_type);
            vm.Primitive(WrenVM.ObjectClass, "<instantiate>", prim_object_instantiate);

            // Now we can define Class, which is a subclass of Object.
            WrenVM.ClassClass = DefineClass(vm, "Class");
            WrenVM.ClassClass.BindSuperclass(WrenVM.ObjectClass);
            // Store a copy of the class in ObjClass
            ObjClass.ClassClass = WrenVM.ClassClass;
            // Define the primitives
            vm.Primitive(WrenVM.ClassClass, "<instantiate>", prim_class_instantiate);
            vm.Primitive(WrenVM.ClassClass, "name", prim_class_name);
            vm.Primitive(WrenVM.ClassClass, "supertype", prim_class_supertype);

            // Finally, we can define Object's metaclass which is a subclass of Class.
            ObjClass objectMetaclass = DefineClass(vm, "Object metaclass");

            // Wire up the metaclass relationships now that all three classes are built.
            WrenVM.ObjectClass.ClassObj = objectMetaclass;
            objectMetaclass.ClassObj = WrenVM.ClassClass;
            WrenVM.ClassClass.ClassObj = WrenVM.ClassClass;

            // Do this after wiring up the metaclasses so objectMetaclass doesn't get
            // collected.
            objectMetaclass.BindSuperclass(WrenVM.ClassClass);

            vm.Primitive(objectMetaclass, "same(_,_)", prim_object_same);

            // The core class diagram ends up looking like this, where single lines point
            // to a class's superclass, and double lines point to its metaclass:
            //
            //        .------------------------------------. .====.
            //        |                  .---------------. | #    #
            //        v                  |               v | v    #
            //   .---------.   .-------------------.   .-------.  #
            //   | Object  |==>| Object metaclass  |==>| Class |=="
            //   '---------'   '-------------------'   '-------'
            //        ^                                 ^ ^ ^ ^
            //        |                  .--------------' # | #
            //        |                  |                # | #
            //   .---------.   .-------------------.      # | # -.
            //   |  Base   |==>|  Base metaclass   |======" | #  |
            //   '---------'   '-------------------'        | #  |
            //        ^                                     | #  |
            //        |                  .------------------' #  | Example classes
            //        |                  |                    #  |
            //   .---------.   .-------------------.          #  |
            //   | Derived |==>| Derived metaclass |=========="  |
            //   '---------'   '-------------------'            -'

            // The rest of the classes can now be defined normally.
            vm.Interpret("", coreLibSource);

            WrenVM.BoolClass = (ObjClass)vm.FindVariable("Bool").Obj;
            vm.Primitive(WrenVM.BoolClass, "toString", prim_bool_toString);
            vm.Primitive(WrenVM.BoolClass, "!", prim_bool_not);

            WrenVM.FiberClass = (ObjClass)vm.FindVariable("Fiber").Obj;
            vm.Primitive(WrenVM.FiberClass.ClassObj, "<instantiate>", prim_fiber_instantiate);
            vm.Primitive(WrenVM.FiberClass.ClassObj, "new(_)", prim_fiber_new);
            vm.Primitive(WrenVM.FiberClass.ClassObj, "abort(_)", prim_fiber_abort);
            vm.Primitive(WrenVM.FiberClass.ClassObj, "current", prim_fiber_current);
            vm.Primitive(WrenVM.FiberClass.ClassObj, "yield()", prim_fiber_yield);
            vm.Primitive(WrenVM.FiberClass.ClassObj, "yield(_)", prim_fiber_yield1);
            vm.Primitive(WrenVM.FiberClass, "call()", prim_fiber_call);
            vm.Primitive(WrenVM.FiberClass, "call(_)", prim_fiber_call1);
            vm.Primitive(WrenVM.FiberClass, "error", prim_fiber_error);
            vm.Primitive(WrenVM.FiberClass, "isDone", prim_fiber_isDone);
            vm.Primitive(WrenVM.FiberClass, "run()", prim_fiber_run);
            vm.Primitive(WrenVM.FiberClass, "run(_)", prim_fiber_run1);
            vm.Primitive(WrenVM.FiberClass, "try()", prim_fiber_try);

            WrenVM.FnClass = (ObjClass)vm.FindVariable("Fn").Obj;
            vm.Primitive(WrenVM.FnClass.ClassObj, "<instantiate>", prim_fn_instantiate);
            vm.Primitive(WrenVM.FnClass.ClassObj, "new(_)", prim_fn_new);

            vm.Primitive(WrenVM.FnClass, "arity", prim_fn_arity);
            vm.Primitive(WrenVM.FnClass, "call()", prim_fn_call0);
            vm.Primitive(WrenVM.FnClass, "call(_)", prim_fn_call1);
            vm.Primitive(WrenVM.FnClass, "call(_,_)", prim_fn_call2);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_)", prim_fn_call3);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_)", prim_fn_call4);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_)", prim_fn_call5);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_)", prim_fn_call6);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_)", prim_fn_call7);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_)", prim_fn_call8);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_)", prim_fn_call9);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_)", prim_fn_call10);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call11);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call12);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call13);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call14);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call15);
            vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call16);
            vm.Primitive(WrenVM.FnClass, "toString", prim_fn_toString);

            WrenVM.NullClass = (ObjClass)vm.FindVariable("Null").Obj;
            vm.Primitive(WrenVM.NullClass, "!", prim_null_not);
            vm.Primitive(WrenVM.NullClass, "toString", prim_null_toString);

            WrenVM.NumClass = (ObjClass)vm.FindVariable("Num").Obj;
            vm.Primitive(WrenVM.NumClass.ClassObj, "fromString(_)", prim_num_fromString);
            vm.Primitive(WrenVM.NumClass.ClassObj, "pi", prim_num_pi);
            vm.Primitive(WrenVM.NumClass, "-(_)", prim_num_minus);
            vm.Primitive(WrenVM.NumClass, "+(_)", prim_num_plus);
            vm.Primitive(WrenVM.NumClass, "*(_)", prim_num_multiply);
            vm.Primitive(WrenVM.NumClass, "/(_)", prim_num_divide);
            vm.Primitive(WrenVM.NumClass, "<(_)", prim_num_lt);
            vm.Primitive(WrenVM.NumClass, ">(_)", prim_num_gt);
            vm.Primitive(WrenVM.NumClass, "<=(_)", prim_num_lte);
            vm.Primitive(WrenVM.NumClass, ">=(_)", prim_num_gte);
            vm.Primitive(WrenVM.NumClass, "&(_)", prim_num_And);
            vm.Primitive(WrenVM.NumClass, "|(_)", prim_num_Or);
            vm.Primitive(WrenVM.NumClass, "^(_)", prim_num_Xor);
            vm.Primitive(WrenVM.NumClass, "<<(_)", prim_num_LeftShift);
            vm.Primitive(WrenVM.NumClass, ">>(_)", prim_num_RightShift);
            vm.Primitive(WrenVM.NumClass, "abs", prim_num_abs);
            vm.Primitive(WrenVM.NumClass, "acos", prim_num_acos);
            vm.Primitive(WrenVM.NumClass, "asin", prim_num_asin);
            vm.Primitive(WrenVM.NumClass, "atan", prim_num_atan);
            vm.Primitive(WrenVM.NumClass, "ceil", prim_num_ceil);
            vm.Primitive(WrenVM.NumClass, "cos", prim_num_cos);
            vm.Primitive(WrenVM.NumClass, "floor", prim_num_floor);
            vm.Primitive(WrenVM.NumClass, "-", prim_num_negate);
            vm.Primitive(WrenVM.NumClass, "sin", prim_num_sin);
            vm.Primitive(WrenVM.NumClass, "sqrt", prim_num_sqrt);
            vm.Primitive(WrenVM.NumClass, "tan", prim_num_tan);
            vm.Primitive(WrenVM.NumClass, "%(_)", prim_num_mod);
            vm.Primitive(WrenVM.NumClass, "~", prim_num_bitwiseNot);
            vm.Primitive(WrenVM.NumClass, "..(_)", prim_num_dotDot);
            vm.Primitive(WrenVM.NumClass, "...(_)", prim_num_dotDotDot);
            vm.Primitive(WrenVM.NumClass, "atan(_)", prim_num_atan2);
            vm.Primitive(WrenVM.NumClass, "fraction", prim_num_fraction);
            vm.Primitive(WrenVM.NumClass, "isNan", prim_num_isNan);
            vm.Primitive(WrenVM.NumClass, "sign", prim_num_sign);
            vm.Primitive(WrenVM.NumClass, "toString", prim_num_toString);
            vm.Primitive(WrenVM.NumClass, "truncate", prim_num_truncate);

            // These are defined just so that 0 and -0 are equal, which is specified by
            // IEEE 754 even though they have different bit representations.
            vm.Primitive(WrenVM.NumClass, "==(_)", prim_num_eqeq);
            vm.Primitive(WrenVM.NumClass, "!=(_)", prim_num_bangeq);

            WrenVM.StringClass = (ObjClass)vm.FindVariable("String").Obj;
            vm.Primitive(WrenVM.StringClass.ClassObj, "fromCodePoint(_)", prim_string_fromCodePoint);
            vm.Primitive(WrenVM.StringClass.ClassObj, "<instantiate>", prim_string_instantiate);
            vm.Primitive(WrenVM.StringClass, "+(_)", prim_string_plus);
            vm.Primitive(WrenVM.StringClass, "[_]", prim_string_subscript);
            vm.Primitive(WrenVM.StringClass, "byteAt(_)", prim_string_byteAt);
            vm.Primitive(WrenVM.StringClass, "codePointAt(_)", prim_string_codePointAt);
            vm.Primitive(WrenVM.StringClass, "contains(_)", prim_string_contains);
            vm.Primitive(WrenVM.StringClass, "count", prim_string_count);
            vm.Primitive(WrenVM.StringClass, "endsWith(_)", prim_string_endsWith);
            vm.Primitive(WrenVM.StringClass, "indexOf(_)", prim_string_indexOf);
            vm.Primitive(WrenVM.StringClass, "iterate(_)", prim_string_iterate);
            vm.Primitive(WrenVM.StringClass, "iterateByte_(_)", prim_string_iterateByte);
            vm.Primitive(WrenVM.StringClass, "iteratorValue(_)", prim_string_iteratorValue);
            vm.Primitive(WrenVM.StringClass, "startsWith(_)", prim_string_startsWith);
            vm.Primitive(WrenVM.StringClass, "toString", prim_string_toString);

            WrenVM.ListClass = (ObjClass)vm.FindVariable("List").Obj;
            vm.Primitive(WrenVM.ListClass.ClassObj, "<instantiate>", prim_list_instantiate);
            vm.Primitive(WrenVM.ListClass, "[_]", prim_list_subscript);
            vm.Primitive(WrenVM.ListClass, "[_]=(_)", prim_list_subscriptSetter);
            vm.Primitive(WrenVM.ListClass, "add(_)", prim_list_add);
            vm.Primitive(WrenVM.ListClass, "clear()", prim_list_clear);
            vm.Primitive(WrenVM.ListClass, "count", prim_list_count);
            vm.Primitive(WrenVM.ListClass, "insert(_,_)", prim_list_insert);
            vm.Primitive(WrenVM.ListClass, "iterate(_)", prim_list_iterate);
            vm.Primitive(WrenVM.ListClass, "iteratorValue(_)", prim_list_iteratorValue);
            vm.Primitive(WrenVM.ListClass, "removeAt(_)", prim_list_removeAt);

            WrenVM.MapClass = (ObjClass)vm.FindVariable("Map").Obj;
            vm.Primitive(WrenVM.MapClass.ClassObj, "<instantiate>", prim_map_instantiate);
            vm.Primitive(WrenVM.MapClass, "[_]", prim_map_subscript);
            vm.Primitive(WrenVM.MapClass, "[_]=(_)", prim_map_subscriptSetter);
            vm.Primitive(WrenVM.MapClass, "clear()", prim_map_clear);
            vm.Primitive(WrenVM.MapClass, "containsKey(_)", prim_map_containsKey);
            vm.Primitive(WrenVM.MapClass, "count", prim_map_count);
            vm.Primitive(WrenVM.MapClass, "remove(_)", prim_map_remove);
            vm.Primitive(WrenVM.MapClass, "iterate_(_)", prim_map_iterate);
            vm.Primitive(WrenVM.MapClass, "keyIteratorValue_(_)", prim_map_keyIteratorValue);
            vm.Primitive(WrenVM.MapClass, "valueIteratorValue_(_)", prim_map_valueIteratorValue);

            WrenVM.RangeClass = (ObjClass)vm.FindVariable("Range").Obj;
            vm.Primitive(WrenVM.RangeClass, "from", prim_range_from);
            vm.Primitive(WrenVM.RangeClass, "to", prim_range_to);
            vm.Primitive(WrenVM.RangeClass, "min", prim_range_min);
            vm.Primitive(WrenVM.RangeClass, "max", prim_range_max);
            vm.Primitive(WrenVM.RangeClass, "isInclusive", prim_range_isInclusive);
            vm.Primitive(WrenVM.RangeClass, "iterate(_)", prim_range_iterate);
            vm.Primitive(WrenVM.RangeClass, "iteratorValue(_)", prim_range_iteratorValue);
            vm.Primitive(WrenVM.RangeClass, "toString", prim_range_toString);

            // While bootstrapping the core types and running the core library, a number
            // of string objects have been created, many of which were instantiated
            // before stringClass was stored in the VM. Some of them *must* be created
            // first -- the ObjClass for string itself has a reference to the ObjString
            // for its name.
            //
            // These all currently have a NULL classObj pointer, so go back and assign
            // them now that the string class is known.
            ObjString.InitClass();

            WrenVM.ClassClass.IsSealed = true;
            WrenVM.FiberClass.IsSealed = true;
            WrenVM.FnClass.IsSealed = true;
            WrenVM.ListClass.IsSealed = true;
            WrenVM.MapClass.IsSealed = true;
            WrenVM.RangeClass.IsSealed = true;
            WrenVM.StringClass.IsSealed = true;
        }
    }
}
