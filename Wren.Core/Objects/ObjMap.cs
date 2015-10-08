using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjMap : Obj
    {

        // Pointer to a contiguous array of [capacity] entries.
        Dictionary<Obj,Obj> _entries;

        // Looks up [key] in [map]. If found, returns the value. Otherwise, returns UNDEFINED.
        public Obj Get(Obj key)
        {
            Obj v;
            return _entries.TryGetValue(key, out v) ? v : Undefined;
        }

        // Creates a new empty map.
        public ObjMap()
        {
            _entries = new Dictionary<Obj, Obj>(new ObjComparer());
            ClassObj = WrenVM.MapClass;
        }

        public int Count()
        {
            return _entries.Count;
        }

        public Obj Get(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return Undefined;
            Obj[] v = new Obj[_entries.Count];
            _entries.Values.CopyTo(v, 0);
            return v[index];
        }

        public Obj GetKey(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return Undefined;
            Obj[] v = new Obj[_entries.Count];
            _entries.Keys.CopyTo(v, 0);
            return v[index];
        }

        // Associates [key] with [value] in [map].
        public void Set(Obj key, Obj c)
        {
            _entries[key] = c;
        }

        public void Clear()
        {
            _entries = new Dictionary<Obj, Obj>(new ObjComparer());
        }

        // Removes [key] from [map], if present. Returns the value for the key if found
        // or `NULL_VAL` otherwise.
        public Obj Remove(Obj key)
        {
            Obj v;
            if (_entries.TryGetValue(key, out v))
            {
                _entries.Remove(key);
                return v;
            }
            return Null;
        }
    }
}
