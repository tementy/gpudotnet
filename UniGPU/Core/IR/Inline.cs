//
// Inline.cs
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

namespace UniGPU.Core.IR
{
	public static class Inline
	{
		public static VirtualRegister MapOperand (this VirtualRegister operand, 
			Dictionary<VirtualRegister, VirtualRegister> regmap)
		{
			return regmap[operand];
		}
		
		public static GenericOperand MapOperand (this GenericOperand operand, 
			Dictionary<VirtualRegister, VirtualRegister> regmap)
		{
			return (operand is VirtualRegister) ?
				(operand as VirtualRegister).MapOperand(regmap) : operand;
		}

		public static BasicBlockInstruction MapInstruction(this BasicBlockInstruction bbi,
			Dictionary<VirtualRegister, VirtualRegister> regmap)
		{
			switch (bbi.OpCode)
			{
			case IROpCodes.ADD:
			case IROpCodes.SUB:
			case IROpCodes.MUL:
			case IROpCodes.DIV:
			case IROpCodes.MIN:
			case IROpCodes.MAX:
			case IROpCodes.REM:
			case IROpCodes.AND:
			case IROpCodes.OR:
			case IROpCodes.XOR:
			case IROpCodes.EQ:
			case IROpCodes.NE:
			case IROpCodes.GE:
			case IROpCodes.GT:
			case IROpCodes.LE:
			case IROpCodes.LT:
				BinaryOperation bop = bbi as BinaryOperation;
				return new BinaryOperation(bop.OpCode, bop.Target.MapOperand(regmap),
					bop.LeftOperand.MapOperand(regmap), bop.RightOperand.MapOperand(regmap));
			case IROpCodes.MAD:
				MADOperation mad = bbi as MADOperation;
				return new MADOperation(mad.Target.MapOperand(regmap),
					mad.MulLeftOperand.MapOperand(regmap), mad.MulRightOperand.MapOperand(regmap),
					mad.AddOperand.MapOperand(regmap));
			case IROpCodes.NEG:
			case IROpCodes.ABS:
			case IROpCodes.NOT:
			case IROpCodes.SQRT:
			case IROpCodes.RSQRT:
			case IROpCodes.SIN:
			case IROpCodes.COS:
			case IROpCodes.LG2:
			case IROpCodes.EX2:
			case IROpCodes.MOV:
			case IROpCodes.CVT:
				UnaryOperation uop = bbi as UnaryOperation;
				return new UnaryOperation(uop.OpCode, uop.Target.MapOperand(regmap), uop.Operand.MapOperand(regmap));
			case IROpCodes.LD:
				LDInstruction load = bbi as LDInstruction;
				return new LDInstruction(load.Target.MapOperand(regmap), load.Address.MapOperand(regmap));
			case IROpCodes.ST:
				STInstruction store = bbi as STInstruction;
				return new STInstruction(store.Address.MapOperand(regmap), store.Source.MapOperand(regmap));
			case IROpCodes.CALL:
				CALLInstruction call = bbi as CALLInstruction;
				return new CALLInstruction(call.Target, call.Arguments.Select(op => op.MapOperand(regmap)).ToList());
			case IROpCodes.SYNC:
				return new SYNCInstruction();
			default:
				throw new NotSupportedException();
			}
		}
		
		private static BasicBlock InlineIR(this CALLInstruction call,
			IList<BasicBlockInstruction> preamble,
			BasicBlock backend,
			Dictionary<Subprogram, Dictionary<VirtualRegister, VirtualRegister>> inlineRegMaps)
		{
			Dictionary<VirtualRegister, VirtualRegister> inlineRegMap;
			if (!inlineRegMaps.TryGetValue(call.Target, out inlineRegMap))
			{
				inlineRegMap = call.Target.LocalVariables.Select(lv => 
					new KeyValuePair<VirtualRegister, VirtualRegister>(lv, new VirtualRegister(lv.UnderlyingType, lv.StateSpace))).
					ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				inlineRegMaps.Add(call.Target, inlineRegMap);
			}
			inlineRegMap = inlineRegMap.Concat(call.Target.FormalParameters.Zip(call.Arguments,
				(formal, actual) => new KeyValuePair<VirtualRegister, VirtualRegister>(formal,
				(actual is VirtualRegister) ? (actual as VirtualRegister) : 
				(formal.StateSpace != StateSpaces.REG) ? new VirtualRegister(formal.UnderlyingType, formal.StateSpace) :
				new VirtualRegister(formal.DataType)))).
				ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			Dictionary<BasicBlock, BasicBlock> cfgmap = call.Target.GetBasicBlocks().Select(bb => 
			{
				VirtualRegister flag = (bb.Trailer is JumpIfInstruction) ?
					(bb.Trailer as JumpIfInstruction).Flag.MapOperand(inlineRegMap) : null;
				
				ControlFlowInstruction trailer;
				
				switch (bb.Trailer.OpCode)
				{
				case IROpCodes.RET:
					trailer = new JMPInstruction() { Target = backend };
					break;
				case IROpCodes.JMP:
					trailer = new JMPInstruction();
					break;
				case IROpCodes.JT:
					trailer = new JTInstruction(flag);
					break;
				case IROpCodes.JF:
					trailer = new JFInstruction(flag);
					break;
				default:
					throw new NotSupportedException();
				}
				
				return new KeyValuePair<BasicBlock, BasicBlock>(bb,
					new BasicBlock(bb.Code.Select(bbi => bbi.MapInstruction(inlineRegMap)).ToList(), trailer));
				
			}).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			foreach (KeyValuePair<BasicBlock, BasicBlock> bbmap in cfgmap)
			{
				if (bbmap.Key.Successor != null)
					bbmap.Value.Successor = cfgmap[bbmap.Key.Successor];
				if (bbmap.Key.Target != null)
					bbmap.Value.Target = cfgmap[bbmap.Key.Target];
			}
			
			BasicBlock root = cfgmap[call.Target.CFGRoot];
			return new BasicBlock(preamble.Concat(root.Code).ToList(), root.Trailer);
		}
		
		private static BasicBlock InlineIR(this IList<BasicBlockInstruction> code,
			ControlFlowInstruction trailer,
			Func<Subprogram, bool> permitInline,
			Dictionary<VirtualRegister, VirtualRegister> copyRegMap,
			Dictionary<Subprogram, Dictionary<VirtualRegister, VirtualRegister>> inlineRegMaps,
			IList<Subprogram> subprograms,
			out BasicBlock backend)
		{
			List<BasicBlockInstruction> preamble = new List<BasicBlockInstruction>();
			int i = 0;
			while (i < code.Count && !(code[i].OpCode == IROpCodes.CALL &&
				permitInline(subprograms.Single(sp => sp.Name == (code[i] as CALLInstruction).Target.Name))))
				preamble.Add(code[i++].MapInstruction(copyRegMap));
			if (i < code.Count)
			{
				CALLInstruction call = code[i++] as CALLInstruction;
				call = new CALLInstruction(subprograms.Single(sp => sp.Name == call.Target.Name),
					call.Arguments.Select(op => op.MapOperand(copyRegMap)).ToList());
				
				List<BasicBlockInstruction> tail = new List<BasicBlockInstruction>();
				while (i < code.Count)
					tail.Add(code[i++]);					
				
				return call.InlineIR(preamble, tail.InlineIR(trailer, permitInline, copyRegMap, 
					inlineRegMaps, subprograms, out backend), inlineRegMaps);
			}
			else
			{
				backend = new BasicBlock(preamble, trailer);
				return backend;
			}
		}
		
		private static BasicBlock InlineIR(this BasicBlock bb,
			Func<Subprogram, bool> permitInline,
			Dictionary<VirtualRegister, VirtualRegister> copyRegMap,
			Dictionary<Subprogram, Dictionary<VirtualRegister, VirtualRegister>> inlineRegMaps,
			IList<Subprogram> subprograms,
			Dictionary<BasicBlock, BasicBlock> basicBlockMap)
		{
			BasicBlock result;
			
			if (!basicBlockMap.TryGetValue(bb, out result))
			{
				VirtualRegister flag = (bb.Trailer is JumpIfInstruction) ?
					(bb.Trailer as JumpIfInstruction).Flag.MapOperand(copyRegMap) : null;
					
				ControlFlowInstruction trailer;
				switch (bb.Trailer.OpCode)
				{
				case IROpCodes.RET:
					trailer = new RETInstruction();
					break;
				case IROpCodes.JMP:
					trailer = new JMPInstruction();
					break;
				case IROpCodes.JT:
					trailer = new JTInstruction(flag);
					break;
				case IROpCodes.JF:
					trailer = new JFInstruction(flag);
					break;
				default:
					throw new NotSupportedException();
				}
				
				BasicBlock backend;
				result = bb.Code.InlineIR(trailer, permitInline, copyRegMap, inlineRegMaps, subprograms, out backend);
				basicBlockMap.Add(bb, result);
				
				if (bb.Successor != null)
					backend.Successor = bb.Successor.InlineIR(permitInline, copyRegMap, inlineRegMaps, subprograms, basicBlockMap);
				if (bb.Target != null)
					backend.Target = bb.Target.InlineIR(permitInline, copyRegMap, inlineRegMaps, subprograms, basicBlockMap);
			}
			
			return result;
		}
		
		private static Subprogram InlineIR(this Subprogram sp, Func<Subprogram, bool> permitInline,
			IList<Subprogram> subprograms)
		{
			List<FormalParameter> formalParameters = sp.FormalParameters.Select(fp => fp.Clone()).ToList();
			
			Dictionary<VirtualRegister, VirtualRegister> copyRegMap = sp.FormalParameters.Zip(formalParameters, 
				(preimage, image) => new KeyValuePair<VirtualRegister, VirtualRegister>(preimage, image)).Concat(
				sp.LocalVariables.Select(lv => new KeyValuePair<VirtualRegister, VirtualRegister>(lv, 
				new VirtualRegister(lv.UnderlyingType, lv.StateSpace)))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
						
			Subprogram result = sp is Kernel ? new Kernel(formalParameters) : new Subprogram(formalParameters);
			
			result.Name = sp.Name;
			result.CFGRoot = sp.CFGRoot.InlineIR(permitInline, copyRegMap,
				new Dictionary<Subprogram, Dictionary<VirtualRegister, VirtualRegister>>(),	subprograms,
				new Dictionary<BasicBlock, BasicBlock>());
			
			return result;
		}
		
		public static Program InlineIR(this Program prg)
		{
			IList<Subprogram> subprograms = prg.GetSubprograms();
			IEnumerable<Subprogram> terminals = subprograms.Where(sp => !(sp is Kernel) && (sp.GetCalls().Count() == 0));
			while (terminals.Count() > 0)
			{
				subprograms = subprograms.Except(terminals).Select(sp => 
					sp.InlineIR(callee => terminals.Contains(callee), subprograms)).ToList().AsReadOnly();
				terminals = subprograms.Where(sp => !(sp is Kernel) && (sp.GetCalls().Count() == 0));
			}
			return new Program(subprograms.Where(sp => sp is Kernel).Select(sp => sp as Kernel).ToList().AsReadOnly(),
				prg.SpecialRegisters);
		}
	}
}

