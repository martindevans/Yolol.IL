using System;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL.Extensions
{
    internal static class ValueExtensions
    {
        /// <summary>
        /// Coerce a value into specific type, throws id the coercion is invalid
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        public static object Coerce(this Value input, StackType output)
        {
            var inputType = input.Type;

            switch (inputType, output)
            {
                #region identity conversions
                case (Execution.Type.String, StackType.YololString):
                    return input.String;

                case (Execution.Type.Number, StackType.YololNumber):
                    return input.Number;
                #endregion

                #region error conversion
                case (Execution.Type.Error, _):
                    throw new InvalidOperationException("Cannot coerce from Error type");
                case (Execution.Type.Unassigned, _):
                    throw new InvalidOperationException("Cannot coerce from Unassigned type");
                case (_, StackType.StaticError):
                    throw new InvalidOperationException("Cannot coerce to StaticError type");
                #endregion

                #region number conversion
                case (Execution.Type.Number, StackType.YololString):
                    throw new InvalidOperationException("Cannot coerce number -> string");

                case (Execution.Type.Number, StackType.Bool):
                    if (input.Number == Number.Zero || input.Number == Number.One)
                        return input.Number == Number.One;
                    throw new InvalidOperationException("Cannot coerce number -> bool");
                #endregion

                #region string conversion
                case (Execution.Type.String, StackType.YololNumber):
                    throw new InvalidOperationException("Cannot coerce string -> number");

                case (Execution.Type.String, StackType.Bool):
                    throw new InvalidOperationException("Cannot coerce string -> bool");
                #endregion

                default:
                    throw new InvalidOperationException($"Cannot coerce `{input}` -> `{output}`");
            }
        }
    }
}
