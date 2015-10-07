using System.Collections.Generic;

namespace Wren.Core.Objects
{
    // A loaded module and the top-level variables it defines.
    public class ObjModule: Obj
    {
        public const int MaxModuleVars = 65536;

        // The currently defined top-level variables.
        public List<ModuleVariable> Variables;

        // The name of the module.
        public ObjString Name;

        // Creates a new module.
        public ObjModule(ObjString name) : base(ObjType.Obj)
        {
            Name = name;
            Variables = new List<ModuleVariable>();
        }
    }

    public class ModuleVariable
    {
        public string Name;

        internal Obj Container;
    }
}
