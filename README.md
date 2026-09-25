# 🍬 gum

[![Release](https://github.com/isadorasophia/gum/actions/workflows/ci.yaml/badge.svg)](https://github.com/isadorasophia/gum/actions/workflows/ci.yaml)
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
@[0-9] which [0-9] is the amount of times this block may be executed
[] for actions
c: when referencing components
i: when referencing icons
{variable_name} for referencing variables
// comments
>> title for options section
> option
%[0-9]+ chance of executing lines
```

## Usage

```shell
$ gum.exe <scripts_path> <out_path>
```

- `<scripts_path>`
  - Path of a directory or a single file to all *.gum files.
- `<out_path>` 
  - Output *.json with C# metadata to be consumed by a third party.

### Contributing
Feel free to contact me or [saint11](http://saint11.org/) if you have any suggestions. I am very interested in people trying it out and any feedback you may have!

Pull requests, bug reports and other contributions made with LLM or any generative AI technology will NOT be accepted. I am not in the slight interested in reviewing any non-human code.

#### Example
```shell
$ gum.exe ../game/resources/dialogs ../game/src/project/packed/dialogs
```
