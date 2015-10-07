using Wren.Core.Objects;
using Wren.Core.VM;

namespace WrenLibrary
{
    class Io
    {
        private static readonly string _ioSource =
            "import \"scheduler\" for Scheduler\n"
            + "\n"
            + "foreign class File {\n"
            + "  static open(path) {\n"
            + "    if (!(path is String)) Fiber.abort(\"Path must be a string.\")\n"
            + "\n"
            + "    open_(path, Fiber.current)\n"
            + "    var fd = Scheduler.runNextScheduled_()\n"
            + "    return new_(fd)\n"
            + "  }\n"
            + "\n"
            + "  static open(path, fn) {\n"
            + "    var file = open(path)\n"
            + "    var fiber = Fiber.new { fn.call(file) }\n"
            + "\n"
            + "    // Poor man's finally. Can we make this more elegant?\n"
            + "    var result = fiber.try()\n"
            + "    file.close()\n"
            + "\n"
            + "    // TODO: Want something like rethrow since now the callstack ends here. :(\n"
            + "    if (fiber.error != null) Fiber.abort(fiber.error)\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  static read(path) {\n"
            + "    return File.open(path) {|file| file.readBytes(file.size) }\n"
            + "  }\n"
            + "\n"
            + "  static size(path) {\n"
            + "    if (!(path is String)) Fiber.abort(\"Path must be a string.\")\n"
            + "\n"
            + "    sizePath_(path, Fiber.current)\n"
            + "    return Scheduler.runNextScheduled_()\n"
            + "  }\n"
            + "\n"
            + "  construct new_(fd) {}\n"
            + "\n"
            + "  close() {\n"
            + "    if (close_(Fiber.current)) return\n"
            + "    Scheduler.runNextScheduled_()\n"
            + "  }\n"
            + "\n"
            + "  isOpen { descriptor != -1 }\n"
            + "\n"
            + "  size {\n"
            + "    if (!isOpen) Fiber.abort(\"File is not open.\")\n"
            + "\n"
            + "    size_(Fiber.current)\n"
            + "    return Scheduler.runNextScheduled_()\n"
            + "  }\n"
            + "\n"
            + "  readBytes(count) {\n"
            + "    if (!isOpen) Fiber.abort(\"File is not open.\")\n"
            + "    if (!(count is Num)) Fiber.abort(\"Count must be an integer.\")\n"
            + "    if (!count.isInteger) Fiber.abort(\"Count must be an integer.\")\n"
            + "    if (count < 0) Fiber.abort(\"Count cannot be negative.\")\n"
            + "\n"
            + "    readBytes_(count, Fiber.current)\n"
            + "    return Scheduler.runNextScheduled_()\n"
            + "  }\n"
            + "\n"
            + "  foreign static open_(path, fiber)\n"
            + "  foreign static sizePath_(path, fiber)\n"
            + "\n"
            + "  foreign close_(fiber)\n"
            + "  foreign descriptor\n"
            + "  foreign readBytes_(count, fiber)\n"
            + "  foreign size_(fiber)\n"
            + "}\n";

        public static void LoadLibrary(WrenVM vm)
        {
            vm.Interpret("io", "io", _ioSource);
            ObjClass file = (ObjClass)vm.FindVariable("io", "File");
            vm.Primitive(file.ClassObj, "open_(_,_)", Open);
            vm.Primitive(file.ClassObj, "sizePath_(_,_)", SizePath);

            vm.Primitive(file, "close_(_)", Close);
            vm.Primitive(file, "descriptor", Descriptor);
            vm.Primitive(file, "readBytes_(_,_)", ReadBytes);
            vm.Primitive(file, "size_(_)", Size);
        }

        private static bool Open(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        private static bool SizePath(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        private static bool Close(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        private static bool Descriptor(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        private static bool ReadBytes(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        private static bool Size(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }
    }
}
