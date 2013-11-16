//
// PTXEmitter.cs
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
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UniGPU.Core.IR;
using UniGPU.Core.Utils;

namespace UniGPU.Core
{
	public static class PTXEmitter
	{
		private static string ToPTX(this Type type)
		{
			int bitsz = type.SizeOf() * 8;
			return Util.SIntTypes.Contains(type) || type == typeof(bool) ? "s" + bitsz :
				Util.UIntTypes.Contains(type) ? "u" + bitsz :
					Util.RealTypes.Contains(type) ? "f" + bitsz : null;
		}
		
		private static string ToPTX(this ControlFlowInstruction cfi, BasicBlock following)
		{
			JumpInstruction jump = cfi as JumpInstruction;
			JumpIfInstruction jumpif = cfi as JumpIfInstruction;
			switch (cfi.OpCode)
			{
			case IROpCodes.JMP:
				return jump.Target == following ? "" : "bra.uni " + jump.Target.Label + ";\n";
			case IROpCodes.JT:
				return "setp.ne." + jumpif.Flag.DataType.ToPTX() + " %p, 0, " + jumpif.Flag + ";\n" + 
					"@%p bra.uni " + jump.Target.Label + ";\n" +
						(jumpif.Next == following ? "" : "bra.uni " + jumpif.Next.Label + ";\n");
			case IROpCodes.JF:
				return "setp.eq." + jumpif.Flag.DataType.ToPTX() + " %p, 0, " + jumpif.Flag + ";\n" + 
					"@%p bra.uni " + jump.Target.Label + ";\n" +
						(jumpif.Next == following ? "" : "bra.uni " + jumpif.Next.Label + ";\n");
			case IROpCodes.RET:
				return "ret;\n";
			default:
				throw new NotSupportedException(cfi.OpCode.ToString());
			}
		}
		
		private static string ToPTX(this BasicBlockInstruction bbi)
		{
			BinaryOperation bop = bbi as BinaryOperation;
			UnaryOperation uop = bbi as UnaryOperation;
			
			switch (bbi.OpCode)
			{
			case IROpCodes.ADD:
			case IROpCodes.SUB:
			case IROpCodes.REM:
			case IROpCodes.MIN:
			case IROpCodes.MAX:
				return bop.OpCode.ToString().ToLower() + "." + bop.Target.DataType.ToPTX() +
					" " + bop.Target + ", " + bop.LeftOperand + ", " + bop.RightOperand;
			case IROpCodes.MUL:
				return "mul." + (Util.IntegralTypes.Contains(bop.Target.DataType) ? "lo." : "") + bop.Target.DataType.ToPTX() +
					" " + bop.Target + ", " + bop.LeftOperand + ", " + bop.RightOperand;
			case IROpCodes.MAD:
				MADOperation mad = bbi as MADOperation;
				return "mad." + (Util.IntegralTypes.Contains(mad.Target.DataType) ? "lo." : "") + mad.Target.DataType.ToPTX() +
					" " + mad.Target + ", " + mad.MulLeftOperand + ", " + mad.MulRightOperand + ", " + mad.AddOperand;
			case IROpCodes.DIV:
				return "div." + (Util.IntegralTypes.Contains(bop.Target.DataType) ? bop.Target.DataType.ToPTX() :
					(bop.Target.DataType == typeof(float)) ? "approx.f32" : "rz.f64") +
					" " + bop.Target + ", " + bop.LeftOperand + ", " + bop.RightOperand;
			case IROpCodes.AND:
			case IROpCodes.OR:
			case IROpCodes.XOR:
				return bop.OpCode.ToString().ToLower() + ".b" + bop.Target.DataType.SizeOf() * 8 +
					" " + bop.Target + ", " + bop.LeftOperand + ", " + bop.RightOperand;
			case IROpCodes.EQ:
			case IROpCodes.NE:
			case IROpCodes.GE:
			case IROpCodes.GT:
			case IROpCodes.LE:
			case IROpCodes.LT:
				return "set." + bop.OpCode.ToString().ToLower() +
					"." + bop.Target.DataType.ToPTX() + "." + bop.LeftOperand.DataType.ToPTX() +
					" " + bop.Target + ", " + bop.LeftOperand + ", " + bop.RightOperand;
			case IROpCodes.ABS:
			case IROpCodes.NEG:
			case IROpCodes.MOV:
				return uop.OpCode.ToString().ToLower() + "." + uop.Target.DataType.ToPTX() +
					" " + uop.Target + ", " + uop.Operand;
			case IROpCodes.NOT:
				return "not.b" + uop.Target.DataType.SizeOf() * 8 +
					" " + uop.Target + ", " + uop.Operand;
			case IROpCodes.CVT:
				{
					string targetModif = uop.Target.DataType.ToPTX();
					string operandModif = uop.Operand.DataType.ToPTX();
					string roundModif = (uop.Target.DataType == typeof(float)) && (uop.Operand.DataType == typeof(double)) ? ".rz" :
					Util.RealTypes.Contains(uop.Target.DataType) && Util.IntegralTypes.Contains(uop.Operand.DataType) ? ".rz" :
					Util.IntegralTypes.Contains(uop.Target.DataType) && Util.RealTypes.Contains(uop.Operand.DataType) ? ".rzi" :
					"";
					return ((targetModif != operandModif) ? "cvt" + roundModif + "." + targetModif : "mov") + "." + operandModif +
					" " + uop.Target + ", " + uop.Operand;
				}
			case IROpCodes.LD:
				{
					LDInstruction op = bbi as LDInstruction;
					return "ld." + op.Address.StateSpace.ToString().ToLower() + "." + op.Address.UnderlyingType.ToPTX() +
					" " + op.Target + ", [" + op.Address + "]";
				}
			case IROpCodes.ST:
				{
					STInstruction op = bbi as STInstruction;
					return "st." + op.Address.StateSpace.ToString().ToLower() + "." + op.Address.UnderlyingType.ToPTX() +
					" " + "[" + op.Address + "], " + op.Source;
				}
			case IROpCodes.SYNC:
				return "bar.sync 0";
			case IROpCodes.SQRT:
				return "sqrt" + ((uop.Target.DataType == typeof(float)) ? ".approx.f32" : ".rz.f64") +
					" " + uop.Target + ", " + uop.Operand;
			case IROpCodes.RSQRT:
			case IROpCodes.SIN:
			case IROpCodes.COS:
			case IROpCodes.LG2:
			case IROpCodes.EX2:
				return uop.OpCode.ToString().ToLower() + ".approx.f32" +
					" " + uop.Target + ", " + uop.Operand;
			case IROpCodes.CALL:
			{
				CALLInstruction call = bbi as CALLInstruction;
				
				IEnumerable<Tuple<FormalParameter, GenericOperand>> argmap =
					call.Target.FormalParameters.Zip(call.Arguments, (fp, ap) =>
						new Tuple<FormalParameter, GenericOperand>(fp, ap));
								
				return string.Format("call.uni ({0}), {1}, ({2})",
					string.Join(", ", from tuple in argmap where tuple.Item1.PassingStyle != PassingStyles.VAL select tuple.Item2),
					call.Target.Name,
					string.Join(", ", from tuple in argmap where tuple.Item1.PassingStyle != PassingStyles.OUT select tuple.Item2));
			}
			default:
				throw new NotSupportedException(bbi.OpCode.ToString());
			}
		}

		private static string ToPTX(this BasicBlock bb, BasicBlock following)
		{
			string bbcode = string.Join("\n", bb.Code.Select(bbi => bbi.ToPTX() + ";"));
			return bb.Label + ":\n" + (bbcode != "" ? bbcode + "\n" : "") + 
				bb.Trailer.ToPTX(following);
		}

		public static string ToPTX(this Subprogram subprogram, bool declaration = false)
		{
			StringBuilder ptx = new StringBuilder();
			
			// Declare header.
			
			if (subprogram is Kernel)
			{
				ptx.AppendFormat(".entry {0}({1})", subprogram.Name, string.Join(", ", subprogram.FormalParameters.Select(fp => 
					".param." + (fp.StateSpace == StateSpaces.REG ? fp.UnderlyingType.ToPTX() : fp.DataType.ToPTX() +
					".ptr." + fp.StateSpace.ToString().ToLower() + ".align " + fp.UnderlyingType.SizeOf()) +
					" " + "%param$" + fp)));
			}
			else
			{
				ptx.AppendFormat(".func ({0}){1}({2})",
					string.Join(", ", from fp in subprogram.FormalParameters where fp.PassingStyle != PassingStyles.VAL
					select ".reg." + fp.DataType.ToPTX() + " " + fp),
					subprogram.Name,
					string.Join(", ", from fp in subprogram.FormalParameters where fp.PassingStyle != PassingStyles.OUT
					select ".reg." + fp.DataType.ToPTX() + " " + (fp.PassingStyle == PassingStyles.REF ? "%init$" : "") + fp));
			}
			
			if (declaration)
				return ptx.ToString();
			else
				ptx.AppendLine();
			
			ptx.AppendLine("{");
				
			if (subprogram is Kernel)
			{
				// Declare argument-to-register mappings.
				ptx.AppendLine("// Argument-to-register mappings.");
				ptx.AppendLine(string.Join("\n", subprogram.FormalParameters.Select(ep => ".reg." + ep.DataType.ToPTX() + " " + ep + ";")));
			}
			
			// Perform basic blocks labeling.
			IList<BasicBlock> bblist = subprogram.GetBasicBlocks();
			for (int idx = 0; idx < bblist.Count(); idx++)
				bblist.ElementAt(idx).Label = "BB" + idx;
			
			// Perform register counting and naming.
			Dictionary<Type, int> typestats = new Dictionary<Type, int>();			
			foreach (Type type in Util.NumericTypes.Add(typeof(bool)))
				typestats.Add(type, 0);
			foreach (VirtualRegister vr in subprogram.LocalVariables)
			{
				if (vr.Name == null)
					vr.Name = (typestats[vr.DataType]++).ToString();
				else
					typestats[vr.DataType] = Math.Max(typestats[vr.DataType], int.Parse(vr.Name) + 1);
				vr.Name = "%" + vr.DataType.Format() + "$" + vr.Name;
			}

			Console.WriteLine("INFO: There are {0} virtual registers in PTX code for {1}",
				typestats.Sum(kvp => kvp.Value) + subprogram.FormalParameters.Count, subprogram.Name);

			// Perform register declaration.
			ptx.AppendLine("// Internally used registers.");
			ptx.AppendLine(".reg.pred %p;");
			ptx.AppendLine(string.Join("\n", from typestat in typestats where typestat.Value > 0
				select ".reg." + typestat.Key.ToPTX() + " %" + typestat.Key.Format() + "$<" + typestat.Value.ToString() + ">;"));
			
			if (subprogram is Kernel)
			{
				// Perform kernel parameters loading.
				ptx.AppendLine("// Kernel parameters loading.");
				ptx.AppendLine(string.Join("\n", subprogram.FormalParameters.Select(fp => "ld.param." + 
					(fp.StateSpace == StateSpaces.REG ? fp.UnderlyingType.ToPTX() : fp.DataType.ToPTX()) + " " +
					fp + ", [%param$" + fp + "];")));
			}
			else
			{
				// Perform ref-parameters initialization.
				ptx.AppendLine("// Ref-parameters initialization.");
				ptx.AppendLine(string.Join("\n", from fp in subprogram.FormalParameters where fp.PassingStyle == PassingStyles.REF
					select "mov." + fp.DataType.ToPTX() + " " + fp + ", %init$" + fp + ";"));
			}
			
			// Perform basic blocks code generation.
			for (int i = 0; i < bblist.Count - 1; i++)
				ptx.AppendLine(bblist[i].ToPTX(bblist[i + 1]));
			ptx.Append(bblist[bblist.Count - 1].ToPTX(null));
			ptx.AppendLine("}");
			
			return ptx.ToString();
		}
		
		public static string ToPTX(this Program program, string target = "sm_20")
		{
			// Perform special registers naming.
			
			foreach (SpecialRegister sr in program.SpecialRegisters)
			{
				switch (sr.Value)
				{
				case PredefinedValues.ThreadIdxX:
					sr.Name = "%tid.x";
					break;
				case PredefinedValues.ThreadIdxY:
					sr.Name = "%tid.y";
					break;
				case PredefinedValues.ThreadIdxZ:
					sr.Name = "%tid.z";
					break;
				case PredefinedValues.BlockDimX:
					sr.Name = "%ntid.x";
					break;
				case PredefinedValues.BlockDimY:
					sr.Name = "%ntid.y";
					break;
				case PredefinedValues.BlockDimZ:
					sr.Name = "%ntid.z";
					break;
				case PredefinedValues.BlockIdxX:
					sr.Name = "%ctaid.x";
					break;
				case PredefinedValues.BlockIdxY:
					sr.Name = "%ctaid.y";
					break;
				case PredefinedValues.BlockIdxZ:
					sr.Name = "%ctaid.z";
					break;
				case PredefinedValues.GridDimX:
					sr.Name = "%nctaid.x";
					break;
				case PredefinedValues.GridDimY:
					sr.Name = "%nctaid.y";
					break;
				case PredefinedValues.GridDimZ:
					sr.Name = "%nctaid.z";
					break;
				default:
					throw new ArgumentException(string.Format("There is no \"{0}\" special register in PTX.", 
						sr.Value), "program");
				}
			}
			
			// Perform code generation.
			
			IEnumerable<Subprogram> subprograms = program.GetSubprograms();
			
			StringBuilder ptx = new StringBuilder();
			ptx.AppendLine(".version 3.0");
			ptx.AppendLine(".target " + target);
			ptx.AppendLine(".address_size 32");
			ptx.AppendLine("// Subprogram forward declarations.");
			ptx.AppendLine(string.Join("\n", subprograms.Where(sp => !(sp is Kernel)).Select(sp => sp.ToPTX(true) + ";")));
			ptx.AppendLine("// Subprogram definitions.");
			ptx.AppendLine(string.Join("\n", subprograms.Select(sp => sp.ToPTX())));
			
			return ptx.ToString();
		}
	}
}

