//
// BasicBlockInstruction.cs
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
using System.Collections.Generic;
using UniGPU.Core.Utils;

namespace UniGPU.Core.IR
{
	public abstract class BasicBlockInstruction : GenericInstruction
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand>().AsReadOnly();
			}
		}
	}

	public abstract class Operation : BasicBlockInstruction
	{
		private IROpCodes opcode;
		
		public override IROpCodes OpCode { get { return opcode; } }
		
		public VirtualRegister Target { get; private set; }

		protected Operation(IROpCodes opcode, VirtualRegister target)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			else
				Target = target;
			
			this.opcode = opcode;
		}
	}

	public sealed class BinaryOperation : Operation
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Target, LeftOperand, RightOperand }.AsReadOnly();
			}
		}
		
		public GenericOperand LeftOperand { get; private set; }

		public GenericOperand RightOperand { get; private set; }

		public BinaryOperation(IROpCodes opcode, VirtualRegister target, GenericOperand left, GenericOperand right) :
			base(opcode, target)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			else
				LeftOperand = left;

			if (right == null)
				throw new ArgumentNullException("right");
			else
				RightOperand = right;
			
			switch (opcode)
			{
			case IROpCodes.ADD:
			case IROpCodes.SUB:
			case IROpCodes.MUL:
			case IROpCodes.DIV:
			case IROpCodes.MIN:
			case IROpCodes.MAX:
				Util.CheckArgumentType(Util.NumericTypes, target.DataType, "target");
				Util.CheckArgumentType(target.DataType, left.DataType, "left");
				Util.CheckArgumentType(target.DataType, right.DataType, "right");
				break;
			case IROpCodes.REM:
				Util.CheckArgumentType(Util.IntegralTypes, target.DataType, "target");
				Util.CheckArgumentType(target.DataType, left.DataType, "left");
				Util.CheckArgumentType(target.DataType, right.DataType, "right");
				break;
			case IROpCodes.AND:
			case IROpCodes.OR:
			case IROpCodes.XOR:
				Util.CheckArgumentType(Util.IntegralTypes, target.DataType, "target");
				Util.CheckArgumentType(target.DataType, left.DataType, "left");
				Util.CheckArgumentType(target.DataType, right.DataType, "right");
				break;
			case IROpCodes.EQ:
			case IROpCodes.NE:
			case IROpCodes.GE:
			case IROpCodes.GT:
			case IROpCodes.LE:
			case IROpCodes.LT:
				Util.CheckArgumentType(typeof(int), target.DataType, "target");
				Util.CheckArgumentType(Util.NumericTypes, left.DataType, "left");
				Util.CheckArgumentType(left.DataType, right.DataType, "right");
				break;
			default:
				throw new InvalidOperationException();
			}
		}
	}

	public sealed class UnaryOperation : Operation
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Target, Operand }.AsReadOnly();
			}
		}
		
		public GenericOperand Operand { get; private set; }

		public UnaryOperation(IROpCodes opcode, VirtualRegister target, GenericOperand operand) :
			base(opcode, target)
		{
			if (operand == null)
				throw new ArgumentNullException("operand");
			else
				Operand = operand;
			
			switch (opcode)
			{
			case IROpCodes.NEG:
			case IROpCodes.ABS:
				Util.CheckArgumentType(Util.SIntTypes.Concat(Util.RealTypes), target.DataType, "target");
				goto case IROpCodes.MOV;
			case IROpCodes.NOT:
				Util.CheckArgumentType(Util.IntegralTypes, target.DataType, "target");
				goto case IROpCodes.MOV;
			case IROpCodes.SQRT:
			case IROpCodes.RSQRT:
				Util.CheckArgumentType(Util.RealTypes, target.DataType, "target");
				goto case IROpCodes.MOV;
			case IROpCodes.SIN:
			case IROpCodes.COS:
			case IROpCodes.LG2:
			case IROpCodes.EX2:
				Util.CheckArgumentType(typeof(float), target.DataType, "target");
				goto case IROpCodes.MOV;
			case IROpCodes.MOV:
				Util.CheckArgumentType(target.DataType, operand.DataType, "operand");
				break;
			case IROpCodes.CVT:
				if (!(Util.NumericTypes.Contains(operand.DataType) && Util.NumericTypes.Contains(target.DataType)) ||
					target.DataType == operand.DataType)
					throw new InvalidCastException(string.Format("There is no conversion from {0} to {1}",
						operand.DataType.Format(), target.DataType.Format()));
				break;
			default:
				throw new InvalidOperationException();
			}
		}
	}
	
	public class MADOperation : Operation
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Target, MulLeftOperand, MulRightOperand, AddOperand }.AsReadOnly();
			}
		}
		
		public GenericOperand MulLeftOperand { get; private set; }
		
		public GenericOperand MulRightOperand { get; private set; }

		public GenericOperand AddOperand { get; private set; }

		public MADOperation(VirtualRegister target, GenericOperand mleft, GenericOperand mright, GenericOperand ad) :
			base(IROpCodes.MAD, target)
		{
			if (mleft == null)
				throw new ArgumentNullException("mleft");
			else
				MulLeftOperand = mleft;

			if (mright == null)
				throw new ArgumentNullException("mright");
			else
				MulRightOperand = mright;
			
			if (ad == null)
				throw new ArgumentNullException("addop");
			else
				AddOperand = ad;
			
			Util.CheckArgumentType(Util.NumericTypes, target.DataType, "target");
			Util.CheckArgumentType(target.DataType, mleft.DataType, "mleft");
			Util.CheckArgumentType(target.DataType, mright.DataType, "mright");
			Util.CheckArgumentType(target.DataType, ad.DataType, "ad");
		}
	}
	
	public abstract class MemoryAccessInstruction : BasicBlockInstruction
	{
		public VirtualRegister Address { get; private set; }

		protected MemoryAccessInstruction(VirtualRegister address)
		{
			if (address == null)
				throw new ArgumentNullException("address");
			else if (address.StateSpace == StateSpaces.REG)
				throw new ArgumentException("Pointer is required", "address");
			else
				Address = address;
		}
	}
	
	public sealed class LDInstruction : MemoryAccessInstruction
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Target, Address }.AsReadOnly();
			}
		}

		public override IROpCodes OpCode { get { return IROpCodes.LD; } }
		
		public VirtualRegister Target { get; private set; }
		
		public LDInstruction(VirtualRegister target, VirtualRegister address) :
			base(address)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			Util.CheckArgumentType(address.UnderlyingType.CompatibleTypes(), target.DataType, "target");
			Target = target;
		}
	}
	
	public sealed class STInstruction : MemoryAccessInstruction
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Address, Source }.AsReadOnly();
			}
		}
		
		public override IROpCodes OpCode { get { return IROpCodes.ST; } }
		
		public VirtualRegister Source { get; private set; }
		
		public STInstruction(VirtualRegister address, VirtualRegister source) :
			base(address)
		{
			if (address.StateSpace == StateSpaces.CONSTANT)
				throw new ArgumentException("Writeable state space is required", "address");
			
			if (source == null)
				throw new ArgumentNullException("value");
			Util.CheckArgumentType(address.UnderlyingType.CompatibleTypes(), source.DataType, "target");
			Source = source;
		}
	}
	
	public sealed class SYNCInstruction : BasicBlockInstruction
	{
		public override IROpCodes OpCode { get { return IROpCodes.SYNC; } }
	}
	
	public sealed class CALLInstruction : BasicBlockInstruction
	{
		private IList<GenericOperand> arguments;
		
		public override IROpCodes OpCode { get { return IROpCodes.CALL; } }
		
		public override IList<GenericOperand> Arguments { get { return arguments; } }
		
		public Subprogram Target { get; private set; }
		
		public CALLInstruction(Subprogram target, IList<GenericOperand> arguments)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			else if (target is Kernel)
				throw new NotSupportedException("Kernels can not be called from device code");
			else
				Target = target;
			
			if (arguments == null)
				throw new ArgumentNullException("operands");
			else if (arguments.Count != target.FormalParameters.Count)
				throw new ArgumentException(string.Format("{0} has {1} formal parameters but {2} actual parameters are provided",
					target.Name, target.FormalParameters.Count, arguments.Count));
			else
			{
				for (int idx = 0; idx < arguments.Count; idx++)
				{
					if (arguments[idx] == null)
						throw new ArgumentException(string.Format("Argument {0} is null", idx), "operands");
					
					FormalParameter fp = target.FormalParameters[idx];
					
					if (arguments[idx] is VirtualRegister)
					{
						VirtualRegister vr = arguments[idx] as VirtualRegister;
						if (vr.StateSpace != fp.StateSpace)
							throw new ArgumentException(string.Format("Argument {0} is incompatible with {1} state space", idx,
								fp.StateSpace.ToString()), "operands");
						if (vr.StateSpace != StateSpaces.REG)
						{
							if (vr.UnderlyingType != fp.UnderlyingType)
								throw new ArgumentException(string.Format("Argument {0} has incompatible underlying type", idx), "operands");
						}
						else
						{
							if (vr.DataType != fp.DataType)
								throw new ArgumentException(string.Format("Argument {0} has incompatible data type", idx), "operands");
						}
					}
					else if (arguments[idx] is ImmediateValue)
					{
						ImmediateValue iv = arguments[idx] as ImmediateValue;
						if (fp.StateSpace != StateSpaces.REG)
							throw new ArgumentException(string.Format("Argument {0} is incompatible with {1} state space", idx,
								fp.StateSpace.ToString()), "operands");
						if (fp.PassingStyle != PassingStyles.VAL)
							throw new ArgumentException(string.Format("Argument {0} can be passed only by value", idx), "operands");
						if (iv.DataType != fp.DataType)
							throw new ArgumentException(string.Format("Argument {0} has incompatible data type", idx), "operands");
					}
				}
			}
			
			this.arguments = arguments;
		}
	}
}

