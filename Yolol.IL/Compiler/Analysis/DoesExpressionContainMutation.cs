using System;
using Yolol.Analysis.ControlFlowGraph.AST;
using Yolol.Analysis.TreeVisitor;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;

namespace Yolol.IL.Compiler.Analysis
{
    internal static class DoesExpressionContainMutationExtension
    {
        /// <summary>
        /// tests if an expression contains an internal mutation (a++ or a--)
        /// </summary>
        public static bool DoesExpressionContainMutation(this BaseExpression expr)
        {
            return new DoesExpressionContainMutation().Visit(expr);
        }
    }

    /// <summary>
    /// tests if an expression contains an internal mutation (a++ or a--)
    /// </summary>
    internal class DoesExpressionContainMutation
        : BaseExpressionVisitor<bool>
    {
        protected override bool Visit(Or or) => base.Visit(or.Left) || base.Visit(or.Right);

        protected override bool Visit(And and) => base.Visit(and.Left) || base.Visit(and.Right);

        protected override bool Visit(Not not) => base.Visit(not.Parameter);

        protected override bool Visit(Factorial fac) => base.Visit(fac.Parameter);

        protected override bool Visit(ErrorExpression err) => false;

        protected override bool Visit(Increment inc) => true;

        protected override bool Visit(Decrement dec) => true;
       
        protected override bool Visit(Phi phi) => false;

        protected override bool Visit(LessThanEqualTo eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(LessThan eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(GreaterThanEqualTo eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(GreaterThan eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(NotEqualTo eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(EqualTo eq) => base.Visit(eq.Left) || base.Visit(eq.Right);

        protected override bool Visit(Variable var) => false;

        protected override bool Visit(Modulo mod) => base.Visit(mod.Left) || base.Visit(mod.Right);

        protected override bool Visit(PreDecrement dec) => true;

        protected override bool Visit(PostDecrement dec) => true;

        protected override bool Visit(PreIncrement inc) => true;

        protected override bool Visit(PostIncrement inc) => true;

        protected override bool Visit(Abs app) => base.Visit(app.Parameter);

        protected override bool Visit(Sqrt app) => base.Visit(app.Parameter);

        protected override bool Visit(Sine app) => base.Visit(app.Parameter);

        protected override bool Visit(Cosine app) => base.Visit(app.Parameter);

        protected override bool Visit(Tangent app) => base.Visit(app.Parameter);

        protected override bool Visit(ArcSine app) => base.Visit(app.Parameter);

        protected override bool Visit(ArcCos app) => base.Visit(app.Parameter);

        protected override bool Visit(ArcTan app) => base.Visit(app.Parameter);

        protected override bool Visit(Bracketed brk) => base.Visit(brk.Parameter);

        protected override bool Visit(Add add) => base.Visit(add.Left) || base.Visit(add.Right);

        protected override bool Visit(Subtract sub) => base.Visit(sub.Left) || base.Visit(sub.Right);

        protected override bool Visit(Multiply mul) => base.Visit(mul.Left) || base.Visit(mul.Right);

        protected override bool Visit(Divide div) => base.Visit(div.Left) || base.Visit(div.Right);

        protected override bool Visit(Exponent exp) => base.Visit(exp.Left) || base.Visit(exp.Right);

        protected override bool Visit(Negate neg) => base.Visit(neg.Parameter);

        protected override bool Visit(ConstantNumber con) => false;

        protected override bool Visit(ConstantString con) => false;
    }
}
