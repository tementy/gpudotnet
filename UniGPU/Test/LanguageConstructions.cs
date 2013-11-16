//
// LanguageConstructions.cs
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
using System.Reflection.Emit;
using NUnit.Framework;
using OpenCL.Net;
using UniGPU;
using UniGPU.Core;
using UniGPU.Core.IR;
using UniGPU.Core.CIL;

namespace UniGPUTest
{
	[TestFixture()]
	public class LanguageConstructions
	{
		private Cl.Device device;
		private Cl.Context context;
		private Cl.Program programInlined;
		private Cl.Mem dummy;
		private Cl.ErrorCode error;
		private Cl.Event clevent;

		private void clSafeCall(Cl.ErrorCode error)
		{
			Assert.AreEqual(Cl.ErrorCode.Success, error, error.ToString());
		}

		[TestFixtureSetUp]
		public void SetUp()
		{
			device = (from platformid in Cl.GetPlatformIDs(out error)
			          from deviceid in Cl.GetDeviceIDs(platformid, Cl.DeviceType.Gpu, out error)
			          select deviceid).First();

			context = Cl.CreateContext(null, 1, new[] { device }, null, IntPtr.Zero, out error);

			dummy = Cl.CreateBuffer(context, Cl.MemFlags.ReadOnly, IntPtr.Zero, IntPtr.Zero, out error);
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			programInlined.Dispose();
			context.Dispose();
		}

		private bool prepared = false;

		private void Prepare()
		{
			if (prepared)
				return;

			Program irprog = this.BuildIR();

			Console.WriteLine("Kernels: " +
				string.Join("; ", irprog.Kernels.Select(krn => krn.Name)));
			Console.WriteLine("Subprograms before inline: " +
				string.Join("; ", irprog.GetSubprograms().Except(irprog.Kernels).Select(sp => sp.Name)));			

			Program irprogInlined = irprog.InlineIR();

			Console.WriteLine("Subprograms after inline: " +
				string.Join("; ", irprogInlined.GetSubprograms().Except(irprogInlined.Kernels).Select(sp => sp.Name)));

			programInlined = irprogInlined.ToGPUClProgram(device, context);
			clSafeCall(Cl.BuildProgram(programInlined, 1, new[] { device }, string.Empty, null, IntPtr.Zero));
			Assert.AreEqual(Cl.BuildStatus.Success, Cl.GetProgramBuildInfo(programInlined, device, Cl.ProgramBuildInfo.Status, out error).
				CastTo<Cl.BuildStatus>());

			prepared = true;
		}

		private static void SimpleRefOutSetter(ref short p1, out int p2)
		{
			p2 = p1;
			p1 *= 2;
		}

		[Kernel]
		private static void SimpleRefOut([Global] short[] p1, [Global] int[] p2)
		{
			SimpleRefOutSetter(ref p1[0], out p2[0]);
		}

		public void SimpleRefOut(Cl.Program program)
		{
			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "SimpleRefOut", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host vectors
			short[] hp1 = { 1 };
			int[] hp2 = { 0 };

			// allocate device vectors
			Cl.Mem dp1 = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(short) * hp1.Length), hp1, out error);
			clSafeCall(error);
			Cl.Mem dp2 = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(int) * hp2.Length), hp2, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dp1));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dp2));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp1, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(short) * hp1.Length), hp1, 0, null, out clevent));
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp2, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(int) * hp1.Length), hp2, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(2, hp1[0]);
			Assert.AreEqual(1, hp2[0]);
		}

		[Test]
		public void SimpleRefOut()
		{
			Prepare();
			SimpleRefOut(programInlined);
		}

		[return: Global]
		private static int[] ArraySelector([Global] int[] p1, [Global] int[] p2)
		{
			return (p1[0] < p2[0]) ? p1 : p2;
		}

		private static void ArrayRefOutSetter([Global] ref int[] p1, [Global] ref int[] p2,
			[Global] out int[] temp)
		{
			temp = p1;
			p1 = p2;
			p2 = temp;
			temp = ArraySelector(p1, p2);
		}

		[Kernel]
		private static void ArrayRefOut([Global] int[] p1, [Global] int[] p2,
			[Global] int[] temp)
		{
			ArrayRefOutSetter(ref p1, ref p2, out temp);
			p1[0] *= 2;
			p2[0] *= 4;
			// This instruction gets invalid value on AMD because writes do not invalidate read cache.
			// So, it is normal that this test fails on AMD.
			// There is a recipe to avoid this behaviour in cost of performance:
			// - remove _cached suffixes from loading instructions.
			// But, is this Read-After-Write pattern good and suitable for global GPU memory with high latencies?
			temp[0]++;
		}

		public void ArrayRefOut(Cl.Program program)
		{
			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "ArrayRefOut", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host vectors
			int[] hp1 = { 1 };
			int[] hp2 = { 2 };

			// allocate device vectors
			Cl.Mem dp1 = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(int) * hp1.Length), hp1, out error);
			clSafeCall(error);
			Cl.Mem dp2 = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(int) * hp2.Length), hp2, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dp1));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dp2));
			clSafeCall(Cl.SetKernelArg(kernel, 2, dummy));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp1, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(int) * hp1.Length), hp1, 0, null, out clevent));
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp2, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(int) * hp1.Length), hp2, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(5, hp1[0]);
			Assert.AreEqual(4, hp2[0]);
		}

		[Test]
		public void ArrayRefOut()
		{
			Prepare();
			ArrayRefOut(programInlined);
		}

		[Kernel]
		private static void ArrayCompare([Global] int[] p1, [Global] int[] p2, [Global] bool[] r)
		{
			r[0] = p1 != null;
			r[1] = p1 == null;
			r[2] = p1 != p2;
			r[3] = p1 == p2;
			for (int i = 0; i < 4; i++)
				r[i] = !r[i];
		}

		public void ArrayCompare(Cl.Program program)
		{
			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "ArrayCompare", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host vectors
			bool[] res = { true, false, true, false };

			// allocate device vectors
			Cl.Mem dp1 = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly, (IntPtr)(sizeof(int)), IntPtr.Zero, out error);
			clSafeCall(error);
			Cl.Mem dp2 = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly, (IntPtr)(sizeof(int)), IntPtr.Zero, out error);
			clSafeCall(error);
			Cl.Mem dp3 = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly, (IntPtr)(sizeof(bool) * res.Length), IntPtr.Zero, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dp1));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dp2));
			clSafeCall(Cl.SetKernelArg(kernel, 2, dp3));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp3, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(bool) * res.Length), res, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(new[] { false, true, false, true }, res);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dummy));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dummy));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dp3, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(bool) * res.Length), res, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(new[] { true, false, true, false }, res);
		}

		[Test]
		public void ArrayCompare()
		{
			Prepare();
			ArrayCompare(programInlined);
		}

		[Kernel]
		private static void SmallTypes([Global] short[] res1, [Global] short[] res2, byte p1, sbyte p2, ushort p3, short p4, bool p5)
		{
			var r = p1 + p2 + p3 + p4;
			(p5 ? res1 : res2)[0] = (short)r;
			(p5 ? res2 : res1)[0] = (short)-r;
		}

		public void SmallTypes(Cl.Program program)
		{
			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "SmallTypes", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host vectors
			short[] hres1 = { 0 };
			short[] hres2 = { 0 };

			// allocate device vectors
			Cl.Mem dres1 = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly,
				(IntPtr)(sizeof(short) * hres1.Length), IntPtr.Zero, out error);
			clSafeCall(error);
			Cl.Mem dres2 = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly,
				(IntPtr)(sizeof(short) * hres2.Length), IntPtr.Zero, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dres1));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dres2));
			clSafeCall(Cl.SetKernelArg(kernel, 2, (byte)1));
			clSafeCall(Cl.SetKernelArg(kernel, 3, (sbyte)-20));
			clSafeCall(Cl.SetKernelArg(kernel, 4, (ushort)30));
			clSafeCall(Cl.SetKernelArg(kernel, 5, (short)-4));
			clSafeCall(Cl.SetKernelArg(kernel, 6, true));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dres1, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(short) * hres1.Length), hres1, 0, null, out clevent));
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dres2, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(short) * hres1.Length), hres2, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(7, hres1[0]);
			Assert.AreEqual(-7, hres2[0]);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 6, false));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dres1, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(short) * hres1.Length), hres1, 0, null, out clevent));
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dres2, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(short) * hres1.Length), hres2, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(-7, hres1[0]);
			Assert.AreEqual(7, hres2[0]);
		}

		[Test]
		public void SmallTypes()
		{
			Prepare();
			SmallTypes(programInlined);
		}

		private static void LoopBody(ref int q)
		{
			q = (q % 2 == 0) ? q + 1 : q + 3;
		}

		[Kernel]
		private static void ExternalLoopBody([Global] int[] p, int c)
		{
			int i = 0;
			do LoopBody(ref p[i++]); while (i < c);
		}

		public void ExternalLoopBody(Cl.Program program)
		{
			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "ExternalLoopBody", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host vectors
			int[] hres = { 0, 1, 2, 3, 4, 5 };

			// allocate device vectors
			Cl.Mem dres = Cl.CreateBuffer(context, Cl.MemFlags.ReadWrite | Cl.MemFlags.CopyHostPtr,
				(IntPtr)(sizeof(int) * hres.Length), hres, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dres));
			clSafeCall(Cl.SetKernelArg(kernel, 1, hres.Length));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dres, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(int) * hres.Length), hres, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Assert.AreEqual(new[] { 1, 4, 3, 6, 5, 8 }, hres);
		}

		[Test]
		public void ExternalLoopBody()
		{
			Prepare();
			ExternalLoopBody(programInlined);
		}

		[Test]
		public void BlockMutation()
		{
			AssemblyName assemblyName = new AssemblyName("UniGPUTestFixture");
			AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
			ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
			TypeBuilder typeBuilder = moduleBuilder.DefineType("CILBBTypeMutation", TypeAttributes.Public);
			MethodBuilder methodBuilder = typeBuilder.DefineMethod("TestCase", MethodAttributes.Public | MethodAttributes.Static,
				typeof(void), new Type[] { typeof(int), typeof(int[]) });
			methodBuilder.DefineParameter(1, ParameterAttributes.None, "arg");
			methodBuilder.DefineParameter(2, ParameterAttributes.None, "addr");

			ILGenerator il = methodBuilder.GetILGenerator();

			LocalBuilder lb = il.DeclareLocal(typeof(float));

			Label ZERO = il.DefineLabel();
			Label LOOP = il.DefineLabel();
			Label LOOP_FLT_MUTATOR = il.DefineLabel();
			Label LOOP_INT_MUTATOR = il.DefineLabel();

			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Ldc_I4_0);

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Brfalse, ZERO);

			il.MarkLabel(LOOP);
			il.Emit(OpCodes.Conv_I2);
			il.Emit(OpCodes.Starg, 0);
			il.Emit(OpCodes.Ldarga, 0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldc_I4_2);
			il.Emit(OpCodes.Rem);
			il.Emit(OpCodes.Not);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.And);
			il.Emit(OpCodes.Brtrue, LOOP_FLT_MUTATOR);

			il.MarkLabel(LOOP_INT_MUTATOR);
			il.Emit(OpCodes.Conv_I4);
			il.Emit(OpCodes.Starg, 0);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Ldc_I4_2);
			il.Emit(OpCodes.Div);
			il.Emit(OpCodes.Ldc_I4_M1);
			il.Emit(OpCodes.Neg);
			il.Emit(OpCodes.Sub);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Bge, LOOP);

			il.Emit(OpCodes.Br, ZERO);

			il.MarkLabel(LOOP_FLT_MUTATOR);
			il.Emit(OpCodes.Conv_R4);
			il.Emit(OpCodes.Stloc_0);
			il.Emit(OpCodes.Pop);
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ldc_R4, 1.0f);
			il.Emit(OpCodes.Sub);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldc_R4, 1.0f);
			il.Emit(OpCodes.Bge, LOOP);

			il.Emit(OpCodes.Conv_I4);

			il.MarkLabel(ZERO);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stelem_I4);
			il.Emit(OpCodes.Ret);

			MethodInfo method = typeBuilder.CreateType().GetMethod(methodBuilder.Name);

			int[] res = { 0 };

			method.Invoke(null, new object[] { 8, res });

			//Assert.AreEqual(1, res[0]);

			Cl.Program program = method.BuildIR().ToGPUClProgram(device, context);
			clSafeCall(Cl.BuildProgram(program, 1, new[] { device }, string.Empty, null, IntPtr.Zero));
			Assert.AreEqual(Cl.BuildStatus.Success, Cl.GetProgramBuildInfo(program, device, Cl.ProgramBuildInfo.Status, out error).
				CastTo<Cl.BuildStatus>());

			Cl.Kernel kernel = Cl.CreateKernel(program, "TestCase", out error);
			clSafeCall(error);

			Cl.Mem cl_res = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly, (IntPtr)sizeof(int), IntPtr.Zero, out error);
			clSafeCall(error);

			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, (Cl.CommandQueueProperties)0, out error);
			clSafeCall(error);

			clSafeCall(Cl.SetKernelArg(kernel, 0, 8));
			clSafeCall(Cl.SetKernelArg(kernel, 1, cl_res));

			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)1 }, null, 0, null, out clevent));

			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, cl_res, Cl.Bool.True, IntPtr.Zero, (IntPtr)sizeof(int), res, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			clSafeCall(Cl.ReleaseMemObject(cl_res));

			program.Dispose();

			Assert.AreEqual(1, res[0]);
		}
	}
}

