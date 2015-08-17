using System;
using Wren.Core.Objects;
using Wren.Core.VM;
using ValueType = Wren.Core.VM.ValueType;

namespace Wren.Core.Library
{
    class IO
    {
        const string IOLibSource =
        "class IO {\n"
        + "  static print {\n"
        + "    IO.writeString_(\"\n\")\n"
        + "  }\n"
        + "\n"
        + "  static print(obj) {\n"
        + "    IO.writeObject_(obj)\n"
        + "    IO.writeString_(\"\n\")\n"
        + "    return obj\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2) {\n"
        + "    printList_([a1, a2])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3) {\n"
        + "    printList_([a1, a2, a3])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4) {\n"
        + "    printList_([a1, a2, a3, a4])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5) {\n"
        + "    printList_([a1, a2, a3, a4, a5])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15])\n"
        + "  }\n"
        + "\n"
        + "  static print(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16) {\n"
        + "    printList_([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16])\n"
        + "  }\n"
        + "\n"
        + "  static printList_(objects) {\n"
        + "    for (object in objects) IO.writeObject_(object)\n"
        + "    IO.writeString_(\"\n\")\n"
        + "  }\n"
        + "\n"
        + "  static write(obj) {\n"
        + "    IO.writeObject_(obj)\n"
        + "    return obj\n"
        + "  }\n"
        + "\n"
        + "  static read(prompt) {\n"
        + "    if (!(prompt is String)) Fiber.abort(\"Prompt must be a string.\")\n"
        + "    IO.write(prompt)\n"
        + "    return IO.read\n"
        + "  }\n"
        + "\n"
        + "  static writeObject_(obj) {\n"
        + "    var string = obj.toString\n"
        + "    if (string is String) {\n"
        + "      IO.writeString_(string)\n"
        + "    } else {\n"
        + "      IO.writeString_(\"[invalid toString]\")\n"
        + "    }\n"
        + "  }\n"
        + "\n"
        + "}\n";

        static PrimitiveResult WriteString(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            if (args[1] != null && args[1].Type == ValueType.Obj)
            {
                string s = args[1].Obj.ToString();
                Console.Write(s);
            }
            args[0] = new Value (ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult Read(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value(Console.ReadLine());
            if (((ObjString)args[0].Obj).Value == "")
            {
                args[0] = new Value (ValueType.Null);
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult Clock(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value((double)DateTime.Now.Ticks / 10000000);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult Time(WrenVM vm, ObjFiber fiber, Value[] args)
        {
            args[0] = new Value((double)DateTime.Now.Ticks / 10000000);
            return PrimitiveResult.Value;
        }

        public static void LoadIOLibrary(WrenVM vm)
        {
            vm.Interpret("", IOLibSource);
            ObjClass IO = (ObjClass)vm.FindVariable("IO").Obj;
            vm.Primitive(IO.ClassObj, "writeString_(_)", WriteString);
            vm.Primitive(IO.ClassObj, "read", Read);
            vm.Primitive(IO.ClassObj, "clock", Clock);
            vm.Primitive(IO.ClassObj, "time", Time);
        }
    }
}
