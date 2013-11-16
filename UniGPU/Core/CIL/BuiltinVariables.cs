//
// BuiltinVariables.cs
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
using UniGPU.Core.IR;

namespace UniGPU.Core.CIL
{
	public static class ThreadIdx
	{
		[PredefinedValue(PredefinedValues.ThreadIdxX)]
		public static uint X { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.ThreadIdxY)]
		public static uint Y { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.ThreadIdxZ)]
		public static uint Z { get { throw new NotImplementedException(); } }
	}
	
	public static class BlockDim
	{
		[PredefinedValue(PredefinedValues.BlockDimX)]
		public static uint X { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.BlockDimY)]
		public static uint Y { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.BlockDimZ)]
		public static uint Z { get { throw new NotImplementedException(); } }
	}
	
	public static class BlockIdx
	{
		[PredefinedValue(PredefinedValues.BlockIdxX)]
		public static uint X { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.BlockIdxY)]
		public static uint Y { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.BlockIdxZ)]
		public static uint Z { get { throw new NotImplementedException(); } }
	}
	
	public static class GridDim
	{
		[PredefinedValue(PredefinedValues.GridDimX)]
		public static uint X { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.GridDimY)]
		public static uint Y { get { throw new NotImplementedException(); } }

		[PredefinedValue(PredefinedValues.GridDimZ)]
		public static uint Z { get { throw new NotImplementedException(); } }
	}
}

