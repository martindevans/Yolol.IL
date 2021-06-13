using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static Grammar.AST.Program Parse([NotNull] params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        public static (IMachineState, int) Test(string line, int lineNumber = 1, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var internals = new InternalsMap();
            var externals = new ExternalsMap();

            var ast = Parse(line);
            var compiled = ast.Lines[0].Compile(lineNumber, 20, internals, externals, staticTypes);

            var i = new Value[internals.Count];
            Array.Fill(i, new Value((Number)0));
            var e = new Value[externals.Count];
            Array.Fill(e, new Value((Number)0));

            var r = compiled.Invoke(i, e);

            return (new EasyMachineState(i, e, internals, externals), r);
        }

        public static (IMachineState, int) Test(string[] lines, int iterations, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var ext = new ExternalsMap();
            var prog = Parser.ParseProgram(string.Join("\n", lines)).Ok;
            var compiled = prog.Compile(ext, staticTypes: staticTypes);

            for (var j = 0; j < iterations; j++)
            {
                compiled.Tick();

                var done = compiled["done"];
                if (done.ToBool())
                    break;
            }

            return (new CompiledMachineState(compiled), compiled.ProgramCounter);
        }
    }

    public interface IMachineState
    {
        Value GetVariable(string v);
    }

    public class EasyMachineState
        : IMachineState
    {
        public Value[] Internals;
        public Value[] Externals;

        public Dictionary<string, int> InternalMap;
        public Dictionary<string, int> ExternalMap;

        public EasyMachineState(Value[] i, Value[] e, InternalsMap internals, ExternalsMap externals)
        {
            Internals = i;
            Externals = e;
            InternalMap = internals;
            ExternalMap = externals;
        }

        public Value GetVariable(string v)
        {
            v = v.ToLowerInvariant();

            var n = new VariableName(v);
            var (a, m) = n.IsExternal ? (Externals, ExternalMap) : (Internals, InternalMap);
            return a[m[v]];
        }
    }

    public class CompiledMachineState
        : IMachineState
    {
        private readonly CompiledProgram _program;

        public CompiledMachineState(CompiledProgram program)
        {
            _program = program;
        }

        public Value GetVariable(string v)
        {
            v = v.ToLowerInvariant();

            return _program[v];
        }
    }
}
