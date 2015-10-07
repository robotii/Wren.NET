using Wren.Core.Objects;
using Wren.Core.VM;

namespace WrenLibrary
{
    public static class Scheduler
    {
        private static string _schedulerSource =
            "class Scheduler {\n"
            + "  static add(callable) {\n"
            + "    if (__scheduled == null) __scheduled = []\n"
            + "\n"
            + "    __scheduled.add(Fiber.new {\n"
            + "      callable.call()\n"
            + "      runNextScheduled_()\n"
            + "    })\n"
            + "  }\n"
            + "\n"
            + "  // Called by native code.\n"
            + "  static resume_(fiber) { fiber.transfer() }\n"
            + "  static resume_(fiber, arg) { fiber.transfer(arg) }\n"
            + "  static resumeError_(fiber, error) { fiber.transferError(error) }\n"
            + "\n"
            + "  static runNextScheduled_() {\n"
            + "    if (__scheduled == null || __scheduled.isEmpty) {\n"
            + "      return Fiber.suspend()\n"
            + "    } else {\n"
            + "      return __scheduled.removeAt(0).transfer()\n"
            + "    }\n"
            + "  }\n"
            + "\n"
            + "  foreign static captureMethods_()\n"
            + "}\n"
            + "\n"
            ;

        public static void LoadLibrary(WrenVM vm)
        {
            vm.Interpret("scheduler", "scheduler", _schedulerSource);
            ObjClass scheduler = (ObjClass)vm.FindVariable("scheduler", "Scheduler");
            vm.Primitive(scheduler.ClassObj, "captureMethods_()", CaptureMethods);
            vm.Interpret("scheduler", "scheduler", "Scheduler.captureMethods_()");
        }

        private static bool CaptureMethods(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }
    }
}
