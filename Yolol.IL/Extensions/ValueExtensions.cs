using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Compiler.Exceptions;

namespace Yolol.IL.Extensions
{
    internal static class ValueExtensions
    {
        /// <summary>
        /// Coerce a value into specific type, throws if the coercion is invalid
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        public static object Coerce(this Value input, StackType output)
        {
            ThrowHelper.Check(input.Type != Execution.Type.Error, "Cannot coerce from Error type");
            ThrowHelper.Check(input.Type != Execution.Type.Unassigned, "Cannot coerce from Unassigned type");
            ThrowHelper.Check(output != StackType.StaticError, "Cannot coerce to StaticError type");

            var inputType = input.Type;

            switch (inputType, output)
            {
                #region identity conversions
                case (Execution.Type.String, StackType.YololString):
                    return input.String;

                case (Execution.Type.Number, StackType.YololNumber):
                    return input.Number;
                #endregion

                #region number conversion
                case (Execution.Type.Number, StackType.Bool):
                    if (input.Number == Number.Zero || input.Number == Number.One)
                        return input.Number == Number.One;
                    throw new InternalCompilerException("Cannot coerce number -> bool");
                #endregion

                default:
                    throw new InternalCompilerException($"Cannot coerce `{input}` -> `{output}`");
            }
        }
    }
}
