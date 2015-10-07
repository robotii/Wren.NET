using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjList : Obj
    {
        // The elements in the list.
        readonly List<Obj> _elements;

        // Creates a new list with [numElements] elements (which are left
        // uninitialized.)
        public ObjList(int numElements)
            : base(ObjType.Obj)
        {
            _elements = new List<Obj>(numElements);
            ClassObj = WrenVM.ListClass;
        }

        public void Clear()
        {
            _elements.Clear();
        }

        public int Count()
        {
            return _elements.Count;
        }

        public Obj Get(int index)
        {
            return _elements[index];
        }

        public void Set(Obj v, int index)
        {
            _elements[index] = v;
        }

        // Inserts [value] in [list] at [index], shifting down the other elements.
        public void Insert(Obj c, int index)
        {
            _elements.Insert(index, c);
        }

        public void Add(Obj v)
        {
            _elements.Add(v);
        }

        // Removes and returns the item at [index] from [list].
        public Obj RemoveAt(int index)
        {
            if (_elements.Count > index)
            {
                Obj v = _elements[index];
                _elements.RemoveAt(index);
                return v;
            }
            return new Obj(ObjType.Null);
        }

    }
}
