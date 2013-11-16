//
// InstructionSelector.cs
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
using System.Reflection;
using Mono.Reflection;
using UniGPU.Core.IR;
using UniGPU.Core.Utils;

namespace UniGPU.Core.CIL
{
	// This class performs conversion of stack-based CLR execution model to register-based IR one for
	// separate CIL basic block and particular stack state.
	internal class InstructionSelector
	{
		/*
		 * Result type mappings for integral binary operations
		 * ADD, SUB, MUL, DIV, REM, AND, OR, XOR
		-------------------------------------------------------------------------
		|Operands| byte  | ushort | uint  | ulong | sbyte | short | int  | long |
		-------------------------------------------------------------------------
		| byte   | int   | int    | uint  | ulong | int   | int   | int  | long |
		-------------------------------------------------------------------------
		| ushort | int   | int    | uint  | ulong | int   | int   | int  | long |
		-------------------------------------------------------------------------
		| uint   | uint  | uint   | uint  | ulong | long  | long  | long | long |
		-------------------------------------------------------------------------
		| ulong  | ulong | ulong  | ulong | ulong |   X   |   X   |  X   |   X  |
		-------------------------------------------------------------------------
		| sbyte  | int   | int    | long  |   X   | int   | int   | int  | long |
		-------------------------------------------------------------------------
		| short  | int   | int    | long  |   X   | int   | int   | int  | long |
		-------------------------------------------------------------------------
		| int    | int   | int    | long  |   X   | int   | int   | int  | long |
		-------------------------------------------------------------------------
		| long   | long  | long   | long  |   X   | long  | long  | long | long |
		-------------------------------------------------------------------------
		*/
		
		public static readonly Dictionary<Type, Dictionary<Type, Type>> ArithmeticMapping =
			new Dictionary<Type, Dictionary<Type, Type>>
		{
			/*{typeof(byte),
				new Dictionary<Type, Type>
				{
					{typeof(byte), typeof(int)},
					{typeof(ushort), typeof(int)},
					{typeof(uint), typeof(uint)},
					{typeof(ulong), typeof(ulong)},
					{typeof(sbyte), typeof(int)},
					{typeof(short), typeof(int)},
					{typeof(int), typeof(int)},
					{typeof(long), typeof(long)}
				}
			},
			{typeof(ushort),
				new Dictionary<Type, Type>
				{
					{typeof(byte), typeof(int)},
					{typeof(ushort), typeof(int)},
					{typeof(uint), typeof(uint)},
					{typeof(ulong), typeof(ulong)},
					{typeof(sbyte), typeof(int)},
					{typeof(short), typeof(int)},
					{typeof(int), typeof(int)},
					{typeof(long), typeof(long)}
				}
			},*/
			{typeof(uint),
				new Dictionary<Type, Type>
				{
					//{typeof(byte), typeof(uint)},
					//{typeof(ushort), typeof(uint)},
					{typeof(uint), typeof(uint)},
					{typeof(ulong), typeof(ulong)},
					//{typeof(sbyte), typeof(long)},
					//{typeof(short), typeof(long)},
					{typeof(int), typeof(long)},
					{typeof(long), typeof(long)}
				}
			},
			{typeof(ulong),
				new Dictionary<Type, Type>
				{
					//{typeof(byte), typeof(ulong)},
					//{typeof(ushort), typeof(ulong)},
					{typeof(uint), typeof(ulong)},
					{typeof(ulong), typeof(ulong)},
					// Workaround for extraordinary compiler
					//{typeof(sbyte), typeof(long)},
					//{typeof(short), typeof(long)},
					{typeof(int), typeof(long)},
					{typeof(long), typeof(long)}
				}
			},
			/*{typeof(sbyte),
				new Dictionary<Type, Type>
				{
					{typeof(byte), typeof(int)},
					{typeof(ushort), typeof(int)},
					{typeof(uint), typeof(long)},
					{typeof(sbyte), typeof(int)},
					{typeof(short), typeof(int)},
					{typeof(int), typeof(int)},
					{typeof(long), typeof(long)},
					// Workaround for extraordinary compiler
					{typeof(ulong), typeof(long)}
				}
			},
			{typeof(short),
				new Dictionary<Type, Type>
				{
					{typeof(byte), typeof(int)},
					{typeof(ushort), typeof(int)},
					{typeof(uint), typeof(long)},
					{typeof(sbyte), typeof(int)},
					{typeof(short), typeof(int)},
					{typeof(int), typeof(int)},
					{typeof(long), typeof(long)},
					// Workaround for extraordinary compiler
					{typeof(ulong), typeof(long)}
				}
			},*/
			{typeof(int),
				new Dictionary<Type, Type>
				{
					//{typeof(byte), typeof(int)},
					//{typeof(ushort), typeof(int)},
					{typeof(uint), typeof(long)},
					//{typeof(sbyte), typeof(int)},
					//{typeof(short), typeof(int)},
					{typeof(int), typeof(int)},
					{typeof(long), typeof(long)},
					// Workaround for extraordinary compiler
					{typeof(ulong), typeof(long)}
				}
			},
			{typeof(long),
				new Dictionary<Type, Type>
				{
					//{typeof(byte), typeof(long)},
					//{typeof(ushort), typeof(long)},
					{typeof(uint), typeof(long)},
					//{typeof(sbyte), typeof(long)},
					//{typeof(short), typeof(long)},
					{typeof(int), typeof(long)},
					{typeof(long), typeof(long)},
					// Workaround for extraordinary compiler
					{typeof(ulong), typeof(long)}
				}
			},
			{typeof(float),
				new Dictionary<Type, Type>
				{
					{typeof(float), typeof(float)}
				}
			},
			{typeof(double),
				new Dictionary<Type, Type>
				{
					{typeof(double), typeof(double)}
				}
			}
		};
		public static readonly Dictionary<Type, Type> UpconvertMapping =
			new Dictionary<Type, Type>
		{
			{typeof(bool), typeof(int)},
			{typeof(byte), typeof(int)},
			{typeof(ushort), typeof(int)},
			{typeof(uint), typeof(uint)},
			{typeof(ulong), typeof(ulong)},
			{typeof(sbyte), typeof(int)},
			{typeof(short), typeof(int)},
			{typeof(int), typeof(int)},
			{typeof(long), typeof(long)},
			{typeof(float), typeof(float)},
			{typeof(double), typeof(double)}
		};
		public static readonly Dictionary<Type, Type> NegMapping =
			new Dictionary<Type, Type>
		{
			//{typeof(byte), typeof(int)},
			//{typeof(ushort), typeof(int)},
			{typeof(uint), typeof(long)},
			//{typeof(sbyte), typeof(int)},
			//{typeof(short), typeof(int)},
			{typeof(int), typeof(int)},
			{typeof(long), typeof(long)},
			{typeof(float), typeof(float)},
			{typeof(double), typeof(double)},
		};
		public static readonly Dictionary<Type, Type> NotMapping =
			new Dictionary<Type, Type>
		{
			//{typeof(byte), typeof(int)},
			//{typeof(ushort), typeof(int)},
			{typeof(uint), typeof(uint)},
			{typeof(ulong), typeof(ulong)},
			//{typeof(sbyte), typeof(int)},
			//{typeof(short), typeof(int)},
			{typeof(int), typeof(int)},
			{typeof(long), typeof(long)}
		};
		
		public class DynamicRegister : VirtualRegister
		{
			public bool Live { get; set; }
			
			public DynamicRegister(Type type, StateSpaces stsp = StateSpaces.REG) :
				base(type, stsp)
			{
				Live = true;
			}
		}
		
		private Func<Type, StateSpaces, DynamicRegister> regalloc;
		
		private DynamicRegister AllocateRegister(Type type, StateSpaces stsp = StateSpaces.REG)
		{
			return regalloc(type, stsp);
		}
		
		private void ReleaseOperand(GenericOperand operand)
		{
			if (operand is DynamicRegister)
				(operand as DynamicRegister).Live = false;
		}
		
		// Formal parameters induced by evaluation stack elements pushed in control flow
		// predessors of current code path and used in this basic block (actual parameters).
		private List<GenericOperand> arguments = new List<GenericOperand>();

		// IR representation of basic block CIL code.
		private List<BasicBlockInstruction> code = new List<BasicBlockInstruction>();

		// Trailing control flow instruction.
		public ControlFlowInstruction Trailer { get; private set; }
		
		public IList<GenericOperand> Arguments { get { return arguments.AsReadOnly(); } }

		public IList<BasicBlockInstruction> Code { get { return code.AsReadOnly(); } }
		
		public void AddInitialization(GenericOperand actual, VirtualRegister formal)
		{
			code.Add(new UnaryOperation(IROpCodes.MOV, formal, actual));
			ReleaseOperand(actual);
		}
		
		private Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack;
				
		private GenericOperand RetrieveOperand()
		{
			GenericOperand operand = stack.Peek().Item1.Item1;
			bool byref = stack.Peek().Item1.Item2;
			InstructionSelector parent = stack.Peek().Item2;
			stack.Pop();
			
			if (parent != this)
			{
				if (byref)
				{
					arguments.Add(operand);
					return operand;
				}
				else
				{
					VirtualRegister argument = operand as VirtualRegister;
					argument = new VirtualRegister(argument.UnderlyingType, argument.StateSpace);
					parent.AddInitialization(operand, argument);
					arguments.Add(argument);
					return argument;
				}
			}
			else
				return operand;
		}
		
		private void PushByVal(VirtualRegister operand)
		{
			stack.Push(new Tuple<Tuple<GenericOperand, bool>, InstructionSelector>(
				new Tuple<GenericOperand, bool>(operand, false), this));
		}
		
		private void PushByRef(GenericOperand operand)
		{
			stack.Push(new Tuple<Tuple<GenericOperand, bool>, InstructionSelector>(
				new Tuple<GenericOperand, bool>(operand, true), this));
		}
		
		private VirtualRegister MapToRegister(GenericOperand operand)
		{
			if (operand is VirtualRegister)
				return operand as VirtualRegister;
			else
			{
				VirtualRegister reg = AllocateRegister(operand.DataType);
				code.Add(new UnaryOperation(IROpCodes.MOV, reg, operand));
				return reg;
			}
		}
		
		private GenericOperand ConvertOperand(GenericOperand operand, Type type)
		{
			if (operand.DataType != UpconvertMapping[type])
			{
				VirtualRegister target = AllocateRegister(UpconvertMapping[type]);
				code.Add(new UnaryOperation(IROpCodes.CVT, target, operand));
				ReleaseOperand(operand);
				return target;
			}
			return operand;
		}
		
		private VirtualRegister CalculateAddress(VirtualRegister array, GenericOperand index)
		{
			VirtualRegister address = AllocateRegister(array.UnderlyingType, array.StateSpace);
			index = ConvertOperand(index, array.DataType);
			int elemsize = array.UnderlyingType.SizeOf();
			code.Add(elemsize == 1 ?
				new BinaryOperation(IROpCodes.ADD, address, index, array) as BasicBlockInstruction :
				new MADOperation(address, index, new ImmediateValue(
					(ValueType)Convert.ChangeType(elemsize, array.DataType)), array) as BasicBlockInstruction);
			ReleaseOperand(array);
			ReleaseOperand(index);
			return address;
		}
		
		public InstructionSelector(IList<Instruction> cilbbcode,
			Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack,
			IList<FormalParameter> parameters,
			IList<VirtualRegister> variables,
			bool result,
			IList<SpecialRegister> srpool,
			IDictionary<MethodInfo, Subprogram> spmap,
			Func<Type, StateSpaces, DynamicRegister> regalloc)
		{
			this.stack = stack;
			this.regalloc = regalloc;
			
			bool skipnext = false;
			
			foreach (Instruction cilinstr in cilbbcode)
			{
				if (skipnext)
				{
					skipnext = false;
					continue;
				}
				
				if (Trailer != null)
					throw new ArgumentException("No instruction is permitted after trailing control flow one", "cilbbcode");
				
				GenericOperand left = null;
				GenericOperand right = null;
				GenericOperand extra = null;
				VirtualRegister regleft = null;
				VirtualRegister regright = null;
				VirtualRegister target = null;
				FormalParameter arg = null;
				VirtualRegister loc = null;

				CILOpCodes cilopcode = (CILOpCodes)cilinstr.OpCode.Value;

				bool isArithmetic = cilopcode >= CILOpCodes.Add && cilopcode <= CILOpCodes.Xor;
				bool isCompare = cilopcode >= CILOpCodes.Ceq && cilopcode <= CILOpCodes.Clt_Un;
				bool isCompBranch = cilopcode >= CILOpCodes.Beq_S && cilopcode <= CILOpCodes.Blt_Un_S ||
					cilopcode >= CILOpCodes.Beq && cilopcode <= CILOpCodes.Blt_Un;

				if (isArithmetic || isCompare || isCompBranch)
				{
					right = RetrieveOperand();
					left = RetrieveOperand();
					if (cilopcode == CILOpCodes.Mul &&
						cilinstr.Next != null &&
						(CILOpCodes)cilinstr.Next.OpCode.Value == CILOpCodes.Add)
						extra = RetrieveOperand();
					Type targetType = ArithmeticMapping[left.DataType][right.DataType];
						
					right = ConvertOperand(right, targetType);
					left = ConvertOperand(left, targetType);
					if (extra != null)
						extra = ConvertOperand(extra, targetType);
					
					ReleaseOperand(right);
					ReleaseOperand(left);
					if (extra != null)
						ReleaseOperand(extra);
					
					target = AllocateRegister(isArithmetic ? targetType : typeof(int));
					
					if (!isCompBranch)
						PushByVal(target);
				}

				switch (cilopcode)
				{
				// Method arguments loading
				case CILOpCodes.Ldarg_0:
				case CILOpCodes.Ldarg_1:
				case CILOpCodes.Ldarg_2:
				case CILOpCodes.Ldarg_3:
				case CILOpCodes.Ldarg_S:
				case CILOpCodes.Ldarg:
					arg = parameters[(cilinstr.Operand == null) ? (cilopcode - CILOpCodes.Ldarg_0) :
						(cilinstr.Operand as ParameterInfo).Position];
					if (arg.PassingStyle == PassingStyles.VAL)
					{
						target = AllocateRegister(arg.StateSpace != StateSpaces.REG ? arg.UnderlyingType : arg.DataType, arg.StateSpace);
						code.Add(new UnaryOperation(IROpCodes.MOV, target, arg));
						PushByVal(target);
					}
					else
						PushByRef(arg);
					break;
				case CILOpCodes.Ldarga_S:
				case CILOpCodes.Ldarga:
					arg = parameters[(cilinstr.Operand as ParameterInfo).Position];
					PushByRef(arg);
					break;
				// Local variables loading
				case CILOpCodes.Ldloc_0:
				case CILOpCodes.Ldloc_1:
				case CILOpCodes.Ldloc_2:
				case CILOpCodes.Ldloc_3:
				case CILOpCodes.Ldloc_S:
				case CILOpCodes.Ldloc:
					loc = variables[(cilinstr.Operand == null) ? (cilopcode - CILOpCodes.Ldloc_0) :
						(cilinstr.Operand as LocalVariableInfo).LocalIndex];
					target = AllocateRegister(loc.UnderlyingType, loc.StateSpace);
					code.Add(new UnaryOperation(IROpCodes.MOV, target, loc));
					PushByVal(target);
					break;
				case CILOpCodes.Ldloca_S:
				case CILOpCodes.Ldloca:
					loc = variables[(cilinstr.Operand as LocalVariableInfo).LocalIndex];
					PushByRef(loc);
					break;
				// Method arguments storing
				case CILOpCodes.Starg_S:
				case CILOpCodes.Starg:
					arg = parameters[(cilinstr.Operand as ParameterInfo).Position];
					right = RetrieveOperand();
					if (arg.DataType != right.DataType)
						code.Add(new UnaryOperation(IROpCodes.CVT, arg, right));
					else
						code.Add(new UnaryOperation(IROpCodes.MOV, arg, right));
					ReleaseOperand(right);
					break;
				// Local variables storing
				case CILOpCodes.Stloc_0:
				case CILOpCodes.Stloc_1:
				case CILOpCodes.Stloc_2:
				case CILOpCodes.Stloc_3:
				case CILOpCodes.Stloc_S:
				case CILOpCodes.Stloc:
					loc = variables[(cilinstr.Operand == null) ? (cilopcode - CILOpCodes.Stloc_0) :
						(cilinstr.Operand as LocalVariableInfo).LocalIndex];
					right = RetrieveOperand();
					if (loc.DataType != right.DataType)
						code.Add(new UnaryOperation(IROpCodes.CVT, loc, right));
					else
						code.Add(new UnaryOperation(IROpCodes.MOV, loc, right));
					ReleaseOperand(right);
					break;
				// Constants storing
				case CILOpCodes.Ldnull:
					PushByRef(new ImmediateValue((int)0));
					break;
				case CILOpCodes.Ldc_I4_M1:
				case CILOpCodes.Ldc_I4_0:
				case CILOpCodes.Ldc_I4_1:
				case CILOpCodes.Ldc_I4_2:
				case CILOpCodes.Ldc_I4_3:
				case CILOpCodes.Ldc_I4_4:
				case CILOpCodes.Ldc_I4_5:
				case CILOpCodes.Ldc_I4_6:
				case CILOpCodes.Ldc_I4_7:
				case CILOpCodes.Ldc_I4_8:
				case CILOpCodes.Ldc_I4_S:
				case CILOpCodes.Ldc_I4:
				case CILOpCodes.Ldc_I8:
				case CILOpCodes.Ldc_R4:
				case CILOpCodes.Ldc_R8:
					PushByRef(new ImmediateValue((cilinstr.Operand == null) ?
						(int)(cilopcode - CILOpCodes.Ldc_I4_0) : (ValueType)Convert.ChangeType(cilinstr.Operand,
						UpconvertMapping[cilinstr.Operand.GetType()])));
					break;
				// Array elements loading
				case CILOpCodes.Ldelem_I1:
				case CILOpCodes.Ldelem_U1:
				case CILOpCodes.Ldelem_I2:
				case CILOpCodes.Ldelem_U2:
				case CILOpCodes.Ldelem_I4:
				case CILOpCodes.Ldelem_U4:
				case CILOpCodes.Ldelem_I8:
				case CILOpCodes.Ldelem_R4:
				case CILOpCodes.Ldelem_R8:
				case CILOpCodes.Ldelem:
					regright = MapToRegister(RetrieveOperand()); // index
					regleft = RetrieveOperand() as VirtualRegister; // array
					VirtualRegister address = CalculateAddress(regleft, regright);
					target = AllocateRegister(UpconvertMapping[regleft.UnderlyingType]);
					code.Add(new LDInstruction(target, address));
					ReleaseOperand(address);
					PushByVal(target);
					break;
				case CILOpCodes.Ldelema:
					regright = MapToRegister(RetrieveOperand()); // index
					regleft = RetrieveOperand() as VirtualRegister; // array
					PushByVal(CalculateAddress(regleft, regright));
					break;
				// Array elements storing
				case CILOpCodes.Stelem_I1:
				case CILOpCodes.Stelem_I2:
				case CILOpCodes.Stelem_I4:
				case CILOpCodes.Stelem_I8:
				case CILOpCodes.Stelem_R4:
				case CILOpCodes.Stelem_R8:
				case CILOpCodes.Stelem:
					GenericOperand val = RetrieveOperand(); // value
					regright = MapToRegister(RetrieveOperand()); // index
					regleft = RetrieveOperand() as VirtualRegister; // array
					IEnumerable<Type> compatible = regleft.UnderlyingType.CompatibleTypes().Where(type => type.SizeOf() >= sizeof(int));
					target = !compatible.Contains(val.DataType) ?
						ConvertOperand(val, compatible.First()) as VirtualRegister : MapToRegister(val);
					address = CalculateAddress(regleft, regright);
					code.Add(new STInstruction(address, target));
					ReleaseOperand(address);
					ReleaseOperand(target);
					break;
				// Indirect loads
				case CILOpCodes.Ldobj:
				case CILOpCodes.Ldind_I1:
				case CILOpCodes.Ldind_U1:
				case CILOpCodes.Ldind_I2:
				case CILOpCodes.Ldind_U2:
				case CILOpCodes.Ldind_I4:
				case CILOpCodes.Ldind_U4:
				case CILOpCodes.Ldind_I8:
				case CILOpCodes.Ldind_R4:
				case CILOpCodes.Ldind_R8:
					// pointer (induced by ldelema) / variable (induced by ldloca) / parameter (induced by ldarga or pass-by-ref ldarg)
					regright = RetrieveOperand() as VirtualRegister;
					if (regright.StateSpace == StateSpaces.REG)
					{
						target = AllocateRegister(regright.DataType);
						code.Add(new UnaryOperation(IROpCodes.MOV, target, regright));
					}
					else
					{
						target = AllocateRegister(UpconvertMapping[regright.UnderlyingType]);
						code.Add(new LDInstruction(target, regright));
					}
					ReleaseOperand(regright);
					PushByVal(target);
					break;
				case CILOpCodes.Ldind_Ref:
					regright = RetrieveOperand() as VirtualRegister; // pointer (induced by pass-by-ref ldarg)
					target = AllocateRegister(regright.UnderlyingType, regright.StateSpace);
					code.Add(new UnaryOperation(IROpCodes.MOV, target, regright));
					ReleaseOperand(regright);
					PushByVal(target);
					break;
				// Indirect stores
				case CILOpCodes.Stobj:
				case CILOpCodes.Stind_I1:
				case CILOpCodes.Stind_I2:
				case CILOpCodes.Stind_I4:
				case CILOpCodes.Stind_I8:
				case CILOpCodes.Stind_R4:
				case CILOpCodes.Stind_R8:
					right = RetrieveOperand(); // value
					// pointer (induced by ldelema) / variable (induced by ldloca) / parameter (induced by ldarga or pass-by-ref ldarg)
					regleft = RetrieveOperand() as VirtualRegister;
					if (regleft.StateSpace == StateSpaces.REG)
					{
						if (regleft.DataType != right.DataType)
							code.Add(new UnaryOperation(IROpCodes.CVT, regleft, right));
						else
							code.Add(new UnaryOperation(IROpCodes.MOV, regleft, right));
						ReleaseOperand(right);
					}
					else
					{
						compatible = regleft.UnderlyingType.CompatibleTypes().Where(type => type.SizeOf() >= sizeof(int));
						regright = !compatible.Contains(right.DataType) ?
							ConvertOperand(right, compatible.First()) as VirtualRegister : MapToRegister(right);
						code.Add(new STInstruction(regleft, regright));
						ReleaseOperand(regright);
					}
					ReleaseOperand(regleft);
					break;
				case CILOpCodes.Stind_Ref:
					regright = RetrieveOperand() as VirtualRegister; // source pointer (induced by ldarg or pass-by-ref ldarg)
					regleft = RetrieveOperand() as VirtualRegister; // target pointer (induced by pass-by-ref ldarg)
					code.Add(new UnaryOperation(IROpCodes.MOV, regleft, regright));
					ReleaseOperand(regleft);
					ReleaseOperand(regright);
					break;
				// Comparison branches
				case CILOpCodes.Beq:
				case CILOpCodes.Beq_S:
					code.Add(new BinaryOperation(IROpCodes.EQ, target, left, right));
					goto case CILOpCodes.Brtrue;
				case CILOpCodes.Bne_Un:
				case CILOpCodes.Bne_Un_S:
					code.Add(new BinaryOperation(IROpCodes.NE, target, left, right));
					goto case CILOpCodes.Brtrue;
				case CILOpCodes.Bge_S:
				case CILOpCodes.Bge_Un_S:
				case CILOpCodes.Bge:
				case CILOpCodes.Bge_Un:
					code.Add(new BinaryOperation(IROpCodes.GE, target, left, right));
					goto case CILOpCodes.Brtrue;
				case CILOpCodes.Bgt_S:
				case CILOpCodes.Bgt_Un_S:
				case CILOpCodes.Bgt:
				case CILOpCodes.Bgt_Un:
					code.Add(new BinaryOperation(IROpCodes.GT, target, left, right));
					goto case CILOpCodes.Brtrue;
				case CILOpCodes.Ble_S:
				case CILOpCodes.Ble_Un_S:
				case CILOpCodes.Ble:
				case CILOpCodes.Ble_Un:
					code.Add(new BinaryOperation(IROpCodes.LE, target, left, right));
					goto case CILOpCodes.Brtrue;
				case CILOpCodes.Blt_S:
				case CILOpCodes.Blt_Un_S:
				case CILOpCodes.Blt:
				case CILOpCodes.Blt_Un:
					code.Add(new BinaryOperation(IROpCodes.LT, target, left, right));
					goto case CILOpCodes.Brtrue;
				// Basic branches (the last instructions in basic block)
				case CILOpCodes.Br_S:
				case CILOpCodes.Br:
					Trailer = new JMPInstruction();
					break;
				case CILOpCodes.Brfalse_S:
				case CILOpCodes.Brfalse:
					regleft = MapToRegister(ConvertOperand(RetrieveOperand(), typeof(int)));
					Trailer = new JFInstruction(regleft);
					ReleaseOperand(regleft);
					break;
				case CILOpCodes.Brtrue_S:
				case CILOpCodes.Brtrue:
					regleft = isCompBranch ? target : MapToRegister(ConvertOperand(RetrieveOperand(), typeof(int)));
					Trailer = new JTInstruction(regleft);
					ReleaseOperand(regleft);
					break;
				// Call stuff.
				case CILOpCodes.Call:
					MethodInfo method = cilinstr.Operand as MethodInfo;
					PropertyInfo property = method.DeclaringType.GetProperty(method.Name.Replace("get_", ""));
					if (property != null)
					{
						// Handle predefined value access.
						PredefinedValueAttribute pva = Attribute.GetCustomAttribute(property, 
							typeof(PredefinedValueAttribute)) as PredefinedValueAttribute;
						if (pva != null)
							PushByVal(MapToRegister(srpool.Single(sr => sr.Value == pva.Value)));
						else
							throw new NotSupportedException(method.DeclaringType.FullName + "." + property.Name);
					}
					else
					{
						// Handle intrinsic function call.
						IntrinsicFunctionAttribute ifa = Attribute.GetCustomAttribute(method, 
							typeof(IntrinsicFunctionAttribute)) as IntrinsicFunctionAttribute;
						if (ifa != null)
						{
							switch (ifa.OpCode)
							{
							case IROpCodes.SYNC:
								code.Add(new SYNCInstruction());
								break;
							case IROpCodes.ABS:
							case IROpCodes.SQRT:
							case IROpCodes.RSQRT:
							case IROpCodes.SIN:
							case IROpCodes.COS:
							case IROpCodes.LG2:
							case IROpCodes.EX2:
								right = ConvertOperand(RetrieveOperand(), UpconvertMapping[method.ReturnType]);
								ReleaseOperand(right);
								target = AllocateRegister(right.DataType);
								code.Add(new UnaryOperation(ifa.OpCode, target, right));
								PushByVal(target);
								break;
							case IROpCodes.MIN:
							case IROpCodes.MAX:
								right = ConvertOperand(RetrieveOperand(), UpconvertMapping[method.ReturnType]);
								left = ConvertOperand(RetrieveOperand(), right.DataType);
								ReleaseOperand(right);
								ReleaseOperand(left);
								target = AllocateRegister(right.DataType);
								code.Add(new BinaryOperation(ifa.OpCode, target, left, right));
								PushByVal(target);
								break;
							default:
								throw new NotSupportedException(ifa.OpCode.ToString());
							}
						}
						else
						{
							// Handle subprogram call.
							Subprogram sp;
							spmap.TryGetValue(method, out sp);
							if (sp == null)
								sp = method.BuildIR(srpool, spmap);
							
							List<GenericOperand> actualParameters = new List<GenericOperand>();
							List<Tuple<VirtualRegister, VirtualRegister>> wrappedRefElements = 
								new List<Tuple<VirtualRegister, VirtualRegister>>();
							
							if (method.ReturnType != typeof(void))
							{
								arg = sp.FormalParameters.Last();
								target = AllocateRegister(arg.StateSpace != StateSpaces.REG ? 
									arg.UnderlyingType : arg.DataType, arg.StateSpace);
								PushByVal(target);
							}
							
							foreach (FormalParameter fp in sp.FormalParameters.Reverse())
							{
								GenericOperand ap = RetrieveOperand();
								if  (fp.StateSpace == StateSpaces.REG)
								{
									VirtualRegister regap = ap as VirtualRegister;
									if (regap != null && regap.StateSpace != StateSpaces.REG)
									{
										// Passing array element by reference.
										
										// Create wrapping register.
										regright = AllocateRegister(fp.DataType);
										// Load array element value to wrapping register.
										if (fp.PassingStyle == PassingStyles.REF)
											code.Add(new LDInstruction(regright, regap));
										// Remember address-to-register mapping.
										wrappedRefElements.Add(new Tuple<VirtualRegister, VirtualRegister>(regap, regright));
										
										// Replace original actual parameter with wrapping register.
										ap = regright;
									}
									else if (fp.PassingStyle == PassingStyles.VAL)
									{
										// Perform implicit parameter conversion.
										ap = ConvertOperand(ap, fp.DataType);
									}
								}
								actualParameters.Insert(0, ap);
							}
							
							code.Add(new CALLInstruction(sp, actualParameters));
							
							// Update values of wrapped array elements.
							code.AddRange(wrappedRefElements.Select(we => new STInstruction(we.Item1, we.Item2)));
							
							foreach (VirtualRegister ap in actualParameters)
								ReleaseOperand(ap);
							foreach (Tuple<VirtualRegister, VirtualRegister> we in wrappedRefElements)
								ReleaseOperand(we.Item1);
							
							if (method.ReturnType != typeof(void))
							{
								PushByVal(target);
								(target as DynamicRegister).Live = true;
							}
						}
					}
					break;
				// Arithmetic operations
				case CILOpCodes.Add:
					code.Add(new BinaryOperation(IROpCodes.ADD, target, left, right));
					break;
				case CILOpCodes.Sub:
					code.Add(new BinaryOperation(IROpCodes.SUB, target, left, right));
					break;
				case CILOpCodes.Mul:
					if (extra != null)
					{
						code.Add(new MADOperation(target, left, right, extra));
						skipnext = true;
					}
					else
						code.Add(new BinaryOperation(IROpCodes.MUL, target, left, right));
					break;
				case CILOpCodes.Div:
				case CILOpCodes.Div_Un:
					code.Add(new BinaryOperation(IROpCodes.DIV, target, left, right));
					break;
				case CILOpCodes.Rem:
				case CILOpCodes.Rem_Un:
					code.Add(new BinaryOperation(IROpCodes.REM, target, left, right));
					break;
				case CILOpCodes.And:
					code.Add(new BinaryOperation(IROpCodes.AND, target, left, right));
					break;
				case CILOpCodes.Or:
					code.Add(new BinaryOperation(IROpCodes.OR, target, left, right));
					break;
				case CILOpCodes.Xor:
					code.Add(new BinaryOperation(IROpCodes.XOR, target, left, right));
					break;
				case CILOpCodes.Neg:
					right = RetrieveOperand();
					right = ConvertOperand(right, NegMapping[right.DataType]);
					ReleaseOperand(right);
					target = AllocateRegister(right.DataType);
					code.Add(new UnaryOperation(IROpCodes.NEG, target, right));
					PushByVal(target);
					break;
				case CILOpCodes.Not:
					right = RetrieveOperand();
					right = ConvertOperand(right, NotMapping[right.DataType]);
					ReleaseOperand(right);
					target = AllocateRegister(right.DataType);
					code.Add(new UnaryOperation(IROpCodes.NOT, target, right));
					PushByVal(target);
					break;
				// Convert operations
				case CILOpCodes.Conv_I1:
				case CILOpCodes.Conv_Ovf_I1:
				case CILOpCodes.Conv_Ovf_I1_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(sbyte))));
					break;
				case CILOpCodes.Conv_I2:
				case CILOpCodes.Conv_Ovf_I2:
				case CILOpCodes.Conv_Ovf_I2_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(short))));
					break;
				case CILOpCodes.Conv_I:
				case CILOpCodes.Conv_Ovf_I:
				case CILOpCodes.Conv_Ovf_I_Un:
				case CILOpCodes.Conv_U:
				case CILOpCodes.Conv_Ovf_U:
				case CILOpCodes.Conv_Ovf_U_Un:
				case CILOpCodes.Conv_I4:
				case CILOpCodes.Conv_Ovf_I4:
				case CILOpCodes.Conv_Ovf_I4_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(int))));
					break;
				case CILOpCodes.Conv_I8:
				case CILOpCodes.Conv_Ovf_I8:
				case CILOpCodes.Conv_Ovf_I8_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(long))));
					break;
				case CILOpCodes.Conv_U1:
				case CILOpCodes.Conv_Ovf_U1:
				case CILOpCodes.Conv_Ovf_U1_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(byte))));
					break;
				case CILOpCodes.Conv_U2:
				case CILOpCodes.Conv_Ovf_U2:
				case CILOpCodes.Conv_Ovf_U2_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(ushort))));
					break;
				case CILOpCodes.Conv_U4:
				case CILOpCodes.Conv_Ovf_U4:
				case CILOpCodes.Conv_Ovf_U4_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(uint))));
					break;
				case CILOpCodes.Conv_U8:
				case CILOpCodes.Conv_Ovf_U8:
				case CILOpCodes.Conv_Ovf_U8_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(ulong))));
					break;
				case CILOpCodes.Conv_R4:
				case CILOpCodes.Conv_R_Un:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(float))));
					break;
				case CILOpCodes.Conv_R8:
					PushByVal(MapToRegister(ConvertOperand(RetrieveOperand(), typeof(double))));
					break;
				// Compare operations
				case CILOpCodes.Ceq:
					code.Add(new BinaryOperation(IROpCodes.EQ, target, left, right));
					break;
				case CILOpCodes.Cgt:
				case CILOpCodes.Cgt_Un:
					code.Add(new BinaryOperation(IROpCodes.GT, target, left, right));
					break;
				case CILOpCodes.Clt:
				case CILOpCodes.Clt_Un:
					code.Add(new BinaryOperation(IROpCodes.LT, target, left, right));
					break;
				// Stack manipulation
				case CILOpCodes.Pop:
					ReleaseOperand(RetrieveOperand());
					break;
				case CILOpCodes.Dup:
					bool byref = stack.Peek().Item1.Item2;
					right = RetrieveOperand();
					if (byref)
					{
						PushByRef(right);
						PushByRef(right);
					}
					else
					{
						regright = right as VirtualRegister;
						PushByVal(regright);
						target = AllocateRegister(regright.UnderlyingType, regright.StateSpace);
						code.Add(new UnaryOperation(IROpCodes.MOV, target, regright));
						PushByVal(target);
					}
					break;
				case CILOpCodes.Ret:
					if (result)
					{
						arg = parameters.Last();
						right = RetrieveOperand();
						if (arg.DataType != right.DataType)
							code.Add(new UnaryOperation(IROpCodes.CVT, arg, right));
						else
							code.Add(new UnaryOperation(IROpCodes.MOV, arg, right));
						ReleaseOperand(right);
					}
					Trailer = new RETInstruction();
					break;
				case CILOpCodes.Nop:
					break;
				default:
					throw new NotSupportedException(cilinstr.OpCode.Name);
				}

				if (isCompare)
					code.Add(new UnaryOperation(IROpCodes.NEG, target, target));
			}
			if (Trailer == null)
				Trailer = new JMPInstruction();
		}
	}
}

