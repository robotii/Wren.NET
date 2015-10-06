using Wren.Core.Library;
using Wren.Core.VM;

namespace WrenLibrary
{
    [LoadLibrary]
    class WrenLibrary
    {
        public static void LoadLibrary(WrenVM vm)
        {
            Scheduler.LoadLibrary(vm);
            io.LoadLibrary(vm);
            Timer.LoadLibrary(vm);
        }
    }
}
