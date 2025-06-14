using System.Net;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Runtime.InteropServices;

namespace Patcher
{
    class Config
    {
        public string OutputAssemblyName { get; set; } = "osu!patched.exe";
        public string DirectoryPath { get; set; } = "./";
        public string InputDomain { get; set; } = "ppy.sh";
        public string OutputDomain { get; set; } = "titanic.sh";
        public string BanchoIp { get; set; } = "176.57.150.202";
        public string? MscorlibPath { get; set; } = null;
        public bool Deobfuscate { get; set; } = false;
        public bool FixNetLib { get; set; } = false;
    }

    class Program
    {
        static void PrintHelp()
        {
            Console.WriteLine("Usage: osu-patcher [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --output <file>          Set output assembly name (default: osu!patched.exe)");
            Console.WriteLine("  --dir <directory>        Set the directory path (default: ./)");
            Console.WriteLine("  --input-domain <domain>  Set input domain to replace (default: ppy.sh)");
            Console.WriteLine("  --output-domain <domain> Set output domain to replace with (default: titanic.sh)");
            Console.WriteLine("  --bancho-ip <ip>         Set Bancho IP (default: 176.57.150.202)");
            Console.WriteLine("  --deobfuscate            Automatically deobfuscate the binary with de4dot");
            Console.WriteLine("  --fix-netlib             Fix issues with netlib data encoding");
            Console.WriteLine("  --mscorlib-path          Specify your path to mscorlib.dll");
            Console.WriteLine("  --help                   Show this help message and exit");
            Environment.Exit(0);
        }

        static Config ParseArguments(string[] args)
        {
            var config = new Config();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                        PrintHelp();
                        break;
                    case "--output":
                        if (i + 1 < args.Length) config.OutputAssemblyName = args[++i];
                        break;
                    case "--dir":
                        if (i + 1 < args.Length) config.DirectoryPath = args[++i];
                        break;
                    case "--input-domain":
                        if (i + 1 < args.Length) config.InputDomain = args[++i];
                        break;
                    case "--output-domain":
                        if (i + 1 < args.Length) config.OutputDomain = args[++i];
                        break;
                    case "--bancho-ip":
                        if (i + 1 < args.Length) config.BanchoIp = args[++i];
                        break;
                    case "--mscorlib-path":
                        if (i + 1 < args.Length) config.MscorlibPath = args[++i];
                        break;
                    case "--deobfuscate":
                        config.Deobfuscate = true;
                        break;
                    case "--fix-netlib":
                        config.FixNetLib = true;
                        break;
                    default:
                        Console.WriteLine("Unknown argument: " + args[i]);
                        break;
                }
            }
            return config;
        }

        static void PatchDomains(AssemblyDefinition assembly, string inputDomain, string outputDomain)
        {
            Console.WriteLine("Patching domains...");

            // Select all methods with bodies in the assembly
            var methods = assembly.Modules.SelectMany(
                module => module.Types.SelectMany(
                    type => type.Methods.Where(method => method.HasBody)
                )
            );

            foreach (var method in methods)
            {
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var instruction = method.Body.Instructions[i];

                    if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string stringValue)
                    {
                        if (stringValue.Contains(inputDomain))
                        {
                            // Update the instruction with the new string value
                            string patchedUrl = stringValue.Replace(inputDomain, outputDomain);
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, patchedUrl);
                            Console.WriteLine(patchedUrl);
                        }
                        else if (stringValue.StartsWith("http://peppy.chigau.com/upload.php"))
                        {
                            string patchedUrl = stringValue.Replace("peppy.chigau.com/upload.php", $"osu.{outputDomain}/web/osu-bmsubmit-upload.php");
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, patchedUrl);
                            Console.WriteLine(patchedUrl);
                        }
                        else if (stringValue.StartsWith("http://peppy.chigau.com/novideo.php"))
                        {
                            string patchedUrl = stringValue.Replace("peppy.chigau.com/novideo.php", $"osu.{outputDomain}/web/osu-bmsubmit-novideo.php");
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, patchedUrl);
                            Console.WriteLine(patchedUrl);
                        }
                        else if (stringValue.StartsWith("http://peppy.chigau.com/bss"))
                        {
                            string patchedUrl = stringValue.Replace("peppy.chigau.com/bss", $"osu.{outputDomain}/d");
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, patchedUrl);
                            Console.WriteLine(patchedUrl);
                        }
                        else if (stringValue.StartsWith("http://peppy.chigau.com"))
                        {
                            string patchedUrl = stringValue.Replace("peppy.chigau.com", $"osu.{outputDomain}");
                            method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, patchedUrl);
                            Console.WriteLine(patchedUrl);
                        }
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        static bool ContainsString(AssemblyDefinition assembly, string input)
        {
            // Select all methods with bodies in the assembly
            var methods = assembly.Modules.SelectMany(
                module => module.Types.SelectMany(
                    type => type.Methods.Where(method => method.HasBody)
                )
            );

            // If the assembly contains the http bancho url, we
            // know that it does not contain a bancho ip.
            foreach (var method in methods)
            {
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var instruction = method.Body.Instructions[i];

                    if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string stringValue)
                    {
                        if (stringValue.Contains(input))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static long IpToDecimal(string ipAddress)
        {
            IPAddress ip = IPAddress.Parse(ipAddress);
            byte[] bytes = ip.GetAddressBytes();
            return BitConverter.ToUInt32(bytes, 0);
        }

        static void PatchBanchoIp(AssemblyDefinition assembly, string ip)
        {
            Console.WriteLine("Patching Bancho IP...");

            // Select all methods with bodies in the assembly
            var methods = assembly.Modules.SelectMany(
                module => module.Types.SelectMany(
                    type => type.Methods.Where(method => method.HasBody)
                )
            );

            List<string> ipList = new List<string>
            {
                "50.23.74.93", "219.117.212.118", "192.168.1.106", "174.34.145.226", "216.6.228.50",
                "50.228.6.216", "69.147.233.10", "167.83.161.203", "10.233.147.69", "1.0.0.127",
                "53.228.6.216", "52.228.6.216", "51.228.6.216", "50.228.6.216", "151.0.0.10"
            };

            List<long> ipListDecimal = ipList.Select(ipStr => IpToDecimal(ipStr)).ToList();
            long newIpDecimal = IpToDecimal(ip);

            foreach (var method in methods)
            {
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    Instruction instruction = method.Body.Instructions[i];
                    Instruction? followingInstruction = null;

                    if (i + 1 < method.Body.Instructions.Count)
                    {
                        followingInstruction = method.Body.Instructions[i + 1];
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4 && ipListDecimal.Contains((int)instruction.Operand))
                    {
                        method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldc_I8, newIpDecimal);
                        Console.WriteLine($"Replaced IP: {instruction.Operand} -> {newIpDecimal}");

                        if (followingInstruction != null && followingInstruction.OpCode == OpCodes.Conv_I8)
                        {
                            // Conv_I8 instructions will make it crash, so let's remove them
                            method.Body.Instructions[i - 1] = Instruction.Create(OpCodes.Nop);
                        }
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I8 && ipListDecimal.Contains((long)instruction.Operand))
                    {
                        method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldc_I8, newIpDecimal);
                        Console.WriteLine($"Replaced IP: {instruction.Operand} -> {newIpDecimal}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldstr && ipList.Contains((string)instruction.Operand))
                    {
                        method.Body.Instructions[i] = Instruction.Create(OpCodes.Ldstr, ip);
                        Console.WriteLine($"Replaced IP: {(string)instruction.Operand} -> {ip}");
                        continue;
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        static void FixNetLibEncoding(AssemblyDefinition assembly, BaseAssemblyResolver resolver)
        {
            Console.WriteLine("Fixing NetLib encoding...");

            // Resolve `UTF8Encoding` type from mscorlib.dll
            // This requires the resolver to be set up correctly, such that it can find a valid mscorlib.dll
            var corlib = resolver.Resolve(assembly.MainModule.AssemblyReferences.First(r => r.Name == "mscorlib"));
            var utf8Type = corlib.MainModule.GetType("System.Text.UTF8Encoding");
            var utf8TypeRef = assembly.MainModule.ImportReference(utf8Type);
            var utf8CtorRef = new MethodReference(
                ".ctor",
                assembly.MainModule.TypeSystem.Void,
                utf8TypeRef
            ) {
                HasThis = true,
                Parameters = {
                    new ParameterDefinition(
                        "encoderShouldEmitUTF8Identifier",
                        ParameterAttributes.None,
                        assembly.MainModule.TypeSystem.Boolean
                    )
                }
            };

            // Find all methods that use StreamWriter
            var validMethods = assembly.MainModule.Types
                .SelectMany(t => t.NestedTypes.Concat(new[] { t }))
                .SelectMany(t => t.Methods.Where(m => m.HasBody && m.Parameters.Count == 2))
                .Where(m => m.Body.Instructions.Any(instr =>
                    (instr.OpCode == OpCodes.Newobj || instr.OpCode == OpCodes.Call) &&
                    instr.Operand is MethodReference mr &&
                    mr.DeclaringType.Name == "StreamWriter"));

            foreach (var method in validMethods)
            {
                var il = method.Body.GetILProcessor();
                
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call &&
                        instruction.Operand is MethodReference methodRef &&
                        methodRef.Name == "get_Default" &&
                        methodRef.DeclaringType.FullName == "System.Text.Encoding")
                    {
                        var falseArgument = Instruction.Create(OpCodes.Ldc_I4_0);
                        var utf8Encoding = Instruction.Create(OpCodes.Newobj, utf8CtorRef);
                        
                        // Replace the existing Encoding.Default call with "new UTF8Encoding(false)"
                        il.InsertBefore(instruction, falseArgument);
                        il.InsertAfter(instruction, utf8Encoding);
                        il.Remove(instruction);
                        
                        Console.WriteLine($"Replaced {instruction.Operand} with UTF8 Encoding in {method.FullName}");
                        return;
                    }
                }
            }
            
            Console.WriteLine("Failed to patch netlib encoding.");
        }

        static string DeobfuscateOsuExecutable(string executablePath)
        {
            Console.WriteLine("Deobfuscating...");
            var outputExecutable = Path.GetFileNameWithoutExtension(executablePath) + ".deobfuscated.exe";
            var status = Deobfuscator.Deobfuscate(new string[] {
                executablePath,
                "-o", outputExecutable,
                // Only deobfuscate strings, and leave
                // everything else untouched
                "--preserve-tokens",
                "--dont-rename",
                "--keep-types",
                "--keep-names", "ntpefmagd",
                "--preserve-table", "all,-pd"
            });

            if (status != 0)
            {
                Console.WriteLine("Deobfuscation failed!");
                Environment.Exit(status);
            }

            return outputExecutable;
        }

        static string? FindOsuCommonDll(string directory)
        {
            string commonDllPath = Path.Combine(directory, "osu!common.dll");
            if (File.Exists(commonDllPath)) return commonDllPath;
            return null;
        }

        static string? FindOsuExecutable(string directory)
        {
            string[] validFiles = { "osu!.exe", "osu.exe", "osu!test.exe", "osu!public.exe", "osu!shine1.exe", "osu!cuttingedge.exe" };
            return validFiles.Select(file => Path.Combine(directory, file)).FirstOrDefault(File.Exists);
        }

        static BaseAssemblyResolver CreateResolver(Config config)
        {
            BaseAssemblyResolver resolver = new DefaultAssemblyResolver();

            if (config.MscorlibPath != null)
            {
                resolver.AddSearchDirectory(config.MscorlibPath);
                resolver.AddSearchDirectory(config.DirectoryPath);
                return resolver;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var validMscorlibDirectories = new string[]
                {
                    @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\",
                    @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\",
                    @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\",
                    @"C:\Windows\Microsoft.NET\Framework64\v2.0.50727\"
                };

                foreach (var dir in validMscorlibDirectories)
                {
                    if (Directory.Exists(dir))
                    {
                        resolver.AddSearchDirectory(dir);
                        break;
                    }
                }

                resolver.AddSearchDirectory(RuntimeEnvironment.GetRuntimeDirectory());
                resolver.AddSearchDirectory(config.DirectoryPath);
                return resolver;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!Directory.Exists("/usr/lib/mono/"))
                {
                    resolver.AddSearchDirectory(config.DirectoryPath);
                    return resolver;
                }

                var validMonoInstallationDirectories = new string[]
                {
                    "/usr/lib/mono/4.8/",
                    "/usr/lib/mono/4.7.2/",
                    "/usr/lib/mono/4.7.1/",
                    "/usr/lib/mono/4.7/",
                    "/usr/lib/mono/4.6.2/",
                    "/usr/lib/mono/4.6.1/",
                    "/usr/lib/mono/4.6/",
                    "/usr/lib/mono/4.5.2/",
                    "/usr/lib/mono/4.5.1/",
                    "/usr/lib/mono/4.5/",
                    "/usr/lib/mono/4.0/",
                    "/usr/lib/mono/3.5/",
                    "/usr/lib/mono/3.0/",
                    "/usr/lib/mono/2.0/",
                };

                foreach (var dir in validMonoInstallationDirectories)
                {
                    if (Directory.Exists(dir))
                    {
                        resolver.AddSearchDirectory(dir);
                        break;
                    }
                }

                resolver.AddSearchDirectory(config.DirectoryPath);
                return resolver;
            }
            
            resolver.AddSearchDirectory(config.DirectoryPath);
            return resolver;
        }

        static void Main(string[] args)
        {
            Config config = ParseArguments(args);

            if (!Directory.Exists(config.DirectoryPath))
            {
                Console.WriteLine("Directory not found!");
                Environment.Exit(1);
            }
            Directory.SetCurrentDirectory(config.DirectoryPath);

            string? osuExe = FindOsuExecutable(".");
            if (osuExe == null)
            {
                Console.WriteLine("osu! executable not found!");
                Environment.Exit(1);
            }

            if (config.Deobfuscate)
            {
                osuExe = DeobfuscateOsuExecutable(osuExe);
            }
            
            Console.WriteLine("Loading assembly...");
            BaseAssemblyResolver resolver = CreateResolver(config);
            ReaderParameters readerParams = new ReaderParameters { AssemblyResolver = resolver };
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(osuExe, readerParams);

            var isHttpBancho = ContainsString(assembly, $"c.{config.InputDomain}");
            var isIrcClient = ContainsString(assembly, $"irc.{config.InputDomain}");

            if (!isHttpBancho && !isIrcClient)
            {
                // We have a tcp client -> try to patch bancho ip
                PatchBanchoIp(assembly, config.BanchoIp);
            }

            if (config.FixNetLib)
            {
                // Check if osu!common.dll is available
                // If not, use osu!.exe assembly
                string? commonDll = FindOsuCommonDll(".");
                
                if (commonDll != null)
                {
                    AssemblyDefinition commonAssembly = AssemblyDefinition.ReadAssembly(commonDll);
                    FixNetLibEncoding(commonAssembly, resolver);
                    
                    Console.WriteLine("Writing osu!common assembly...");
                    commonAssembly.Write("osu!common.patched.dll");
                    commonAssembly.Dispose();
                }
                else
                {
                    // osu!common was merged into main osu!.exe
                    FixNetLibEncoding(assembly, resolver);
                }
            }
            
            // Replace all domains
            PatchDomains(assembly, config.InputDomain, config.OutputDomain);

            Console.WriteLine("Writing new assembly...");
            assembly.Write(config.OutputAssemblyName);
            assembly.Dispose();

            if (config.Deobfuscate)
            {
                Console.WriteLine("Removing deobfuscated executable...");
                File.Delete(osuExe);
            }

            Console.WriteLine("Done.");
        }
    }
}