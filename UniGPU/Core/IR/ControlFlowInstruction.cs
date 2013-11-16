//
// ControlFlowInstruction.cs
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
using UniGPU.Core.Utils;

namespace UniGPU.Core.IR
{
	public abstract class ControlFlowInstruction : GenericInstruction
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand>().AsReadOnly();
			}
		}
	}

	public sealed class RETInstruction : ControlFlowInstruction
	{
		public override IROpCodes OpCode { get { return IROpCodes.RET; } }
	}
	
	public abstract class JumpInstruction : ControlFlowInstruction
	{
		public BasicBlock Target { get; set; }
	}
	
	public sealed class JMPInstruction : JumpInstruction
	{
		public override IROpCodes OpCode { get { return IROpCodes.JMP; } }
	}
	
	public abstract class JumpIfInstruction : JumpInstruction
	{
		public override IList<GenericOperand> Arguments
		{
			get
			{
				return new List<GenericOperand> { Flag }.AsReadOnly();
			}
		}
		
		public BasicBlock Next { get; set; }
		
		public VirtualRegister Flag { get; private set; }
		
		protected JumpIfInstruction(VirtualRegister flag)
		{
			if (flag == null)
				throw new ArgumentNullException("flag");
			
			Util.CheckArgumentType(typeof(int), flag.DataType, "flag");
			
			Flag = flag;
		}
	}
	
	public sealed class JTInstruction : JumpIfInstruction
	{
		public override IROpCodes OpCode { get { return IROpCodes.JT; } }
		
		public JTInstruction(VirtualRegister flag) :
			base(flag)
		{
		}
	}
	
	public sealed class JFInstruction : JumpIfInstruction
	{
		public override IROpCodes OpCode { get { return IROpCodes.JF; } }
		
		public JFInstruction(VirtualRegister flag) :
			base(flag)
		{
		}
	}
}

