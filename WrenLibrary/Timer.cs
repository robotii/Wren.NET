using Wren.Core.Objects;
using Wren.Core.VM;

namespace WrenLibrary
{
    public static class Timer
    {
        private static string _timerSource =
            "import \"scheduler\" for Scheduler\n"
            + "\n"
            + "class Timer {\n"
            + "  static sleep(milliseconds) {\n"
            + "    if (!(milliseconds is Num)) Fiber.abort(\"Milliseconds must be a number.\")\n"
            + "    if (milliseconds < 0) Fiber.abort(\"Milliseconds cannot be negative.\")\n"
            + "\n"
            + "    startTimer_(milliseconds, Fiber.current)\n"
            + "    Scheduler.runNextScheduled_()\n"
            + "  }\n"
            + "\n"
            + "  foreign static startTimer_(milliseconds, fiber)\n"
            + "}\n";

        public static void LoadLibrary(WrenVM vm)
        {
            vm.Interpret("timer", "timer", _timerSource);
            ObjClass timer = (ObjClass)vm.FindVariable("timer", "Timer");
            vm.Primitive(timer.ClassObj, "startTimer_(_,_)", StartTimer);
        }

        private static bool StartTimer(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }
    }
}
