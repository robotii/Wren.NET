using System;
using System.IO;
using System.Reflection;
using Wren.Core.VM;

namespace Wren.Core.Library
{
    [AttributeUsage(AttributeTargets.Class)]
    public class LoadLibrary : Attribute
    {
    }

    public class LibraryLoader
    {
        public static void LoadLibraries(WrenVM vm)
        {
            Type wrenLibrary = Type.GetType("Wren.Core.Library.LoadLibrary");

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var di = new DirectoryInfo(path);
            foreach (var file in di.GetFiles("*.dll"))
            {
                try
                {
                    var nextAssembly = Assembly.LoadFrom(file.FullName);

                    foreach (var type in nextAssembly.GetTypes())
                    {
                        if (type.GetCustomAttributes(wrenLibrary, false).Length > 0)
                        {
                            // This class implements the interface
                            var m = type.GetMethod("LoadLibrary");
                            m.Invoke(null, new object[] {vm});
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a .net assembly  - ignore
                }
            }
        }

        public static void LoadLibrary(WrenVM vm, string libraryName, string typeName)
        {
            Type wrenLibrary = Type.GetType("Wren.Core.Library.LoadLibrary");

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var di = new DirectoryInfo(path);
            foreach (var file in di.GetFiles(libraryName))
            {
                try
                {
                    var nextAssembly = Assembly.LoadFrom(file.FullName);

                    foreach (var type in nextAssembly.GetTypes())
                    {
                        if (type.GetCustomAttributes(wrenLibrary, false).Length > 0)
                        {
                            // This class implements the interface
                            var m = type.GetMethod("LoadLibrary");
                            m.Invoke(null, new object[] {vm});
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a .net assembly  - ignore
                }
            }
        }
    }

}
