using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    // A loaded module and the top-level variables it defines.
    public class ObjModule: Obj
    {
        public const int MAX_MODULE_VARS = 65536;

        // The currently defined top-level variables.

        // The name of the module.

        // Creates a new module.
        public ObjModule(ObjString name)
        {
            Name = name;
            Variables = new List<ModuleVariable>();
        }

        public List<ModuleVariable> Variables;

        public ObjString Name;
    }

    public class ModuleVariable
    {
        public string Name;

        internal Value Container;
    }
}
