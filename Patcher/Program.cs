using System;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Config
{
    public string OutputAssemblyName { get; set; } = "osu!patched.exe";
    public string DirectoryPath { get; set; } = "./";
    public string InputDomain { get; set; } = "ppy.sh";
    public string OutputDomain { get; set; } = "titanic.sh";
    public string BanchoIP { get; set; } = "176.57.150.202";
    public bool Deobfuscate { get; set; } = false;
}

class Program
{
    static void PrintHelp()
    {
        Console.WriteLine("Usage: osu_patcher [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --output <file>          Set output assembly name (default: osu!patched.exe)");
        Console.WriteLine("  --dir <directory>        Set the directory path (default: ./)");
        Console.WriteLine("  --input-domain <domain>  Set input domain to replace (default: ppy.sh)");
        Console.WriteLine("  --output-domain <domain> Set output domain to replace with (default: titanic.sh)");
        Console.WriteLine("  --bancho-ip <ip>         Set Bancho IP (default: 176.57.150.202)");
        Console.WriteLine("  --deobfuscate            Automatically deobfuscate the binary with de4dot");
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
                    if (i + 1 < args.Length) config.BanchoIP = args[++i];
                    break;
                case "--deobfuscate":
                    config.Deobfuscate = true;
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
                }
            }
        }

        Console.WriteLine("Done.");
    }

    static bool ContainsBanchoIP(AssemblyDefinition assembly, string inputDomain)
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
                    if (stringValue.Contains($"c.{inputDomain}"))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    static long IpToDecimal(string ipAddress)
    {
        IPAddress ip = IPAddress.Parse(ipAddress);
        byte[] bytes = ip.GetAddressBytes();
        return BitConverter.ToUInt32(bytes, 0);
    }

    static void PatchBanchoIP(AssemblyDefinition assembly, string ip)
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

    static string DeobfuscateOsuExecutable(string executablePath)
    {
        var newExecutable = "deobfuscated.exe";
        Console.WriteLine("Deobfuscation is currently not implemented!");
        File.Copy(executablePath, newExecutable);
        // TODO: Deobfuscate the binary & return new path
        return newExecutable;
    }

    static string FindOsuExecutable(string directory)
    {
        string[] validFiles = { "osu!.exe", "osu.exe", "osu!test.exe", "osu!shine1.exe" };
        return validFiles.Select(file => Path.Combine(directory, file)).FirstOrDefault(File.Exists);
    }

    static void Main(string[] args)
    {
        Config config = ParseArguments(args);
        bool removeExecutable = false;

        if (!Directory.Exists(config.DirectoryPath))
        {
            Console.WriteLine("Directory not found!");
            Environment.Exit(1);
        }
        Directory.SetCurrentDirectory(config.DirectoryPath);

        string osuExe = FindOsuExecutable(".");
        if (osuExe == null)
        {
            Console.WriteLine("osu! executable not found!");
            Environment.Exit(1);
        }

        if (config.Deobfuscate)
        {
            osuExe = DeobfuscateOsuExecutable(osuExe);
            removeExecutable = true;
        }

        Console.WriteLine("Loading assembly...");
        AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(osuExe);

        if (ContainsBanchoIP(assembly, config.InputDomain))
        {
            // We have a tcp client -> try to patch bancho ip
            PatchBanchoIP(assembly, config.BanchoIP);
        }

        // Replace all domains
        PatchDomains(assembly, config.InputDomain, config.OutputDomain);

        Console.WriteLine("Writing new assembly...");
        assembly.Write(config.OutputAssemblyName);
        assembly.Dispose();

        if (removeExecutable)
        {
            Console.WriteLine("Removing deobfuscated executable...");
            File.Delete(osuExe);
        }
        
        Console.WriteLine("Done.");
    }
}
