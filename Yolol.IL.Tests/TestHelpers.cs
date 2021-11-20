using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static Program Parse(params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program:\n{result.Err}");

            return result.Ok;
        }

        public static IMachineState Test(string line, int lineNumber = 1, int? maxStringLength = null, IReadOnlyDictionary<VariableName, Type>? staticTypes = null, bool changeDetection = false)
        {
            var internals = new InternalsMap();
            var externals = new ExternalsMap();

            var ast = Parse(line);
            var compiled = ast.Lines[0].Compile(lineNumber, 20, maxStringLength, internals, externals, staticTypes, changeDetection);

            var i = new Value[internals.Count];
            Array.Fill(i, new Value((Number)0));
            var e = new Value[externals.Count];
            Array.Fill(e, new Value((Number)0));

            var r = compiled.Invoke(i, e);

            return new EasyMachineState(i, e, internals, externals, r.ProgramCounter, r.ChangeSet);
        }

        public static IMachineState Test(string[] lines, int iterations, int? maxStringLength = null, IReadOnlyDictionary<VariableName, Type>? staticTypes = null, bool changeDetection = false)
        {
            var prog = Parse(lines);
            return Test(prog, iterations, maxStringLength, staticTypes, changeDetection);
        }

        public static IMachineState Test(Program program, int iterations, int? maxStringLength = null, IReadOnlyDictionary<VariableName, Type>? staticTypes = null, bool changeDetection = false)
        {
            var ext = new ExternalsMap();
            var lines = Math.Max(20, program.Lines.Count);
            var compiled = program.Compile(ext, maxLines: lines, maxStringLength: maxStringLength, staticTypes: staticTypes, changeDetection: changeDetection);
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

        public static IMachineState Interpret(Program ast, int ticks)
        {
            var maxLines = Math.Max(20, ast.Lines.Count);

            int CheckPc(int pc)
            {
                if (pc >= maxLines)
                    return 0;
                if (pc < 0)
                    return 0;
                return pc;
            }

            var pc = 0;
            var nt = new DeviceNetwork();
            var st = new MachineState(nt, (ushort)maxLines);

            for (var i = 0; i < ticks; i++)
            {
                if (pc >= ast.Lines.Count)
                {
                    pc++;
                }
                else
                {
                    try
                    {
                        pc = ast.Lines[pc].Evaluate(pc, st);
                    }
                    catch (ExecutionException)
                    {
                        pc++;
                    }
                }

                pc = CheckPc(pc);
            }

            return new InterpretMachineState(pc + 1, nt, st);
        }

        private class DeviceNetwork
            : IDeviceNetwork
        {
            private readonly Dictionary<string, IVariable> _cache = new Dictionary<string, IVariable>();

            public IVariable Get(string name)
            {
                name = name.ToLowerInvariant();
                if (!_cache.TryGetValue(name, out var result))
                {
                    result = new Variable();
                    result.Value = Number.Zero;
                    _cache[name] = result;
                }

                return result;
            }
        }
    }

    public interface IMachineState
    {
        Value GetVariable(string v);

        ChangeSetKey GetVariableChangeSetKey(VariableName n);

        ChangeSet ChangeSet { get; }

        int ProgramCounter { get; }
    }

    public class InterpretMachineState
        : IMachineState
    {
        private readonly MachineState _machineState;

        public int ProgramCounter { get; }

        public InterpretMachineState(int pc, IDeviceNetwork deviceNetwork, MachineState machineState)
        {
            _machineState = machineState;
            ProgramCounter = pc;
        }

        public Value GetVariable(string v)
        {
            return _machineState.GetVariable(v).Value;
        }

        ChangeSetKey IMachineState.GetVariableChangeSetKey(VariableName n)
        {
            throw new NotSupportedException();
        }

        ChangeSet IMachineState.ChangeSet => throw new NotSupportedException();
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
