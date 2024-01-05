using System.Net;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static void PatchDomains(AssemblyDefinition assembly, string domain)
    {
        Console.WriteLine("Patching domains...");

        // Select all methods with bodies in the assembly
        IEnumerable<MethodDefinition> methods = assembly.Modules.SelectMany(
            module => module.Types.SelectMany(
                type => type.Methods.Where(method => method.HasBody)
            )
        );

        foreach (var method in methods)
        {
            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];

                if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string stringValue)
                {
                    if (stringValue.Contains("ppy.sh"))
                    {
                        // Replace the old domain with the new domain
                        string newUrl = stringValue.Replace("ppy.sh", domain);

                        // Update the instruction with the new string value
                        method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, newUrl);

                        Console.WriteLine(newUrl);
                    }
                }
            }
        }

        Console.WriteLine("Done.");
    }

    static void PatchBanchoIP(AssemblyDefinition assembly, string ip)
    {
        Console.WriteLine("Patching bancho ip...");

        // Select all methods with bodies in the assembly
        IEnumerable<MethodDefinition> methods = assembly.Modules.SelectMany(
            module => module.Types.SelectMany(
                type => type.Methods.Where(method => method.HasBody)
            )
        );

        foreach (var method in methods)
        {
            bool found = false;

            // Check if the method contains the string "Connecting to Bancho"
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string stringValue)
                {
                    if (stringValue.Contains("Connecting to Bancho"))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
                continue;

            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];

                if ((instruction.OpCode == OpCodes.Newobj || instruction.OpCode == OpCodes.Call) &&
                    instruction.Operand is MethodReference methodRef &&
                    methodRef.DeclaringType.FullName == "System.Net.IPEndPoint")
                {
                    // Check if this is a constructor or method creating an IPEndPoint
                    if (methodRef.Name == ".ctor" || methodRef.Name == "Create")
                    {
                        long ipDecimal = IPAddress.Parse(ip).Address;

                        // Skip these ldc.i4 values
                        List<int> skip = new List<int>()
                        {
                            13380,
                            13381,
                            13382,
                            13383
                        };

                        // Iterate through the sorrounding instructions to find the ldc.i4 matches
                        // Replace the matches with the new ip address
                        for (int j = 0; j < 12; j++)
                        {
                            object value = method.Body.Instructions[i - j].Operand;
                            OpCode code = method.Body.Instructions[i - j].OpCode;

                            if (code == OpCodes.Ldc_I4)
                            {
                                if (skip.Contains((int)value))
                                    continue;

                                Console.WriteLine($"{method.Body.Instructions[i - j]} -> {ipDecimal} ({ip})");
                                method.Body.Instructions[i - j] = Instruction.Create(OpCodes.Ldc_I4, (int)ipDecimal);
                            }

                            if (code == OpCodes.Ldc_I8)
                            {
                                Console.WriteLine($"{method.Body.Instructions[i - j]} -> {ipDecimal} ({ip})");
                                method.Body.Instructions[i - j] = Instruction.Create(OpCodes.Ldc_I8, ipDecimal);
                            }
                        }

                        Console.WriteLine("Done.");
                    }
                }
            }
        }
    }

    static void Main()
    {
        
        string assemblyPath = "./assemblies/osu!.exe";
        string outputPath = "./osu!Patched.exe";
        string domain = "lekuru.xyz";
        string ip = "176.57.150.202";

        foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
        {
            var split = arg.Split('=');
            if (split.Length != 2) {
                Console.WriteLine($"Invalid argument: {arg}");
                Console.WriteLine("Usage: --ip=<ip> --domain=<domain> --assembly=<path> --output=<path>");
                System.Environment.Exit(1);
            }
            switch (split[0]) {
                case "--ip":
                    ip = split[1];
                    break;
                case "-d":
                case "--domain":
                    domain = split[1];
                    break;
                case "-a":
                case "--assembly":
                    assemblyPath = split[1];
                    break;
                case "-o":
                case "--output":
                    outputPath = split[1];
                    break;
                default:
                    Console.WriteLine($"Invalid argument: {arg}");
                    Console.WriteLine("Usage: --ip=<ip> --domain=<domain> --assembly=<path> --output=<path>");
                    System.Environment.Exit(1);
                    break;
            }
        }

        string outputAssemblyPath = Path.GetFullPath(outputPath);
        string fileName = Path.GetFileName(assemblyPath);

        string? directoryPath = Path.GetDirectoryName(assemblyPath);

        if (!Directory.Exists(directoryPath)) {
            Console.WriteLine("Directory could not be found!");
            return;
        }

        if (!string.IsNullOrEmpty(directoryPath))
            Directory.SetCurrentDirectory(directoryPath);

        // TODO: de4dot deobfuscating

        if (!File.Exists(fileName))
        {
            Console.WriteLine("File could not be found!");
            return;
        }

        Console.WriteLine("Loading assembly...");
        AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(fileName);

        PatchBanchoIP(assembly, ip);
        PatchDomains(assembly, domain);

        Console.WriteLine("Writing new assembly...");
        assembly.Write(outputAssemblyPath);

        Console.WriteLine("Done.");
    }
}