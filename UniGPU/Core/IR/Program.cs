//
// Program.cs
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
	public class Program
	{
		private static void Enumerate(Subprogram sp, Action<Subprogram> handler, IList<Subprogram> visited)
		{
			if (!visited.Contains(sp))
			{
				handler(sp);
				visited.Add(sp);
				foreach (Subprogram callee in sp.GetCalls())
					Enumerate(callee, handler, visited);
			}
		}
		
		public void SubprogramPass(Action<Subprogram> handler)
		{
			List<Subprogram> splist = new List<Subprogram>();
			foreach (Subprogram kernel in Kernels)
				Enumerate(kernel, handler, splist);
		}
		
		public IList<Subprogram> GetSubprograms()
		{
			List<Subprogram> splist = new List<Subprogram>();
			foreach (Subprogram kernel in Kernels)
				Enumerate(kernel, (x => { }), splist);
			return splist.AsReadOnly();
		}

		public static IList<Subprogram> GetCallsRec(Subprogram sp)
		{
			List<Subprogram> splist = new List<Subprogram>();
			foreach (Subprogram callee in sp.GetCalls())
				Enumerate(callee, (x => { }), splist);
			return splist.AsReadOnly();
		}
		
		public IList<Kernel> Kernels { get; private set; }
		
		public IList<SpecialRegister> SpecialRegisters { get; private set; }
		
		public Program(IList<Kernel> kernels, IList<SpecialRegister> srpool)
		{
			if (kernels == null)
				throw new ArgumentNullException("kernels");
			else
				Kernels = kernels;
			
			if (srpool == null)
				throw new ArgumentNullException("srpool");
			else
				SpecialRegisters = srpool;
		}
	}
}

