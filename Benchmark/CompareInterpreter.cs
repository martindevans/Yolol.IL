using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class CompareInterpreter
    {
        private readonly Yolol.Grammar.AST.Program _ast;
        private readonly Network _network;
        private readonly MachineState _state;

        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, LineResult>[] _compiledLines;
        private readonly CompiledProgram _compiledProgramLine;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        public CompareInterpreter()
        {
            _ast = Parse(
                ":done++ b=97 c=89",
                ":o++ :done++",
                ":done++",
                "i=127-1 _=(i/3%1==0)*i/3>1+(i/5%1==0)*i/5>1+(i/7%1==0)*i/7>1 a=i/11%1==0",
                "_+=a*i/11>1+(i/13%1==0)*i/13>1+(i/17%1==0)*i/17>1+(i/19%1==0)*i/19>1",
                "_+=(i/23%1==0)*i/23>1+(i/29%1==0)*i/29>1+(i/31%1==0)*i/31>1a=i/37%1==0",
                "_+=a*i/37>1+(i/41%1==0)*i/41>1+(i/43%1==0)*i/43>1+(i/47%1==0)*i/47>1",
                "_+=(i/53%1==0)*i/53>1+(i/59%1==0)*i/59>1+(i/61%1==0)*i/61>1a=i/67%1==0",
                "_+=a*i/67>1+(i/71%1==0)*i/71>1+(i/73%1==0)*i/73>1+(i/79%1==0)*i/79>1",
                "_+=(i/83%1==0)*i/83>1+(i/c%1==0)*i/c>1+(i/b%1==0)*i/b>1:o+=_<1:done++",
                "z=:o :done++goto4"
            );

            _network = new Network((Number)1, (Number)2, (Number)0);
            _state = new MachineState(_network);

            var types = new Dictionary<VariableName, Yolol.Execution.Type> {
                { new VariableName("a"), Yolol.Execution.Type.Number },
                { new VariableName("b"), Yolol.Execution.Type.Number },
                { new VariableName("c"), Yolol.Execution.Type.Number },
                { new VariableName("d"), Yolol.Execution.Type.Number },
                { new VariableName("e"), Yolol.Execution.Type.Number },
                { new VariableName("f"), Yolol.Execution.Type.Number },
            };
            var internalsPerLine = new InternalsMap();
            var externalsPerLine = new ExternalsMap();
            _compiledLines = new Func<ArraySegment<Value>, ArraySegment<Value>, LineResult>[_ast.Lines.Count];
            for (var i = 0; i < _ast.Lines.Count; i++)
                _compiledLines[i] = _ast.Lines[i].Compile(i + 1, 20, null, internalsPerLine, externalsPerLine, types);

            _internals = new Value[internalsPerLine.Count];
            _externals = new Value[externalsPerLine.Count];

            _compiledProgramLine = _ast.Compile(new ExternalsMap(), 20, null, types);
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
            Array.Fill(_externals, new Value((Number)0));
            Array.Fill(_internals, new Value((Number)0));

            var pc = 0;
            for (var i = 0; i < _ast.Lines.Count; i++)
                pc = _compiledLines[pc].Invoke(_internals, _externals).ProgramCounter - 1;

            return (_internals, _externals);
        }

        [Benchmark]
        public Value[] CompileProgram()
        {
            Array.Fill(_externals, new Value((Number)0));
            Array.Fill(_internals, new Value((Number)0));

            for (var i = 0; i < _ast.Lines.Count; i++)
                _compiledProgramLine.Tick(_internals, _externals);

            return _externals;
        }

        [Params(0)]
        public Value ExternalA { get; set; }

        [Params(0)]
        public Value ExternalB { get; set; }

        [Benchmark]
        public (Value[] _internals, Value[] _externals) Rewrite()
        {
            Array.Fill(_externals, new Value((Number)0));
            Array.Fill(_internals, new Value((Number)0));

            var a = ExternalA;
            var b = ExternalB;
            var c = a * b;

            var d = c * c;
            var e = (Value)(Number)Math.Sqrt((float)(c + c + d));

            e++;

            var f = e / (Number)2;

            _externals[0] = f;

            return (_internals, _externals);
        }
    }
}
