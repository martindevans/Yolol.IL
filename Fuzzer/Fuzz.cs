using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Fuzzer
{
    public class Fuzz
    {
        private readonly AstGenerator _generator;

        public Fuzz()
        {
            _generator = new AstGenerator(new Random());
        }

        public void Run(int iterations)
        {
            var ast = _generator.Generate();
            var max = Math.Max(20, ast.Lines.Count);

            // Execute program in interpreter and compiled code
            var interpretedTask = Task.Run(() => RunInterpreted(ast, max, iterations));
            var compiled = RunCompiled(ast, max, iterations);

            // Wait for interpreter to finish
            interpretedTask.Wait();
            var interpreted = interpretedTask.Result;

            Console.WriteLine($"Compile: {compiled.Prepare.TotalMilliseconds}ms");
            Console.WriteLine($"Execute: {compiled.Execute.TotalMilliseconds}ms");
            Console.WriteLine($"Interpret: {interpreted.Execute.TotalMilliseconds}ms");

            // Compare results
            //todo: interpreter does not implement string length limit, so results frequently differ
            //Compare(interpreted, compiled);
        }

        private void Compare(IExecutionResult expected, IExecutionResult actual)
        {
            if (expected.ProgramCounter != actual.ProgramCounter)
                throw new Exception($"PC differs! Expected:{expected.ProgramCounter} Actual:{actual.ProgramCounter}");

            var expectedValues = expected.Values().ToDictionary(a => a.Key, a => a.Value);
            foreach (var (k, v) in actual.Values())
            {
                var expectedVal = expectedValues.GetValueOrDefault(k, Number.Zero);
                if (expectedVal != v)
                    throw new Exception("Different values!");
            }
        }

        #region execution
        private static InterpretResult RunInterpreted(Yolol.Grammar.AST.Program ast, int max, int iters)
        {
            int CheckPc(int pc)
            {
                if (pc >= max)
                    return 0;
                if (pc < 0)
                    return 0;
                return pc;
            }

            var pc = 0;
            var nt = new DeviceNetwork();
            var st = new MachineState(nt, (ushort)max);

            var timer = new Stopwatch();
            timer.Start();
            for (var i = 0; i < iters; i++)
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

            return new InterpretResult(pc + 1, timer.Elapsed, nt, st);
        }

        private static CompiledResult RunCompiled(Yolol.Grammar.AST.Program ast, int max, int iters)
        {
            var compileTimer = new Stopwatch();
            compileTimer.Start();
            var externalsMap = new ExternalsMap();
            var compiled = ast.Compile(externalsMap, max, 1024, null, false);
            compileTimer.Stop();

            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, Number.Zero);
            var internals = new Value[compiled.InternalsMap.Count];
            Array.Fill(internals, Number.Zero);

            var execTimer = new Stopwatch();
            execTimer.Start();
            compiled.Run(internals, externals, iters, default);
            execTimer.Stop();

            return new CompiledResult(compiled.ProgramCounter, compileTimer.Elapsed, execTimer.Elapsed, compiled.InternalsMap, internals, externalsMap, externals);
        }
        #endregion

        #region helper classes
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

            public IEnumerable<KeyValuePair<VariableName, Value>> Values()
            {
                return _cache.Select(a => new KeyValuePair<VariableName, Value>(new VariableName($":{a.Key}"), a.Value.Value));
            }
        }

        private interface IExecutionResult
        {
            public TimeSpan Prepare { get; }
            public TimeSpan Execute { get; }

            public int ProgramCounter { get; }

            public IEnumerable<KeyValuePair<VariableName, Value>> Values();
        }

        private class InterpretResult
            : IExecutionResult
        {
            private readonly DeviceNetwork _network;
            private readonly MachineState _state;

            public TimeSpan Prepare => TimeSpan.Zero;
            public TimeSpan Execute { get; }
            public int ProgramCounter { get; }

            public InterpretResult(int programCounter, TimeSpan execute, DeviceNetwork network, MachineState state)
            {
                _network = network;
                _state = state;
                ProgramCounter = programCounter;
                Execute = execute;
            }

            public IEnumerable<KeyValuePair<VariableName, Value>> Values()
            {
                foreach (var item in _network.Values())
                    yield return item;
                foreach (var (key, value) in _state)
                    yield return new KeyValuePair<VariableName, Value>(new VariableName(key), value.Value);
            }
        }

        private class CompiledResult
            : IExecutionResult
        {
            private readonly IReadonlyInternalsMap _internalsMap;
            private readonly IReadOnlyList<Value> _internals;
            private readonly ExternalsMap _externalsMap;
            private readonly IReadOnlyList<Value> _externals;

            public TimeSpan Prepare { get; }
            public TimeSpan Execute { get; }
            public int ProgramCounter { get; }

            public CompiledResult(
                int programCounter,
                TimeSpan compile, TimeSpan execute,
                IReadonlyInternalsMap internalsMap, IReadOnlyList<Value> internals,
                ExternalsMap externalsMap, IReadOnlyList<Value> externals)
            {
                _internalsMap = internalsMap;
                _internals = internals;
                _externalsMap = externalsMap;
                _externals = externals;
                Prepare = compile;
                Execute = execute;
                ProgramCounter = programCounter;
            }

            public IEnumerable<KeyValuePair<VariableName, Value>> Values()
            {
                foreach (var (name, index) in _internalsMap)
                    yield return new KeyValuePair<VariableName, Value>(name, _internals[index]);
                foreach (var (name, index) in _externalsMap)
                    yield return new KeyValuePair<VariableName, Value>(name, _externals[index]);
            }
        }
        #endregion
    }
}
