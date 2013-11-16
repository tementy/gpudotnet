//
// BuiltinFunctions.cs
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
	public class BuiltinFunctions
	{
		[IntrinsicFunction(IROpCodes.SYNC)]
		public static void SyncThreads()
		{
			throw new NotImplementedException();
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static sbyte Abs(sbyte x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static short Abs(short x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static int Abs(int x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static long Abs(long x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static float Abs(float x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.ABS)]
		public static double Abs(double x)
		{
			return Math.Abs(x);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static sbyte Min(sbyte x, sbyte y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static short Min(short x, short y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static int Min(int x, int y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static long Min(long x, long y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static byte Min(byte x, byte y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static ushort Min(ushort x, ushort y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static uint Min(uint x, uint y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static ulong Min(ulong x, ulong y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static float Min(float x, float y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MIN)]
		public static double Min(double x, double y)
		{
			return Math.Min(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static sbyte Max(sbyte x, sbyte y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static short Max(short x, short y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static int Max(int x, int y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static long Max(long x, long y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static byte Max(byte x, byte y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static ushort Max(ushort x, ushort y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static uint Max(uint x, uint y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static ulong Max(ulong x, ulong y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static float Max(float x, float y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.MAX)]
		public static double Max(double x, double y)
		{
			return Math.Max(x, y);
		}
		
		[IntrinsicFunction(IROpCodes.SQRT)]
		public static float Sqrt(float x)
		{
			return (float)Math.Sqrt(x);
		}
		
		[IntrinsicFunction(IROpCodes.SQRT)]
		public static double Sqrt(double x)
		{
			return Math.Sqrt(x);
		}
		
		[IntrinsicFunction(IROpCodes.RSQRT)]
		public static float Rsqrt(float x)
		{
			return 1.0f / (float)Math.Sqrt(x);
		}
		
		[IntrinsicFunction(IROpCodes.RSQRT)]
		public static double Rsqrt(double x)
		{
			return 1.0d / Math.Sqrt(x);
		}
		
		[IntrinsicFunction(IROpCodes.SIN)]
		public static float Sin(float x)
		{
			return (float)Math.Sin(x);
		}
		
		[IntrinsicFunction(IROpCodes.COS)]
		public static float Cos(float x)
		{
			return (float)Math.Cos(x);
		}
		
		[IntrinsicFunction(IROpCodes.LG2)]
		public static float Log2(float x)
		{
			return (float)Math.Log(x, 2);
		}
		
		[IntrinsicFunction(IROpCodes.EX2)]
		public static float Exp2(float x)
		{
			return (float)Math.Pow(2, x);
		}
	}
}

