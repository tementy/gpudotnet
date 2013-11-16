//
// BasicBlock.cs
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
using System.Collections.Generic;

namespace UniGPU.Core.IR
{
	public class BasicBlock
	{
		public string Label { get; set; }
		
		// Unconditional branch target or block after conditional branch.
		public BasicBlock Successor
		{
			get
			{
				switch (Trailer.OpCode)
				{
				case IROpCodes.JMP:
					return (Trailer as JMPInstruction).Target;
				case IROpCodes.JT:
				case IROpCodes.JF:
					return (Trailer as JumpIfInstruction).Next;
				default:
					return null;
				}
			}
			set
			{
				switch (Trailer.OpCode)
				{
				case IROpCodes.JMP:
					(Trailer as JMPInstruction).Target = value;
					break;
				case IROpCodes.JT:
				case IROpCodes.JF:
					(Trailer as JumpIfInstruction).Next = value;
					break;
				default:
					throw new InvalidOperationException();
				}
			}
		}
		
		// Conditional branch target.
		public BasicBlock Target
		{
			get
			{
				return (Trailer is JumpIfInstruction) ? (Trailer as JumpIfInstruction).Target : null;
			}
			set
			{
				if (Trailer is JumpIfInstruction)
					(Trailer as JumpIfInstruction).Target = value;
				else
					throw new InvalidOperationException();
			}
		}
		
		// Block code.
		public IList<BasicBlockInstruction> Code { get; private set; }
		
		// Trailing branch.
		public ControlFlowInstruction Trailer { get; private set; }
		
		public BasicBlock(IList<BasicBlockInstruction> code, ControlFlowInstruction trailer)
		{
			Code = code;
			Trailer = trailer;
		}
	}
}

