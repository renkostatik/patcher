using System;
using System.Diagnostics;
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

                                if ((int)value < 16700000)
                            	    continue;

                                Console.WriteLine($"{method.Body.Instructions[i - j]} -> {ipDecimal} ({ip})");
                                method.Body.Instructions[i - j] = Instruction.Create(OpCodes.Ldc_I4, (int)ipDecimal);
                            }

                            if (code == OpCodes.Ldc_I8)
                            {
                                if ((long)value < 16700000)
                            	    continue;

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

    static int Deobfuscate(string assemblyPath, string newAssemblyPath, string de4dotPath)
    {
        Console.WriteLine("Deobfuscating...");

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"{de4dotPath} {assemblyPath} -o {newAssemblyPath}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Start the de4dot
            process.Start();

            // Read the output and error stream
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            // Wait for both streams to be fully read
            Task.WaitAll(outputTask, errorTask);

            // make sure that the output is captured
            string output = outputTask.Result;
            string error = errorTask.Result;

            Console.WriteLine($"{output}");
            if (error != "")
                Console.WriteLine($"de4dot Error:\n{error}");


            // Wait for the de4dot to deobfuscate
            process.WaitForExit();

            return process.ExitCode;
        }
    }

    public static int DisplayFailedError()
    {
        Console.WriteLine("Deobfuscation Failed, Do you wanna continue Patching or exit Patcher Y/N");
        ConsoleKeyInfo key = Console.ReadKey();
        if (key.Key == ConsoleKey.Y)
        {
            return 1;
        }
        else if (key.Key == ConsoleKey.N)
        {
            Console.WriteLine("Ok. Exitting...");
            return 0;
        }
        else
        {
            return DisplayFailedError();
        }
    }
    static void Main()
    {
        // TODO move it to some sort of config instead of arguments or support both the args and the config
        string assemblyPath = "./assemblies/osu!.exe";
        string outputPath = "./osu!Patched.exe";
        string domain = "lekuru.xyz";
        string ip = "176.57.150.202";
        string de4dotPath = "C:\\de4dot\\netcoreapp2.1\\de4dot.dll";


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
            System.Environment.Exit(1);
        }
        
        if (!string.IsNullOrEmpty(directoryPath))
            Directory.SetCurrentDirectory(directoryPath);

        
        string TempDirectory = Path.GetTempFileName();
        //Console.WriteLine(TempDirectory);
        var result = Deobfuscate(assemblyPath, TempDirectory, de4dotPath);
        fileName = TempDirectory;
        if (result == 0)
        {
            Console.WriteLine("Deobfuscation was succesful");
            // do nothing(for now)
        } else
        {

            DisplayFailedError();
        }

        Console.WriteLine(fileName);
        if (!File.Exists(fileName))
        {
            Console.WriteLine("File could not be found!");
            System.Environment.Exit(1);
        }

        Console.WriteLine("Loading assembly...");
        AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(fileName);

        PatchDomains(assembly, domain);
        PatchBanchoIP(assembly, ip);

        Console.WriteLine("Writing new assembly...");
        assembly.Write("osu!.exe");
        
        Console.WriteLine("Done.");
    }
}