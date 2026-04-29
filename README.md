# UmbraSharp

a work-in-progress Lua interpreter written in C#, intended as an alternative to MoonSharp

## goals

UmbraSharp will be backwards-compatible with MoonSharp syntax, _and_ be able to compile Luau syntax (analysis will be left to luau-analyze, that is not the job of the compiler/interpreter)

it will _not_ be a drop in replacement for MoonSharp

## why

this project learns from the pain points of MoonSharp

- performance (UmbraSharp uses structs over classes, a register machine over a stack machine, and avoids allocations like the plague)
- documentation (upon completion, UmbraSharp will have detailed documentation, including on its bytecode)
- bytecode focus (UmbraSharp focuses on both source _and_ bytecode stability)
- ownership (MoonSharp tracks and constantly checks the ownership of table/function/coroutine values. UmbraSharp is capable of handling values from other scripts, even within the same VM)

## state of the project

todo: fill all sections

### interpreter

- [ ] data types (excl. coroutines)
- [ ] instruction set
- [ ] coroutines

### compiler

- [ ] parsing
- [ ] generation of correct bytecode
- [ ] optimizing (using more of the instruction set and being smarter about its behavior)

### stdlib

- [ ] lua parity
- [ ] figure out debug library
- [ ] moonsharp compat
- [ ] umbrasharp specific libraries
- [ ] yielding

### command-line interface

- [ ] rudimentary CLI

### debugger

- [ ] comprehensive debugger protocol
- [ ] implementation within the interpreter
- [ ] client
- [ ] debugger protocol documentation

### testing

- [ ] interpreter tests
- [ ] stdlib tests
- [ ] compiler tests

### documentation

- [ ] API docs
- [ ] bytecode format
- [ ] all internal documentation

### misc

- [ ] AOT support
