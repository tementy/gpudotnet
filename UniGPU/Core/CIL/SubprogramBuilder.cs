//
// SubprogramBuilder.cs
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
using System.Runtime.InteropServices;
using System.Reflection;
using Mono.Reflection;
using UniGPU.Core.IR;

namespace UniGPU.Core.CIL
{
	internal static class SubprogramBuilder
	{
		private class CILBB
		{
			public CILBB successor;
			public CILBB target;
			public List<Instruction> code = new List<Instruction>();
		}
		
		private static CILBB BuildCILCFG(Instruction start)
		{
			if (start == null)
				throw new ArgumentNullException("start");
			
			List<CILBB> cilbblist = new List<CILBB>();
			
			Instruction instr = start;
			
			// Determine leading instructions starting each basic block:
			// The first instruction is a leader.
			// The target of a conditional or an unconditional goto/jump instruction is a leader.
			// The instruction that immediately follows a conditional or an unconditional goto/jump instruction is a leader.
			
			List<Instruction> leaders = new List<Instruction>();
			leaders.Add(instr);
			bool afterTrailer = false;
			while (instr != null)
			{
				if (afterTrailer)
				{
					if (!leaders.Contains(instr))
						leaders.Add(instr);
					afterTrailer = false;
				}
				
				CILOpCodes opcode = (CILOpCodes)instr.OpCode.Value;
				bool isBranch = opcode > CILOpCodes.Ret && opcode < CILOpCodes.Switch;
				if (isBranch)
				{
					if (!leaders.Contains(instr.Operand as Instruction))
						leaders.Add(instr.Operand as Instruction);
				}
				afterTrailer = isBranch || opcode == CILOpCodes.Ret;
				 
				instr = instr.Next;
			}
			
			// Starting from a leader, the set of all following instructions until and not including the next leader is 
			// the basic block corresponding to the starting leader.
			
			List<Instruction> trailers = new List<Instruction>();
			foreach (Instruction leader in leaders)
			{
				CILBB cilbb = new CILBB();
				cilbb.code.Add(leader);
				Instruction cur = leader.Next;
				while (cur != null && !leaders.Contains(cur))
				{
					cilbb.code.Add(cur);
					cur = cur.Next;
				}
				trailers.Add(cilbb.code.Last());
				cilbblist.Add(cilbb);
			}

			// Perform linking of CIL basic blocks.
			
			for (int i = 0; i < cilbblist.Count; i++)
			{
				Instruction trailer = trailers[i];
				CILOpCodes trailerCode = (CILOpCodes)trailer.OpCode.Value;
				bool uncondBranch = trailerCode == CILOpCodes.Br_S || trailerCode == CILOpCodes.Br;
				bool condBranch = !uncondBranch && trailerCode > CILOpCodes.Ret && trailerCode < CILOpCodes.Switch;
				if (condBranch)
				{
					cilbblist[i].target = cilbblist.Single(cilbb => cilbb.code.First().Offset == (trailer.Operand as Instruction).Offset);
					cilbblist[i].successor = cilbblist.Single(cilbb => cilbb.code.First().Offset == trailer.Next.Offset);
				}
				else if (trailerCode != CILOpCodes.Ret)
				{
					cilbblist[i].successor = uncondBranch ?
						cilbblist.Single(cilbb => cilbb.code.First().Offset == (trailer.Operand as Instruction).Offset) :
						((trailer.Next == null) ? null : cilbblist.Single(cilbb => cilbb.code.First().Offset == trailer.Next.Offset));
				}
			}
			
			return cilbblist[0];
		}
		
		private class RegisterAllocator
		{
			private List<InstructionSelector.DynamicRegister> pool = new List<InstructionSelector.DynamicRegister>();
			
			public bool FinishTest()
			{
				return pool.TrueForAll(reg => !reg.Live);
			}
			
			public InstructionSelector.DynamicRegister Allocate(Type type, StateSpaces stsp)
			{
				InstructionSelector.DynamicRegister vreg = pool.Where(
					rcr => !rcr.Live && rcr.StateSpace == stsp &&
					(rcr.StateSpace == StateSpaces.REG && rcr.DataType == type ||
					rcr.StateSpace != StateSpaces.REG && rcr.UnderlyingType == type)).FirstOrDefault();
				if (vreg == null)
				{
					vreg = new InstructionSelector.DynamicRegister(type, stsp);
					pool.Add(vreg);
				}
				else
					vreg.Live = true;
				return vreg;
			}

			public static InstructionSelector.DynamicRegister Waste(Type type, StateSpaces stsp)
			{
				return new InstructionSelector.DynamicRegister(type, stsp);
			}
		}
		
		private class CILBBImplementation
		{
			private InstructionSelector selector;
			private Tuple<GenericOperand, bool>[] initializers;
			private object[] typeContext;
			private List<Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>> stackStates = 
				new List<Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>>();
			private BasicBlock basicBlock;

			private object[] GetTypeContext(Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack)
			{
				return stack.Select(op => op.Item1.Item2 ? op.Item1.Item1 as object :
					new Tuple<Type, StateSpaces>((op.Item1.Item1 as VirtualRegister).StateSpace != StateSpaces.REG ? 
					(op.Item1.Item1 as VirtualRegister).UnderlyingType : (op.Item1.Item1 as VirtualRegister).DataType, 
					(op.Item1.Item1 as VirtualRegister).StateSpace) as object).ToArray();
			}
			
			private class TypeContextEqualityComparer : EqualityComparer<object>
			{
				public override bool Equals(object x, object y)
				{
					if (x is Tuple<Type, StateSpaces> && y is Tuple<Type, StateSpaces>)
						return x.Equals(y);
					else
						return x == y;	
				}
				
				public override int GetHashCode(object obj)
				{
					return obj.GetHashCode();
				}
			}

			public bool TypeContextEqual(Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack)
			{
				return typeContext.SequenceEqual(GetTypeContext(stack), new TypeContextEqualityComparer());
			}

			public bool Implements(Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack)
			{
				return stackStates.Find(st => st.SequenceEqual(stack)) != null;
			}

			public void Simulate(Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack)
			{
				if (TypeContextEqual(stack) && !Implements(stack))
				{
					stackStates.Add(new Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>(stack.Reverse()));
					foreach (GenericOperand arg in selector.Arguments)
					{
						GenericOperand operand = stack.Peek().Item1.Item1;
						bool byref = stack.Peek().Item1.Item2;
						InstructionSelector parent = stack.Peek().Item2;
						if (!byref)
							parent.AddInitialization(operand, arg as VirtualRegister);
						stack.Pop();
					}
					foreach (Tuple<GenericOperand, bool> init in initializers)
						stack.Push(new Tuple<Tuple<GenericOperand, bool>, InstructionSelector>(init, selector));
				}
			}
			
			public CILBBImplementation Successor { get; set; }
			
			public CILBBImplementation Target { get; set; }
			
			public BasicBlock AsBasicBlock
			{
				get
				{
					if (basicBlock == null)
					{
						basicBlock = new BasicBlock(selector.Code, selector.Trailer);
						if (Successor != null)
							basicBlock.Successor = Successor.AsBasicBlock;
						if (Target != null)
							basicBlock.Target = Target.AsBasicBlock;
					}
					return basicBlock;
				}
			}
			
			public CILBBImplementation(IList<Instruction> cilbbcode,
				Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack,
				IList<FormalParameter> parameters,
				IList<VirtualRegister> variables,
				bool result,
				IList<SpecialRegister> srpool,
				IDictionary<MethodInfo, Subprogram> spmap,
				Func<Type, StateSpaces, InstructionSelector.DynamicRegister> regalloc)
			{
				Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> temp =
					new Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>(stack.Reverse());
				selector = new InstructionSelector(cilbbcode, stack, parameters, variables, result, srpool, spmap, regalloc);
				initializers = stack.Except(temp).Reverse().Select(se => se.Item1).ToArray();
				typeContext = GetTypeContext(temp);
				stackStates.Add(temp);
			}
		}
		
		// IL basic block will have a separate IR implementation for each stack type context.
						
		private static CILBBImplementation BuildIRCFG(CILBB cilbb,
			Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>> stack,
			Dictionary<CILBB, List<CILBBImplementation>> cilbbimpls,
			IList<FormalParameter> parameters,
			IList<VirtualRegister> variables,
			bool result,
			IList<SpecialRegister> srpool,
			IDictionary<MethodInfo, Subprogram> spmap,
			Func<Type, StateSpaces, InstructionSelector.DynamicRegister> regalloc)
		{
			if (cilbb == null)
			{
				if (stack.Count > 0)
					throw new InvalidProgramException("Evaluation stack must be empty at return moment.");
				else
					return null;
			}
			else
			{
				List<CILBBImplementation> impls;
				if (!cilbbimpls.TryGetValue(cilbb, out impls))
				{
					impls = new List<CILBBImplementation>();
					cilbbimpls.Add(cilbb, impls);
				}
				
				CILBBImplementation impl = impls.Find(im => im.TypeContextEqual(stack));
				
				bool handleNext = false;
				
				if (impl != null)
				{
					if (!impl.Implements(stack))
					{
						impl.Simulate(stack);
						handleNext = true;
					}
				}
				else
				{
					impl = new CILBBImplementation(cilbb.code, stack, parameters, variables, result, srpool, spmap, regalloc);
					impls.Add(impl);
					handleNext = true;
				}
				
				if (handleNext)
				{
					impl.Successor = BuildIRCFG(cilbb.successor, 
						new Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>(stack.Reverse()),
						cilbbimpls, parameters, variables, result, srpool, spmap, regalloc);
					if (cilbb.target != null)
						impl.Target = BuildIRCFG(cilbb.target,
							new Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>(stack.Reverse()),
							cilbbimpls, parameters, variables, result, srpool, spmap, regalloc);
				}
				
				return impl;
			}
		}
		
		private class Argument : FormalParameter
		{
			public override Type DataType
			{
				get
				{
					return InstructionSelector.UpconvertMapping[UnderlyingType];
				}
			}
			
			public Argument(Type type, PassingStyles style = PassingStyles.VAL) :
				base(type, StateSpaces.REG, style)
			{
			}
			
			public override FormalParameter Clone()
			{
				return new Argument(UnderlyingType, PassingStyle) { Name = Name };
			}
		}

		public static Subprogram BuildIR(this MethodInfo method,
			IList<SpecialRegister> srpool,
			IDictionary<MethodInfo, Subprogram> spmap,
			bool anonymousKernel = false)
		{
			if (!method.IsStatic)
				throw new ArgumentException("Only static methods are supported", "method");
			
			bool kernel = anonymousKernel || Attribute.GetCustomAttribute(method, typeof(KernelAttribute)) != null;
			bool result = method.ReturnType != typeof(void);
			
			if (kernel && result)
				throw new ArgumentException("Kernels can not have return parameter", "method");

			List<FormalParameter> formalParameters = new List<FormalParameter>(method.GetParameters().Select(pi =>
			{
				PassingStyles ps = pi.ParameterType.IsByRef ? pi.IsOut ? PassingStyles.OUT : PassingStyles.REF : PassingStyles.VAL;
				Type pt = pi.ParameterType.IsByRef ? pi.ParameterType.GetElementType() : pi.ParameterType;
				FormalParameter fp = pt.IsArray ? new FormalParameter(pt.GetElementType(), anonymousKernel ? StateSpaces.GLOBAL :
					(Attribute.GetCustomAttribute(pi, typeof(StateSpaceAttribute)) as StateSpaceAttribute).StateSpace, ps) :
					new Argument(pt, ps);
				fp.Name = pi.Name;
				return fp;
			}));
			
			if (result)
			{
				FormalParameter returnParameter = method.ReturnType.IsArray ? new FormalParameter(method.ReturnType.GetElementType(),
					(Attribute.GetCustomAttribute(method.ReturnParameter, typeof(StateSpaceAttribute)) as StateSpaceAttribute).StateSpace,
					PassingStyles.OUT) :
					new Argument(method.ReturnType, PassingStyles.OUT);
				returnParameter.Name = method.Name;
				formalParameters.Add(returnParameter);
			}
			
			IList<FormalParameter> formalParametersRO = formalParameters.AsReadOnly();
			
			IList<VirtualRegister> localVariablesRO = new List<VirtualRegister>(method.GetMethodBody().LocalVariables.Select(lvi =>
				new VirtualRegister(InstructionSelector.UpconvertMapping[lvi.LocalType.IsArray ? typeof(int) : lvi.LocalType]))).AsReadOnly();
			
			Subprogram subprogram = kernel ? new Kernel(formalParametersRO) : new Subprogram(formalParametersRO);
			subprogram.Name = method.Name;
			
			spmap.Add(method, subprogram);

			Func<Type, StateSpaces, InstructionSelector.DynamicRegister> regalloc =
				IRBuildOptions.WasteRegisters ? 
				(Func<Type, StateSpaces, InstructionSelector.DynamicRegister>)RegisterAllocator.Waste :
				new RegisterAllocator().Allocate;
			
			subprogram.CFGRoot = BuildIRCFG(BuildCILCFG(method.GetInstructions().First()),
				new Stack<Tuple<Tuple<GenericOperand, bool>, InstructionSelector>>(),
				new Dictionary<CILBB, List<CILBBImplementation>>(), 
				formalParametersRO, localVariablesRO, result, srpool, spmap, regalloc).AsBasicBlock;
			
			if (regalloc.Target != null && !(regalloc.Target as RegisterAllocator).FinishTest())
				throw new Exception("There is a bug in instruction selecting phase: we have live registers");
			
			return subprogram;
		}
	}

	public static partial class IRBuildOptions
	{
		public static bool WasteRegisters = false;
	}
}

