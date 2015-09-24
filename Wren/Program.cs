using System;
using System.IO;
using Wren.Core.VM;

namespace Wren
{
    class Program
    {
        private static string _loadedFile;

        static int Main(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    RunRepl();
                    break;
                case 1:
                    int r = RunFile(args[0]);
                    return r;
                default:
                    Console.WriteLine("Usage: wren [file]\n");
                    return 1; // EX_USAGE.
            }
            return 0;
        }

        static int RunFile(string path)
        {
            if (File.Exists(path))
            {
                _loadedFile = path;
                string source = File.ReadAllText(path);
                WrenVM vm = new WrenVM { LoadModuleFn = LoadModule };
                return (int)vm.Interpret(path, source);
            }
            return 66; // File Not Found
        }

        static void RunRepl()
        {
            WrenVM vm = new WrenVM();

            Console.WriteLine("-- wren v0.0.0");

            string line = "";

            for (; line != "/exit"; )
            {
                Console.Write("> ");
                line = Console.ReadLine();

                // TODO: Handle failure.
                vm.Interpret("Prompt", line);
            }
        }

        static string LoadModule(string name)
        {
            int lastPathSeparator = _loadedFile.LastIndexOf("\\", StringComparison.Ordinal);
            if (lastPathSeparator < 0)
                lastPathSeparator = _loadedFile.LastIndexOf("/", StringComparison.Ordinal);
            string rootDir = _loadedFile.Substring(0, lastPathSeparator + 1);
            string path = rootDir + name + ".wren";
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            if (Directory.Exists(path.Substring(0, path.Length - 5)))
            {
                path = path.Substring(0, path.Length - 5) + "\\" + "module.wren";
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            return null;
        }
    }
}
