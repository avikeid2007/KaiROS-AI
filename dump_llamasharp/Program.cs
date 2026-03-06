using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        try {
            var asm = Assembly.LoadFrom(@"C:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\bin\Debug\net9.0-windows10.0.19041.0\LLamaSharp.dll");
            var t = asm.GetType("LLama.InteractiveExecutor");
            if (t == null) {
                Console.WriteLine("Could not find InteractiveExecutor");
                return;
            }
            Console.WriteLine("--- Properties ---");
            foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)) {
                Console.WriteLine(" - " + p.PropertyType.Name + " " + p.Name + " (CanRead: " + p.CanRead + ", CanWrite: " + p.CanWrite + ")");
            }
            Console.WriteLine("\n--- Methods ---");
            foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)) {
                if (m.DeclaringType.Name == "Object") continue;
                Console.WriteLine(" - " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            }

            Console.WriteLine("\n--- Image Handlers? ---");
            var imgProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(c => c.Name.Contains("Image") || c.Name.Contains("Media") || c.Name.Contains("Embed")).ToList();
            foreach(var p in imgProps) Console.WriteLine(p.Name + " of type " + p.PropertyType.Name);

            // Let's also check ILLamaExecutor interface
            Console.WriteLine("\n--- ILLamaExecutor Interfaces ---");
            foreach(var i in t.GetInterfaces()) {
                Console.WriteLine(i.Name);
            }
        } catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }
}
