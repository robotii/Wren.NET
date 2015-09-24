using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjMap : Obj
    {

        // Pointer to a contiguous array of [capacity] entries.
        Dictionary<Value,Value> _entries;

        // Looks up [key] in [map]. If found, returns the value. Otherwise, returns UNDEFINED.
        public Value Get(Value key)
        {
            Value v;
            return _entries.TryGetValue(key, out v) ? v : new Value();
        }

        // Creates a new empty map.
        public ObjMap()
        {
            _entries = new Dictionary<Value, Value>(new ValueComparer());
            ClassObj = WrenVM.MapClass;
        }

        public int Count()
        {
            return _entries.Count;
        }

        public Value Get(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return new Value();
            Value[] v = new Value[_entries.Count];
            _entries.Values.CopyTo(v, 0);
            return v[index];
        }

        public Value GetKey(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return new Value();
            Value[] v = new Value[_entries.Count];
            _entries.Keys.CopyTo(v, 0);
            return v[index];
        }

        // Associates [key] with [value] in [map].
        public void Set(Value key, Value c)
        {
            _entries[key] = c;
        }

        public void Clear()
        {
            _entries = new Dictionary<Value, Value>(new ValueComparer());
        }

        // Removes [key] from [map], if present. Returns the value for the key if found
        // or `NULL_VAL` otherwise.
        public Value Remove(Value key)
        {
            Value v;
            if (_entries.TryGetValue(key, out v))
            {
                _entries.Remove(key);
                return v;
            }
            return new Value (ValueType.Null);
        }
    }
}
