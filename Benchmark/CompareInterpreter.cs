using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class CompareInterpreter
    {
        private readonly Yolol.Grammar.AST.Program _ast;
        private readonly Network _network;
        private readonly MachineState _state;

        private readonly Func<Memory<Value>, Memory<Value>, int>[] _compiledLines;
        private readonly Value[] _internals;
        private readonly Value[] _externals;
        private readonly Func<Memory<Value>, Memory<Value>, int, int, int> _compiledProgram;

        public CompareInterpreter()
        {
            _ast = Parse(
                "a = :a b = :b c = a * b",
                "d = c * c e = sqrt(c + c + d)",
                "e++",
                "f = e / 2",
                ":g = f"
            );

            _network = new Network(1, 2, 0);
            _state = new MachineState(_network);

            var internalsPerLine = new Dictionary<string, int>();
            var externalsPerLine = new Dictionary<string, int>();
            _compiledLines = new Func<Memory<Value>, Memory<Value>, int>[_ast.Lines.Count];
            for (var i = 0; i < _ast.Lines.Count; i++)
                _compiledLines[i] = _ast.Lines[i].Compile(i + 1, 20, internalsPerLine, externalsPerLine);

            _internals = new Value[internalsPerLine.Count];
            _externals = new Value[externalsPerLine.Count];

            var internalsWholeProgram = new Dictionary<string, int>();
            var externalsWholeProgram = new Dictionary<string, int>();
            _compiledProgram = _ast.Compile(internalsWholeProgram, externalsWholeProgram);
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
            _network.Reset();
            for (var i = 0; i < _ast.Lines.Count; i++)
                _ast.Lines[i].Evaluate(i, _state);

            return _state;
        }

        [Benchmark]
        public (Value[] _internals, Value[] _externals) CompileLines()
        {
            Array.Fill(_externals, new Value(0));
            Array.Fill(_internals, new Value(0));

            var pc = 0;
            for (var i = 0; i < 5; i++)
                pc = _compiledLines[pc](_internals, _externals) - 1;

            return (_internals, _externals);
        }

        [Benchmark]
        public int CompileProgram()
        {
            Array.Fill(_externals, new Value(0));
            Array.Fill(_internals, new Value(0));

            return _compiledProgram(_internals, _externals, 0, 5);
        }

        [Params(0)]
        public Value ExternalA { get; set; }

        [Params(0)]
        public Value ExternalB { get; set; }

        [Benchmark]
        public Value Rewrite()
        {
            var a = ExternalA;
            var b = ExternalB;
            var c = a * b;

            var d = c * c;
            var e = (Value)(Number)Math.Sqrt((double)(c + c + d));

            e++;

            var f = e / 2;

            var g = f;
            return g;
        }
    }
}
