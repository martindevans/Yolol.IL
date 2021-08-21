using System;
using System.Collections.Generic;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static Grammar.AST.Program Parse(params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        public static IMachineState Test(string line, int lineNumber = 1, int? maxStringLength = null, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var internals = new InternalsMap();
            var externals = new ExternalsMap();

            var ast = Parse(line);
            var compiled = ast.Lines[0].Compile(lineNumber, 20, maxStringLength, internals, externals, staticTypes, true);

            var i = new Value[internals.Count];
            Array.Fill(i, new Value((Number)0));
            var e = new Value[externals.Count];
            Array.Fill(e, new Value((Number)0));

            var r = compiled.Invoke(i, e);

            return new EasyMachineState(i, e, internals, externals, r.ProgramCounter, r.ChangeSet);
        }

        public static IMachineState Test(string[] lines, int iterations, int? maxStringLength = null, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var ext = new ExternalsMap();
            var prog = Parser.ParseProgram(string.Join("\n", lines)).Ok;
            var compiled = prog.Compile(ext, maxStringLength: maxStringLength, staticTypes: staticTypes, changeDetection: true);
            var doneIndex = compiled.InternalsMap.GetValueOrDefault(new VariableName("done"), -1);

            var i = new Value[compiled.InternalsMap.Count];
            Array.Fill(i, new Value((Number)0));

            var e = new Value[ext.Count];
            Array.Fill(e, new Value((Number)0));

            for (var j = 0; j < iterations; j++)
            {
                compiled.Tick(i, e);

                var done = doneIndex < 0 ? Number.Zero : i[doneIndex];
                if (done.ToBool())
                    break;
            }

            return new CompiledMachineState(compiled, ext, i, e);
        }
    }

    public interface IMachineState
    {
        Value GetVariable(string v);

        ChangeSetKey GetVariableChangeSetKey(VariableName n);

        ChangeSet ChangeSet { get; }

        int ProgramCounter { get; }
    }

    public class EasyMachineState
        : IMachineState
    {
        public Value[] Internals;
        public Value[] Externals;

        public Dictionary<VariableName, int> InternalMap;
        public Dictionary<VariableName, int> ExternalMap;

        public ChangeSet ChangeSet { get; }
        public int ProgramCounter { get; }

        public EasyMachineState(Value[] i, Value[] e, InternalsMap internals, ExternalsMap externals, int pc, ChangeSet set)
        {
            Internals = i;
            Externals = e;
            InternalMap = internals;
            ExternalMap = externals;
            ProgramCounter = pc;
            ChangeSet = set;
        }

        public Value GetVariable(string v)
        {
            v = v.ToLowerInvariant();

            var n = new VariableName(v);
            var (a, m) = n.IsExternal ? (Externals, ExternalMap) : (Internals, InternalMap);
            return a[m[n]];
        }

        public ChangeSetKey GetVariableChangeSetKey(VariableName n)
        {
            if (!n.IsExternal)
                throw new ArgumentException("Variable must be external", nameof(n));

            return ExternalMap.ChangeSetKey(n);
        }
    }

    public class CompiledMachineState
        : IMachineState
    {
        private readonly CompiledProgram _program;
        private readonly IReadonlyExternalsMap _externalsMap;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        public ChangeSet ChangeSet => _program.ChangeSet;
        public int ProgramCounter => _program.ProgramCounter;

        public CompiledMachineState(CompiledProgram program, IReadonlyExternalsMap externalsMap, Value[] internals, Value[] externals)
        {
            _program = program;
            _externalsMap = externalsMap;
            _internals = internals;
            _externals = externals;
        }

        public Value GetVariable(string v)
        {
            var vn = new VariableName(v);

            if (vn.IsExternal)
            {
                if (!_externalsMap.TryGetValue(vn, out var idxe))
                    return Number.Zero;
                return _externals[idxe];
            }

            if (!_program.InternalsMap.TryGetValue(vn, out var idxi))
                return Number.Zero;
            return _internals[idxi];
        }

        public ChangeSetKey GetVariableChangeSetKey(VariableName n)
        {
            if (!n.IsExternal)
                throw new ArgumentException("Variable must be external", nameof(n));

            return _externalsMap.ChangeSetKey(n);
        }
    }
}
