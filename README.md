# 🍬 gum

![Build Status](https://github.com/isadorasophia/gum/actions/workflows/ci.yaml/badge.svg)
[![LICENSE](https://img.shields.io/github/license/isadorasophia/gum.svg)](LICENSE)

gum is a tool that converts *.gum scripts into a narrative dialog dialog.

## Syntax

```json
= new situation
- executed once
+ executed multiple times
() for conditional statements
(...) for else statements, will be part of the same block as above
-> goto
-> exit! stops
@random picks randomly - and + blocks
@order order options by rule matching and then order (default)
@[0-9] which [0-9] is the amount of times this block may be executed
[] for actions
c: when referencing components
i: when referencing icons
{} for referencing variables
// comments
>> title for options section
> option
```

## Usage

```shell
$ gum.exe <scripts_path> <out_path>
```

- `<scripts_path>`
  - Path of a directory or a single file to all *.gum files.
- `<out_path>` 
  - Output *.json with C# metadata to be consumed by a third party.
  
#### Example
```shell
$ gum.exe ../game/resources/dialogs ../game/src/project/packed/dialogs
```

## Installing
### Building from source
_From terminal_
1. Open a terminal in the root directory
2. `dotnet restore`
3. `dotnet build`

_From Visual Studio_
1. Open `gum.sln` with Visual Studio 2022 17.4 or higher version (required for .NET 7)
2. Build!

### Pre-compiled binaries
You can download the binaries at our [releases](https://github.com/isadorasophia/gum/releases/) page. Or with the command line:

**ps1**
```shell
mkdir bin
Invoke-WebRequest https://github.com/isadorasophia/gum/releases/download/v0.1/gum-v0.1-win-x64.zip -OutFile bin/parser.zip
Expand-Archive bin/gum.zip -DestinationPath bin
Remove-Item bin/gum.zip
```

**sh**
```bash
mkdir bin
curl -sSL https://github.com/isadorasophia/gum/releases/download/v0.1/gum-v0.1-linux-x64.tar.gz | tar -xz --directory=bin
```
