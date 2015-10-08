namespace Wren.Core.Objects
{
    public class ObjInstance : Obj
    {
        // Creates a new instance of the given [classObj].
        public ObjInstance(ObjClass classObj)
        {
            Fields = new Obj[classObj.NumFields];

            // Initialize fields to null.
            for (int i = 0; i < classObj.NumFields; i++)
            {
                Fields[i] = Null;
            }
            ClassObj = classObj;
        }

        public Obj[] Fields;
    }
}
