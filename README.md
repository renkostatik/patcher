# Titanic! Patcher

A server url patcher for osu! clients, made for use in Titanic. It includes automatic replacement of all urls, as well as patching Bancho IPs on older clients. It can also automatically deobfuscate binaries using de4dot, if specified.

## Installation & Usage

Download the latest release for your operating system [here](https://github.com/osuTitanic/titanic-patcher/releases/).  
Check the `--help` flag for usage information, like this:

```shell
osu-patcher --help
```

```shell
Usage: osu_patcher [options]
Options:
  --output <file>          Set output assembly name (default: osu!patched.exe)
  --dir <directory>        Set the directory path (default: ./)
  --input-domain <domain>  Set input domain to replace (default: ppy.sh)
  --output-domain <domain> Set output domain to replace with (default: titanic.sh)
  --bancho-ip <ip>         Set Bancho IP (default: 176.57.150.202)
  --deobfuscate            Automatically deobfuscate the binary with de4dot
  --help                   Show this help message and exit
```

## Example

Here is a simple example of how it could look:

https://github.com/user-attachments/assets/08c56048-2c89-4fc0-9d9d-a9106836a0d3

## Building

To build the project yourself, be sure to have [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed, .NET 6.0 or higher will work as well, when changing the project version.

Clone the project and go into the directory:

```shell
git clone https://github.com/osuTitanic/titanic-patcher.git && cd titanic-patcher
```

Build or publish the binary:

```shell
dotnet build --configuration Release
cd ./Patcher/bin/Release/<your-dotnet-version>/<your-target>/
./osu-patcher
```

or publish it, to produce a single binary:

```shell
dotnet publish Patcher
cd ./Patcher/bin/Release/<your-dotnet-version>/<your-target>/publish/
./osu-patcher
```

## .NET Framework Clients

You may run into issues when deobfuscating osu! clients that use .NET Framework 4.  
In that case, the solution would be to manually [download](https://github.com/vee2xx/de4dot-built-binaries) or [build](https://github.com/de4dot/de4dot) de4dot, targeting the .NET Framework 4.5 version.

Due to the annoying obfuscation techniques used by eazfuscator, we have to make some adjustments to the de4dot command, like this:

```shell
de4dot --preserve-tokens --dont-rename --keep-types --keep-names ntpefmagd --preserve-table all,-pd "osu!.exe"
```

This ensures that only the strings will be deobfuscated and the rest will stay the same.  
Now you are able to use the patcher again, to do the rest:

```shell
osu-patcher --output "./osu-fixed.exe"
```
