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
