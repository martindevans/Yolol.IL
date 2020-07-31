using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;

namespace Benchmark
{
    public class CompareInterpreter
    {
        private readonly Yolol.Grammar.AST.Program _ast;
        private readonly Network _network;
        private readonly MachineState _state;

        private Func<Memory<Value>, Memory<Value>, int>[] _compiledLines;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        public CompareInterpreter()
        {
            _ast = Parse(
                "a = :a b = :b c = a * b",
                "d = c * c e = sqrt(c + c)",
                "e++",
                "f = e / 2",
                ":g = f"
            );

            _network = new Network(1, 2, 0);
            _state = new MachineState(_network);

            var internals = new Dictionary<string, int>();
            var externals = new Dictionary<string, int>();
            _compiledLines = new Func<Memory<Value>, Memory<Value>, int>[_ast.Lines.Count];
            for (var i = 0; i < _ast.Lines.Count; i++)
                _compiledLines[i] = _ast.Lines[i].Compile(i + 1, 20, internals, externals);

            _internals = new Value[internals.Count];
            _externals = new Value[externals.Count];
        }

        private static Yolol.Grammar.AST.Program Parse([NotNull] params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        private class Network
            : IDeviceNetwork
        {
            private readonly Number[] _values;
            private int _next;

            public Network(params Number[] values)
            {
                _values = values;
            }

            public IVariable Get(string name)
            {
                return new Variable {
                    Value = _values[_next++]
                };
            }

            public void Reset()
            {
                _next = 0;
            }
        }

        [Benchmark]
        public MachineState Interpret()
        {
            for (var j = 0; j < 100; j++)
            {
                _network.Reset();
                for (var i = 0; i < _ast.Lines.Count; i++)
                    _ast.Lines[i].Evaluate(i, _state);
            }

            return _state;
        }

        [Benchmark]
        public (Value[] _internals, Value[] _externals) Execute()
        {
            for (var j = 0; j < 100; j++)
            {
                Array.Fill(_externals, new Value(0));
                Array.Fill(_internals, new Value(0));

                for (var i = 0; i < _ast.Lines.Count; i++)
                    _compiledLines[i](_internals, _externals);
            }

            return (_internals, _externals);
        }
    }
}
