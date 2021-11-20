Yolol.IL is a compiler from the [Yolol language](https://wiki.starbasegame.com/index.php/YOLOL) to dotnet IL. The compiled code executes very fast - millions of lines/second.

# Basic Usage

Yolol.IL is distributed as a nuget package. Once you have it installed in your project using it is quite simple:

```csharp
// First parse the source code into an AST
var result = Parser.ParseProgram("the_code");
if (!result.IsOk)
    throw new Exception($"Cannot parse program: {result.Err}");

// Compile the AST
var externalsMap = new ExternalsMap();
var compiled = ast.Compile(externalsMap);

// set up memory to use at runtime
var externals = new Value[externalsMap.Count];
Array.Fill(externals, Number.Zero);
var internals = new Value[compiled.InternalsMap.Count];
Array.Fill(internals, Number.Zero);

// Run the code
while (true)
    compiled.Tick(internals, externals)
```

Each call to `Tick` will execute one line of Yolol.

# Advanced Usage

## Parameters

The `Compile` method has a number of optional parameters which allow you to tweak some of the rules of the language. The default values are the correct behaviour for compatibility with the Starbase in-game implementation.

 - `maxLines`: Allows you to set the maximum number of lines in a program. Programs will be padded up to this number.
 - `maxStringLength`: The maximum length of strings. Any strings longer than this will be trimmed to this length.
 - `staticTypes`: A set of type hints for variables, the compiled code may be faster if static types are provided. Use with caution - providing incorrect type hints will cause undefined behaviour.
 - `changeDetection`: Enable "change detection", this allows you to inspect the `ChangeSet` property on the `compiled` program to determine which variables may have changed (false positives are possible, false negatives are not).

## Execution Methods

There are two ways to run the `compiled` program.

The `Tick` method runs one single line, this gives you an opportunity to inspect the execution state between each line and to modify it if necessary (e.g. for simulating devices).

The `Run` method runs for a maximum number of ticks or until any one of a set of variables changes. This is faster for bulk execution of lines where you simply want to break out when some indicator variable is set (e.g. `done=1`).

# Architecture

Yolol.IL is a two pass compiler with no complex pre-compilation code analysis, this keeps the project relatively simple and maintainable.

## Pass #1: Yolol -> IL

The first pass consumes Yolol and produces IL, it is split across several files.

`Yolol.IL\Extensions\CompileExtensions.cs` provides a set of `Compile` extension methods on Yolol ASTs and individual Yolol lines. All of these extensions ultimately call one single extension which sets up the method body and then calls the `ConvertLineVisitor`. Part of the setup involves creating a try/catch block for Yolol runtime exceptions. The compiler should emit code which never causes a runtime exception (they're very slow), but this exists just in case one is missed!

`Yolol.IL\Compiler\ConvertLineVisitor.cs` walks over the AST and emits IL node by node. There is a `Visit` method for every single statement/expression type in the language which is responsible for emitting code for that item.

`Yolol.IL\Compiler\TypeStack.cs` provides a helper for keeping track of types on the stack. IL is a stack based language, so many of the `Visit` methods leave a value onto the stack, the type stack keeps track of what type it was. Most `Visit` methods which consume values off the stack will inspect this and specialise based on the types it is about to operate on.

`Yolol.IL\Extensions\ExpressionExtensions.cs` converts simple C# expression trees into IL. There are two separate compilation paths here - the "fast path" which consumes values off the stack in the order they are provided and supports a fairly limited set of operations and the "slow path" which supports a wider range of operations but must copy values off the stack into locals.

## Pass #2: IL -> IL

The first pass produced a stream of instructions by calling methods on an `OptimisingEmitter` (`Yolol.IL\Compiler\Emitter\OptimisingEmitter.cs`), these methods have a one-to-one correspondence to IL instructions. When `Dispose` is called on the `OptimisingEmitter` this stream of instructions will be emitted.

The `Optimise` method is called to inspect the instruction stream and to rewrite it if necessary. This allows the first pass to emit correct but non-optimal IL. All of the optimisations at this level are "peephole optimisations" which scan for a specific pattern of instructions and rewrites it into something better.

For example, the `DupStorePopChain` optimisation finds sequences like this:

 - Duplicate()
 - Store()
 - Pop()

The `Duplicate` and `Pop` instructions are useless here since they duplicate a value, save the duplicate and then throw away the original. This is rewritten to:

 - Store()

# Contributing

Although Yolol.IL is quite a complex project overall contributing an single optimisation is relatively simple and should only touch a small part of the project. There are broadly three ways an optimisation can be added:

 - Add a way to discover more type information.
 - Modify a single `Visit` method to make better use of type information available to it.
 - Add a new peephole optimisation to the `OptimisingEmitter`.

Yolol.IL calls into the types provided by the [Yolol](https://github.com/martindevans/Yolol/tree/master/Yolol/Execution) project (i.e. Value/YString/Number). Optimisations to these types can often provide large speedups to compiled code.

If you are interested in contributing contact Yolathothep (Martin#2468) in [Discord](https://discord.gg/Dcn7BG4) to discuss your ideas.
