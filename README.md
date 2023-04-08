# 🍬 gum

![Build Status](https://github.com/isadorasophia/gum/actions/workflows/ci.yaml/badge.svg)
[![LICENSE](https://img.shields.io/github/license/isadorasophia/gum.svg)](LICENSE)

gum is a tool that converts narrative scripts into a graph that can be read by C# metadata.

## Syntax

```
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
