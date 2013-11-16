//
// ProgramBuilder.cs
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
using UniGPU.Core.IR;

namespace UniGPU.Core.CIL
{
	public static class ProgramBuilder
	{
		public static Program BuildIR(this object obj)
		{
			return obj.GetType().BuildIR();
		}
		
		public static Program BuildIR(this Type type)
		{
			IList<SpecialRegister> srpool = SpecialRegister.CreatePool();
			Dictionary<MethodInfo, Subprogram> spmap = new Dictionary<MethodInfo, Subprogram>();
			Program irprog = new Program(type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(method =>
				Attribute.GetCustomAttribute(method, typeof(KernelAttribute)) != null).Select(kernel => 
				kernel.BuildIR(srpool, spmap) as Kernel).ToList(), srpool);
			return IRBuildOptions.AutoInline ? irprog.InlineIR() : irprog;
		}

		public static Program BuildIR(this MethodInfo method)
		{
			IList<SpecialRegister> srpool = SpecialRegister.CreatePool();
			Dictionary<MethodInfo, Subprogram> spmap = new Dictionary<MethodInfo, Subprogram>();
			return new Program(new Kernel[] { method.BuildIR(srpool, spmap, true) as Kernel }, srpool);
		}
	}

	public static partial class IRBuildOptions
	{
		public static bool AutoInline = false;
	}
}

