using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    // A hash table mapping keys to values.
    //
    // We use something very simple: open addressing with linear probing. The hash
    // table is an array of entries. Each entry is a key-value pair. If the key is
    // the special UNDEFINED_VAL, it indicates no value is currently in that slot.
    // Otherwise, it's a valid key, and the value is the value associated with it.
    //
    // When entries are added, the array is dynamically scaled by GROW_FACTOR to
    // keep the number of filled slots under MAP_LOAD_PERCENT. Likewise, if the map
    // gets empty enough, it will be resized to a smaller array. When this happens,
    // all existing entries are rehashed and re-added to the new array.
    //
    // When an entry is removed, its slot is replaced with a "tombstone". This is an
    // entry whose key is UNDEFINED_VAL and whose value is TRUE_VAL. When probing
    // for a key, we will continue past tombstones, because the desired key may be
    // found after them if the key that was removed was part of a prior collision.
    // When the array gets resized, all tombstones are discarded.
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
