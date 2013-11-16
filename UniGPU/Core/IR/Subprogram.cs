//
// Subprogram.cs
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
	public class Subprogram
	{
		private void Enumerate(BasicBlock bb, Action<BasicBlock> handler, IList<BasicBlock> visited)
		{
			if (bb != null && !visited.Contains(bb))
			{
				handler(bb);
				visited.Add(bb);
				Enumerate(bb.Successor, handler, visited);
				Enumerate(bb.Target, handler, visited);
			}
		}
		
		public void BasicBlockPass(Action<BasicBlock> handler)
		{
			Enumerate(CFGRoot, handler, new List<BasicBlock>());
		}
		
		public IList<BasicBlock> GetBasicBlocks()
		{
			List<BasicBlock> visited = new List<BasicBlock>();
			Enumerate(CFGRoot, (bb => { }), visited);
			return visited.AsReadOnly();
		}
		
		public IEnumerable<Subprogram> GetCalls()
		{
			return (from bb in GetBasicBlocks()
				from bbi in bb.Code
				where bbi.OpCode == IROpCodes.CALL
				let call = (bbi as CALLInstruction).Target
				select call).Distinct();
		}
		
		public IEnumerable<VirtualRegister> LocalVariables
		{
			get
			{
				return (from bb in GetBasicBlocks()
					from gi in bb.Code.Add<GenericInstruction>(bb.Trailer)
					from operand in gi.Arguments
					where (operand is VirtualRegister) && !(operand is FormalParameter)
					select operand as VirtualRegister).Distinct();
			}
		}
		
		public IList<FormalParameter> FormalParameters { get; private set; }
		
		public BasicBlock CFGRoot { get; set; }
		
		public string Name { get; set; }
		
		public Subprogram(IList<FormalParameter> formalParameters)
		{
			if (formalParameters == null)
				throw new ArgumentNullException("formalParameters");
			else
				FormalParameters = formalParameters;
		}
	}
	
	public class Kernel : Subprogram
	{
		public Kernel(IList<FormalParameter> formalParameters) :
			base(formalParameters)
		{
			if (formalParameters.Count(fp => fp.PassingStyle != PassingStyles.VAL) > 0)
				throw new ArgumentException("Kernel parameters can be passed only by value", "formalParameters");
		}
	}
}

