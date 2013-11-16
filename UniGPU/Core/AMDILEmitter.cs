//
// AMDILEmitter.cs
//
// Author:
//   Artem Lebedev (tementy@gmail.com)
//
// (C) 2012 Rybinsk State Aviation Technical University (http://www.rsatu.ru)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UniGPU.Core.IR;
using UniGPU.Core.Utils;

namespace UniGPU.Core
{
	public static class AMDILEmitter
	{
		// Reserved registers:
		// r0.w - basic block selector
		// r0.xyz - block dimensions (thread-wise)
		// r1.xyz - grid dimensions (block-wise)
		// r2, r3 - temporary result holders

		private class LiteralPool
		{
			private List<ValueType> pool = new List<ValueType>();

			public IList<ValueType> Pool { get { return pool.AsReadOnly(); } }

			public string this[ValueType val]
			{
				get
				{
					int idx = pool.IndexOf(val);
					if (idx == -1)
					{
						pool.Add(val);
						idx = pool.Count - 1;
					}
					return "l" + idx.ToString();
				}
			}
		}

		private static string ToAMDIL(this GenericOperand operand, LiteralPool literals)
		{
			if (operand is ImmediateValue)
			{
				ImmediateValue iv = operand as ImmediateValue;
				string literal = literals[iv.Value];
				switch (iv.Value.GetType().SizeOf())
				{
				case 4:
					return literal + ".x";
				case 8:
					return literal + ".xy";
				case 16:
					return literal + ".xyzw";
				default:
					throw new NotSupportedException(iv.Value.GetType().Format());
				}
			} else
				return operand.ToString();
		}

		private static string ToAMDIL(this BasicBlockInstruction bbi,
			LiteralPool literals,
			Dictionary<string, int> functions = null,
			int raw_uav_id = 11,
			int arena_uav_id = 13)
		{
			BinaryOperation bop = bbi as BinaryOperation;
			UnaryOperation uop = bbi as UnaryOperation;

			switch (bbi.OpCode)
			{
			case IROpCodes.ADD:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
				case TypeCode.UInt32:
					return string.Format(
						"iadd {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return string.Format(
						"i64add {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"add {0}, {1}, {2}",
						bop.Target.ToString(), 
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dadd {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.SUB:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
				case TypeCode.UInt32:
					return string.Format(
						"inegate r2.x, {2}\n" +
						"iadd {0}, {1}, r2.x",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return string.Format(
						"i64negate r2.xy, {2}\n" +
						"i64add {0}, {1}, r2.xy",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"sub {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dadd {0}, {1}, {2}_neg(xyzw)",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.MUL:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
				case TypeCode.UInt32:
					return string.Format(
						"imul {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return string.Format(
						"i64mul {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"mul_ieee {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dmul {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.MAD:
				{
					MADOperation mad = bbi as MADOperation;
					
					switch (Type.GetTypeCode(mad.Target.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"imad {0}, {1}, {2}, {3}",
							mad.Target.ToString(),
							mad.MulLeftOperand.ToAMDIL(literals),
							mad.MulRightOperand.ToAMDIL(literals),
							mad.AddOperand.ToAMDIL(literals)
						);
					case TypeCode.UInt32:
						return string.Format(
							"umad {0}, {1}, {2}, {3}",
							mad.Target.ToString(),
							mad.MulLeftOperand.ToAMDIL(literals),
							mad.MulRightOperand.ToAMDIL(literals),
							mad.AddOperand.ToAMDIL(literals)
						);
					case TypeCode.Int64:
					case TypeCode.UInt64:
						return string.Format(
							"i64mul r2.xy, {1}, {2}\n" +
							"i64add {0}, r2.xy, {3}",
							mad.Target.ToString(),
							mad.MulLeftOperand.ToAMDIL(literals),
							mad.MulRightOperand.ToAMDIL(literals),
							mad.AddOperand.ToAMDIL(literals)
						);
					case TypeCode.Single:
						return string.Format(
							"mad_ieee {0}, {1}, {2}, {3}",
							mad.Target.ToString(),
							mad.MulLeftOperand.ToAMDIL(literals),
							mad.MulRightOperand.ToAMDIL(literals),
							mad.AddOperand.ToAMDIL(literals)
						);
					case TypeCode.Double:
						return string.Format(
							"dmad {0}, {1}, {2}, {3}",
							mad.Target.ToString(),
							mad.MulLeftOperand.ToAMDIL(literals),
							mad.MulRightOperand.ToAMDIL(literals),
							mad.AddOperand.ToAMDIL(literals)
						);
					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				}
			case IROpCodes.DIV:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
					return string.Format(
						"ilt r2.x, {1}, {3}\n" + // compare left operand with 0, get -1 if negative or 0 otherwise
						"iadd r2.z, {1}, r2.x\n" + // r2.z = abs(left operand), see IROpCodes.ABS case
						"ixor r2.z, r2.z, r2.x\n" +
						"ilt r2.y, {2}, {3}\n" + // compare right operand with 0, get -1 if negative or 0 otherwise
						"iadd r2.w, {2}, r2.y\n" + // r2.w = abs(right operand), see IROpCodes.ABS case
						"ixor r2.w, r2.w, r2.y\n" +
						"udiv {0}, r2.z, r2.w\n" + // perform division with non-negative operands
						"ixor r2.x, r2.x, r2.y\n" + // get result sign factor (-1 if negative or 0 otherwise)
						"iadd {0}, {0}, r2.x\n" + // perform negation if needed
						"ixor {0}, {0}, r2.x",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals),
						literals[(int)0] + ".x");
				case TypeCode.UInt32:
					return string.Format(
						"udiv {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
					return string.Format(
						"i64div {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.UInt64:
					return string.Format(
						"u64div {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"div_zeroop(infinity) {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"ddiv {0}, {1}, {2}",
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.REM:
				{
					VirtualRegister tmp = new VirtualRegister(bop.Target.DataType) { Name = 
						"r3" + SwizzleMask(bop.Target.ToString())};

					return new BinaryOperation(
						IROpCodes.DIV,
						tmp,
						bop.LeftOperand,
						bop.RightOperand
					).ToAMDIL(literals) + "\n" + new BinaryOperation(
						IROpCodes.MUL,
						tmp,
						tmp,
						bop.RightOperand
					).ToAMDIL(literals) + "\n" + new BinaryOperation(
						IROpCodes.SUB,
						bop.Target,
						bop.LeftOperand,
						tmp
					).ToAMDIL(literals);
				}
			case IROpCodes.MIN:
			case IROpCodes.MAX:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
					return string.Format(
						"i{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.UInt32:
					return string.Format(
						"u{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
					return string.Format(
						"i64{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.UInt64:
					return string.Format(
						"u64{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"{0}_ieee {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"d{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.AND:
			case IROpCodes.OR:
			case IROpCodes.XOR:
				return string.Format(
					"i{0} {1}, {2}, {3}",
					bop.OpCode.ToString().ToLower(),
					bop.Target.ToString(),
					bop.LeftOperand.ToAMDIL(literals),
					bop.RightOperand.ToAMDIL(literals)
				);
			case IROpCodes.EQ:
			case IROpCodes.NE:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
				case TypeCode.UInt32:
					return string.Format(
						"i{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return string.Format(
						"i64{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"d{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.GE:
			case IROpCodes.LT:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Int32:
					return string.Format(
						"i{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.UInt32:
					return string.Format(
						"u{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
					return string.Format(
						"i64{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.UInt64:
					return string.Format(
						"u64{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"d{0} {1}, {2}, {3}",
						bbi.OpCode.ToString().ToLower(),
						bop.Target.ToString(),
						bop.LeftOperand.ToAMDIL(literals),
						bop.RightOperand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.GT:
				return new BinaryOperation(
					IROpCodes.LT,
					bop.Target,
					bop.RightOperand,
					bop.LeftOperand
				).ToAMDIL(literals);
			case IROpCodes.LE:
				return new BinaryOperation(
					IROpCodes.GE,
					bop.Target,
					bop.RightOperand,
					bop.LeftOperand
				).ToAMDIL(literals);
			case IROpCodes.NEG:
				switch (Type.GetTypeCode(uop.Target.DataType))
				{
				case TypeCode.Int32:
					return string.Format(
						"inegate {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Int64:
					return string.Format(
						"i64negate {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Single:
					return string.Format(
						"mov {0}, {1}_neg(xyzw)",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dadd {0}, {1}_neg(xyzw), {2}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals),
						literals[(int)0] + ".xy"
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.NOT:
				return string.Format(
					"inot {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.MOV:
				return string.Format(
					"mov {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.ABS:
				switch (Type.GetTypeCode(uop.Target.DataType))
				{
				case TypeCode.Int32:
					return string.Format(
						"ishr r2.x, {1}, {2}\n" + // -1 if negative or 0 otherwise
						"iadd {0}, {1}, r2.x\n" + // substract 1 if negative or keep unchanged otherwise
						"ixor {0}, {0}, r2.x",    // perform a bit-wise one's complement if negative or keep unchanged otherwise
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals),
						literals[(int)31] + ".x"
					);
				case TypeCode.Int64:
					return string.Format(
						"i64shr r2.x, {1}, {2}\n" +  // -1 if negative or 0 otherwise
						"i64add {0}, {1}, r2.xx\n" + // substract 1 if negative or keep unchanged otherwise
						"ixor {0}, {0}, r2.xx",      // perform a bit-wise one's complement if negative or keep unchanged otherwise
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals),
						literals[(int)63] + ".x"
					);
				case TypeCode.Single:
					return string.Format(
						"abs {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dadd {0}, {1}_abs, {2}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals),
						literals[(int)0] + ".xy"
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.CVT:
				switch (Type.GetTypeCode(uop.Target.DataType))
				{
				case TypeCode.Int32:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.UInt32:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Int64:
					case TypeCode.UInt64:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							LowerPart(uop.Operand.ToAMDIL(literals))
						);
					case TypeCode.Single:
						return string.Format(
							"ftoi {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Double:
						return string.Format(
							"d2f {0}, {1}\n" +
							"ftoi {0}, {0}",
							uop.Target.ToString(), uop.Operand.ToAMDIL(literals));
					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				case TypeCode.UInt32:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Int64:
					case TypeCode.UInt64:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							LowerPart(uop.Operand.ToAMDIL(literals))
						);
					case TypeCode.Single:
						return string.Format(
							"ftou {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Double:
						return string.Format(
							"d2f {0}, {1}\n" +
							"ftou {0}, {0}",
							uop.Target.ToString(), uop.Operand.ToAMDIL(literals));
					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				case TypeCode.Int64:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"ishr r2.y, {1}, {2}\n" + // -1 if negative or 0 otherwise
							"mov r2.x, {1}\n" +       // perform sign extension
							"mov {0}, r2.xy",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals),
							literals[(int)31] + ".x"
						);
					case TypeCode.UInt32:
						return string.Format(
							"mov {0}, {1}0",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.UInt64:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Single:

					case TypeCode.Double:

					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				case TypeCode.UInt64:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"ishr r2.y, {1}, {2}\n" + // -1 if negative or 0 otherwise
							"mov r2.x, {1}\n" + // perform sign extension
							"mov {0}, r2.xy",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals),
							literals[(int)31] + ".x"
						);
					case TypeCode.UInt32:
						return string.Format(
							"mov {0}, {1}0",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Int64:
						return string.Format(
							"mov {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Single:

					case TypeCode.Double:

					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				case TypeCode.Single:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"itof {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);							
					case TypeCode.UInt32:
						return string.Format(
							"utof {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					case TypeCode.Int64:
						// ugly, i know!!!
						return string.Format(
							"itof {0}, {1}",
							uop.Target.ToString(),
							LowerPart(uop.Operand.ToAMDIL(literals))
						);
					case TypeCode.UInt64:
						// ugly, i know!!!
						return string.Format(
							"utof {0}, {1}",
							uop.Target.ToString(),
							LowerPart(uop.Operand.ToAMDIL(literals))
						);
					case TypeCode.Double:
						return string.Format(
							"d2f {0}, {1}",
							uop.Target.ToString(),
							uop.Operand.ToAMDIL(literals)
						);
					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				case TypeCode.Double:
					switch (Type.GetTypeCode(uop.Operand.DataType))
					{
					case TypeCode.Int32:
						return string.Format(
							"itof r2.x, {1}\n" +
							"f2d {0}, r2.x",
							uop.Target.ToString(), uop.Operand.ToAMDIL(literals));
					case TypeCode.UInt32:
						return string.Format(
							"utof r2.x, {1}\n" +
							"f2d {0}, r2.x",
							uop.Target.ToString(), uop.Operand.ToAMDIL(literals));
					case TypeCode.Int64:
						// ugly, i know!!!
						return string.Format(
							"itof r2.x, {1}\n" +
							"f2d {0}, r2.x",
							uop.Target.ToString(), LowerPart(uop.Operand.ToAMDIL(literals)));
					case TypeCode.UInt64:
						// ugly, i know!!!
						return string.Format(
							"utof r2.x, {1}\n" +
							"f2d {0}, r2.x",
							uop.Target.ToString(), LowerPart(uop.Operand.ToAMDIL(literals)));
					case TypeCode.Single:
						return string.Format(
									"f2d {0}, {1}",
									uop.Target.ToString(),
									uop.Operand.ToAMDIL(literals)
						);
					default:
						throw new NotSupportedException(bop.Target.DataType.Format());
					}
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.LD:
				{
					LDInstruction op = bbi as LDInstruction;
					switch (op.Address.StateSpace)
					{
					case StateSpaces.GLOBAL:
						switch (Type.GetTypeCode(op.Address.UnderlyingType))
						{
						case TypeCode.Boolean:
						case TypeCode.SByte:
						case TypeCode.Byte:
							return string.Format(
								"uav_arena_load_id({3})_size(byte)_cached {0}, {1}\n" +
								"ishl {0}, {0}, {2}\n" +
								"ishr {0}, {0}, {2}",
								op.Target.ToString(),
								op.Address.ToString(),
								literals[(int)24] + ".x",
								arena_uav_id
							);
						case TypeCode.Int16:
						case TypeCode.UInt16:
							return string.Format(
								"uav_arena_load_id({3})_size(short)_cached {0}, {1}\n" +
								"ishl {0}, {0}, {2}\n" +
								"ishr {0}, {0}, {2}",
								op.Target.ToString(),
								op.Address.ToString(),
								literals[(int)16] + ".x",
								arena_uav_id
							);
						case TypeCode.Int32:
						case TypeCode.UInt32:
						case TypeCode.Single:
							return string.Format(
								"uav_raw_load_id({2})_cached {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString(),
								raw_uav_id
							);				
						case TypeCode.Int64:
						case TypeCode.UInt64:
						case TypeCode.Double:
							return string.Format(
								"uav_raw_load_id({2})_cached {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString(),
								raw_uav_id
							);				
						default:
							throw new NotSupportedException(bop.Target.DataType.Format());
						}
					case StateSpaces.SHARED:
						switch (Type.GetTypeCode(op.Address.UnderlyingType))
						{
						case TypeCode.Boolean:
						case TypeCode.SByte:
							return string.Format(
								"lds_load_byte_id(1) {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);
						case TypeCode.Byte:
							return string.Format(
								"lds_load_ubyte_id(1) {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);
						case TypeCode.Int16:
							return string.Format(
								"lds_load_short_id(1) {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);
						case TypeCode.UInt16:
							return string.Format(
								"lds_load_ushort_id(1) {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);
						case TypeCode.Int32:
						case TypeCode.UInt32:
						case TypeCode.Single:
							return string.Format(
								"lds_load_id(1) {0}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);				
						case TypeCode.Int64:
						case TypeCode.UInt64:
						case TypeCode.Double:
							return string.Format(
								"lds_load_vec_id(1) {0}, {1}, {1}",
								op.Target.ToString(),
								op.Address.ToString()
							);
						default:
							throw new NotSupportedException(bop.Target.DataType.Format());
						}
					case StateSpaces.CONSTANT:
					default:
						throw new NotImplementedException();
					}
				}
			case IROpCodes.ST:
				{
					STInstruction op = bbi as STInstruction;
					switch (op.Address.StateSpace)
					{
					case StateSpaces.GLOBAL:
						switch (Type.GetTypeCode(op.Address.UnderlyingType))
						{
						case TypeCode.Boolean:
						case TypeCode.SByte:
						case TypeCode.Byte:
							return string.Format(
								"iand {1}, {1}, {3}\n" +
								"uav_arena_store_id({2})_size(byte) {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString(),
								arena_uav_id,
								literals[(int)255]
							);
						case TypeCode.Int16:
						case TypeCode.UInt16:
							return string.Format(
								"iand {1}, {1}, {3}\n" +
								"uav_arena_store_id({2})_size(short) {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString(),
								arena_uav_id,
								literals[(int)65535]
							);
						case TypeCode.Int32:
						case TypeCode.UInt32:
						case TypeCode.Single:
							return string.Format(
								"uav_raw_store_id({2}) mem0.x, {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString(),
								raw_uav_id
							);
						case TypeCode.Int64:
						case TypeCode.UInt64:
						case TypeCode.Double:
							return string.Format(
								"uav_raw_store_id({2}) mem0.xy, {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString(),
								raw_uav_id
							);
						default:
							throw new NotSupportedException(bop.Target.DataType.Format());
						}
					case StateSpaces.SHARED:
						switch (Type.GetTypeCode(op.Address.UnderlyingType))
						{
						case TypeCode.Boolean:
						case TypeCode.SByte:
						case TypeCode.Byte:
							return string.Format(
								"lds_store_byte_id(1) {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString()
							);
						case TypeCode.Int16:
						case TypeCode.UInt16:
							return string.Format(
								"lds_store_short_id(1) {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString()
							);
						case TypeCode.Int32:
						case TypeCode.UInt32:
						case TypeCode.Single:
							return string.Format(
								"lds_store_id(1) {0}, {1}",
								op.Address.ToString(),
								op.Source.ToString()
							);				
						case TypeCode.Int64:
						case TypeCode.UInt64:
						case TypeCode.Double:
							return string.Format(
								"lds_store_vec_id(1) mem0.xy, {0}, {1}, {1}",
								op.Address.ToString(),
								op.Source.ToString()
							);
						default:
							throw new NotSupportedException(bop.Target.DataType.Format());
						}
					case StateSpaces.CONSTANT:
					default:
						throw new NotImplementedException();
					}
				}
			case IROpCodes.SYNC:
				return "fence_threads_lds_memory";
			case IROpCodes.SQRT:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Single:
					return string.Format(
						"sqrt_vec {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"dsqrt {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.RSQRT:
				switch (Type.GetTypeCode(bop.Target.DataType))
				{
				case TypeCode.Single:
					return string.Format(
						"rsq_vec {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				case TypeCode.Double:
					return string.Format(
						"drsq_zeroop(infinity) {0}, {1}",
						uop.Target.ToString(),
						uop.Operand.ToAMDIL(literals)
					);
				default:
					throw new NotSupportedException(bop.Target.DataType.Format());
				}
			case IROpCodes.SIN:
				return string.Format(
					"sin_vec {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.COS:
				return string.Format(
					"cos_vec {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.LG2:
				return string.Format(
					"log_vec {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.EX2:
				return string.Format(
					"exp_vec {0}, {1}",
					uop.Target.ToString(),
					uop.Operand.ToAMDIL(literals)
				);
			case IROpCodes.CALL:
				{
					CALLInstruction call = bbi as CALLInstruction;
				
					IEnumerable<KeyValuePair<FormalParameter, GenericOperand>> argmap = call.Target.FormalParameters.Zip(
						call.Arguments,
						(fp, ap) => new KeyValuePair<FormalParameter, GenericOperand>(fp, ap)
					);
								
					return string.Join(
						"\n",
						from kvp in argmap where kvp.Key.PassingStyle != PassingStyles.OUT select string.Format(
						"mov {0}, {1}",
						kvp.Key,
						kvp.Value
					)
					) + "\n" + string.Format(
						"call {0}",
						functions[call.Target.Name]
					) + "\n" + string.Join(
						"\n",
						from kvp in argmap where kvp.Key.PassingStyle != PassingStyles.VAL select string.Format(
						"mov {1}, {0}",
						kvp.Key,
						kvp.Value
					)
					);
				}
			default:
				throw new NotSupportedException(bbi.OpCode.ToString());
			}
		}

		private static string ToAMDIL(this ControlFlowInstruction cfi,
			LiteralPool literals,
			IList<BasicBlock> bblist)
		{
			JumpInstruction jump = cfi as JumpInstruction;
			JumpIfInstruction jumpif = cfi as JumpIfInstruction;
			switch (cfi.OpCode)
			{
			case IROpCodes.JMP:
				return string.Format(
					"mov r0.w, {0}.w",
					literals[bblist.IndexOf(jump.Target)]
				);
			case IROpCodes.JT:
				return string.Format(
					"if_logicalnz {0}\n" +
					"mov r0.w, {1}.w\n" +
					"else\n" +
					"mov r0.w, {2}.w\n" +
					"endif",
					jumpif.Flag.ToString(),
					literals[bblist.IndexOf(jumpif.Target)],
					literals[bblist.IndexOf(jumpif.Next)]
				);
			case IROpCodes.JF:
				return string.Format(
					"if_logicalz {0}\n" +
					"mov r0.w, {1}.w\n" +
					"else\n" +
					"mov r0.w, {2}.w\n" +
					"endif",
					jumpif.Flag.ToString(),
					literals[bblist.IndexOf(jumpif.Target)],
					literals[bblist.IndexOf(jumpif.Next)]
				);
			case IROpCodes.RET:
					return "ret_dyn";
			default:
				throw new NotSupportedException(cfi.OpCode.ToString());
			}
		}

		private static string ToAMDIL(
			this TreeStatement src,
			LiteralPool literals,
			Dictionary<string, int> functions = null,
			int raw_uav_id = 11,
			int arena_uav_id = 13)
		{
			switch (src.StatementType)
			{
				case StatementTypes.INSTRUCTION:
					return (src as InstructionStatement).Instruction.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id);
				case StatementTypes.BRANCH:
				{
					BranchStatement bs = src as BranchStatement;
					if (bs.TrueBranch == null)
						return string.Format(
						"if_logicalz {0}\n" +
						"{1}\n" +
						"endif",
						bs.Flag.ToString(),
						string.Join("\n", bs.FalseBranch.Select(st => st.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id))));
					if (bs.FalseBranch == null)
						return string.Format(
						"if_logicalnz {0}\n" +
						"{1}\n" +
						"endif",
						bs.Flag.ToString(),
						string.Join("\n", bs.TrueBranch.Select(st => st.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id))));
					return string.Format(
						"if_logicalnz {0}\n" +
						"{1}\n" +
						"else\n" +
						"{2}\n" +
						"endif",
						bs.Flag.ToString(),
						string.Join("\n", bs.TrueBranch.Select(st => st.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id))),
						string.Join("\n", bs.FalseBranch.Select(st => st.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id))));
				}
				case StatementTypes.INFLOOP:
					return string.Format(
						"whileloop\n" +
						"{0}\n" +
						"endloop",
						string.Join("\n", (src as InfiniteLoopStatement).Body.Select(st => st.ToAMDIL(
						literals, functions, raw_uav_id, arena_uav_id))));
				case StatementTypes.BREAK:
					return "break";
				default:
					throw new NotSupportedException(src.StatementType.ToString());
			}
		}

		private static string ToAMDIL(this Subprogram subprogram,
			LiteralPool literals,
			Dictionary<string, int> functions,
			int raw_uav_id,
			int arena_uav_id)
		{
			IList<BasicBlock> bblist = subprogram.GetBasicBlocks();

			StringBuilder code = new StringBuilder();

			code.AppendLine("func " + functions[subprogram.Name]);

			try
			{
				foreach (TreeStatement st in subprogram.BuildAST())
					code.AppendLine(st.ToAMDIL(literals, functions, raw_uav_id, arena_uav_id));
			}
			catch (IrreducibleCFGException e)
			{
				Console.WriteLine("WARNING: " + subprogram.Name + " control flow will be handled with poor unsafe method");
				code.AppendLine("mov r0.w, " + literals[bblist.IndexOf(subprogram.CFGRoot)] + ".w");
				code.AppendLine("whileloop");
				for (int i = 0; i < bblist.Count; i++)
				{
					code.AppendLine("ieq r2.x, r0.w, " + literals[i] + ".w");
					code.AppendLine("if_logicalnz r2.x");
					foreach (BasicBlockInstruction bbi in bblist[i].Code)
						code.AppendLine(bbi.ToAMDIL(literals, functions, raw_uav_id, arena_uav_id));
					code.AppendLine(bblist[i].Trailer.ToAMDIL(literals, bblist));
					code.AppendLine("endif");
					code.AppendLine();
				}
				code.AppendLine("endloop");
			}

			code.AppendLine("ret");
			code.AppendLine("endfunc");

			return code.ToString();
		}

		public static string ToAMDIL(this Program program, string kernelName, int setup_id, int func_id, int raw_uav_id, int arena_uav_id)
		{
			// Perform special registers naming.
			
			foreach (SpecialRegister sr in program.SpecialRegisters)
			{
				switch (sr.Value)
				{
				case PredefinedValues.ThreadIdxX:
					sr.Name = "vTidInGrp.x";
					break;
				case PredefinedValues.ThreadIdxY:
					sr.Name = "vTidInGrp.y";
					break;
				case PredefinedValues.ThreadIdxZ:
					sr.Name = "vTidInGrp.z";
					break;
				case PredefinedValues.BlockDimX:
					sr.Name = "r0.x";
					break;
				case PredefinedValues.BlockDimY:
					sr.Name = "r0.y";
					break;
				case PredefinedValues.BlockDimZ:
					sr.Name = "r0.z";
					break;
				case PredefinedValues.BlockIdxX:
					sr.Name = "vThreadGrpId.x";
					break;
				case PredefinedValues.BlockIdxY:
					sr.Name = "vThreadGrpId.y";
					break;
				case PredefinedValues.BlockIdxZ:
					sr.Name = "vThreadGrpId.z";
					break;
				case PredefinedValues.GridDimX:
					sr.Name = "r1.x";
					break;
				case PredefinedValues.GridDimY:
					sr.Name = "r1.y";
					break;
				case PredefinedValues.GridDimZ:
					sr.Name = "r1.z";
					break;
				default:
					throw new ArgumentException(string.Format("There is no \"{0}\" special register in AMDIL.", 
						sr.Value), "program");
				}
			}

			Kernel kernel = program.Kernels.Single(krn => krn.Name == kernelName);
			IEnumerable<Subprogram> kernelCallees = Program.GetCallsRec(kernel);

			// Perform register counting and naming.

			IEnumerable<VirtualRegister> regs = 
				from sp in kernelCallees.Add<Subprogram>(kernel)
				from op in sp.LocalVariables.Concat(sp.FormalParameters)
				select op;

			int regnum = 4;

			foreach (VirtualRegister vr in regs)
			{
				vr.Name = "r" + (regnum++).ToString();
				switch (vr.DataType.SizeOf())
				{
				case 1:
				case 2:
				case 4:
					vr.Name = vr.Name + ".x";
					break;
				case 8:
					vr.Name = vr.Name + ".xy";
					break;
				case 16:
					vr.Name = vr.Name + ".xyzw";
					break;
				default:
					throw new NotSupportedException(vr.DataType.Format());
				}
			}

			Console.WriteLine("INFO: There are {0} virtual registers in AMDIL code for {1}", regnum, kernel.Name);

			int base_id = Math.Max(setup_id, func_id) + 1;
			Dictionary<string, int> functions = kernelCallees.Select(
				(sp, idx) => new KeyValuePair<string, int>(sp.Name, base_id + idx)).ToDictionary(
				kvp => kvp.Key,kvp => kvp.Value);
			functions.Add(kernel.Name, func_id);

			LiteralPool literals = new LiteralPool();

			StringBuilder amdil = new StringBuilder();

			amdil.AppendLine("il_cs_2_0");
			amdil.AppendLine("LITERALS");
			amdil.AppendLine("call " + setup_id);
			amdil.AppendLine("endmain");
			amdil.AppendLine();

			amdil.AppendLine("func " + setup_id);
			amdil.AppendLine("dcl_max_thread_per_group 1024");
			if (kernel.FormalParameters.Any(fp => fp.StateSpace == StateSpaces.SHARED))
				amdil.AppendLine("dcl_lds_id(1) 32768");
			amdil.AppendLine("dcl_raw_uav_id(" + raw_uav_id + ")");
			amdil.AppendLine("dcl_arena_uav_id(" + arena_uav_id + ")");
			amdil.AppendLine("dcl_cb cb0[2]");
			amdil.AppendLine("dcl_cb cb1[" + kernel.FormalParameters.Count + "]");
			for (int i = 0; i < kernel.FormalParameters.Count(fp => fp.StateSpace == StateSpaces.CONSTANT); i++)
				amdil.AppendLine("dcl_cb cb" + (i + 2) + "[4096]");
			amdil.AppendLine("mov r0, cb0[1].xyz0");
			amdil.AppendLine("mov r1, cb0[0].xyz0");

			for (int i = 0; i < kernel.FormalParameters.Count; i++)
			{
				FormalParameter fp = kernel.FormalParameters[i];

				switch (fp.StateSpace)
				{
				case StateSpaces.REG:
					switch (Type.GetTypeCode(fp.UnderlyingType))
					{
					case TypeCode.Boolean:
					case TypeCode.SByte:
					case TypeCode.Byte:
						amdil.AppendFormat(
							"mov {0}, cb1[{1}].x\n" +
							"ishl {0}, {0}, {2}\n" +
							"ishr {0}, {0}, {2}\n",
							fp.ToString(),
							i,
							literals[(int)24] + ".x"
						);
						break;
					case TypeCode.Int16:
					case TypeCode.UInt16:
						amdil.AppendFormat(
							"mov {0}, cb1[{1}].x\n" +
							"ishl {0}, {0}, {2}\n" +
							"ishr {0}, {0}, {2}\n",
							fp.ToString(),
							i,
							literals[(int)16] + ".x"
						);
						break;
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Single:
						amdil.AppendFormat(
							"mov {0}, cb1[{1}].x\n",
							fp.ToString(),
							i
						);
						break;
					case TypeCode.Int64:
					case TypeCode.UInt64:
					case TypeCode.Double:
						amdil.AppendFormat(
							"mov {0}, cb1[{1}].xy\n",
							fp.ToString(),
							i
						);
						break;
					default:
						throw new NotSupportedException(fp.UnderlyingType.Format());
					}
					break;
				case StateSpaces.GLOBAL:
				case StateSpaces.SHARED:
					amdil.AppendFormat(
						"mov {0}, cb1[{1}].x\n",
						fp.ToString(),
						i
					);
					break;
				case StateSpaces.CONSTANT:
				default:
					amdil.AppendFormat(
						"ixor {0}, {0}\n",
						fp.ToString()
					);
					break;
				}
			}

			amdil.AppendLine("call " + func_id);
			amdil.AppendLine("ret");
			amdil.AppendLine("endfunc");
			amdil.AppendLine();

			amdil.AppendLine(kernel.ToAMDIL(literals, functions, raw_uav_id, arena_uav_id));
			foreach (Subprogram sp in kernelCallees)
				amdil.AppendLine(sp.ToAMDIL(literals, functions, raw_uav_id, arena_uav_id));

			amdil.AppendLine("end");
			amdil.AppendLine();

			StringBuilder dcl_literal = new StringBuilder();

			for (int i = 0; i < literals.Pool.Count; i++)
			{
				ValueType val = literals.Pool [i];
				string x, y, z, w;
				if (val is float)
				{
					x = y = z = w = string.Format(
						"{0:X8}",
						BitConverter.ToInt32(BitConverter.GetBytes((float)val), 0)
					);
				}
				else if (val is double)
				{
					y = string.Format("{0:X16}", BitConverter.DoubleToInt64Bits((double)val));
					x = z = y.Substring(8);
					y = w = y.Substring(0, 8);
				}
				else if (val is int)
				{
					x = y = z = w = string.Format("{0:X8}", (int)val);
				}
				else
					throw new NotSupportedException(val.GetType().ToString());
				
				dcl_literal.AppendFormat(
					"dcl_literal l{0}, 0x{1}, 0x{2}, 0x{3}, 0x{4}\n",
					i,
					x,
					y,
					z,
					w
				);
			}

			return amdil.ToString().Replace("LITERALS", dcl_literal.ToString());
		}

		private static string SwizzleMask(string access)
		{
			return access.Substring(access.IndexOf('.'));
		}

		private static string LowerPart(string access)
		{
			return access.Substring(0, access.IndexOf('.') + 2);
		}
	}
}

