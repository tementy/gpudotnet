//
// LinearAlgebra.cs
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
using System.Diagnostics;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenCL.Net;
using UniGPU;
using UniGPU.Core;
using UniGPU.Core.IR;
using UniGPU.Core.CIL;

namespace UniGPUTest
{
	[TestFixture()]
	public class LinearAlgebra
	{
		private Cl.Device device;
		private Cl.Context context;
		private Cl.Program program;
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
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			program.Dispose();
			context.Dispose();
		}

		private bool prepared = false;

		private void Prepare(Program irprog)
		{
			Console.WriteLine("Kernels: " +
				string.Join("; ", irprog.Kernels.Select(krn => krn.Name)));
			Console.WriteLine("Subprograms: " +
				string.Join("; ", irprog.GetSubprograms().Except(irprog.Kernels).Select(sp => sp.Name)));

			program = irprog.ToGPUClProgram(device, context);

			clSafeCall(Cl.BuildProgram(program, 1, new[] { device }, string.Empty, null, IntPtr.Zero));
			Assert.AreEqual(Cl.BuildStatus.Success, Cl.GetProgramBuildInfo(program, device, Cl.ProgramBuildInfo.Status, out error).
				CastTo<Cl.BuildStatus>());
		}

		[Kernel]
		private static void VecAdd([Global] float[] a, [Global] float[] b, [Global] float[] c, int len)
		{
			int tid = (int)(ThreadIdx.X + BlockIdx.X * BlockDim.X);
			if (tid < len)
				c[tid] = a[tid] + b[tid];
		}

		[Test()]
		public void VecAdd()
		{
			if (!prepared)
			{
				Prepare(this.BuildIR().InlineIR());
				prepared = true;
			}

			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "VecAdd", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			int length = 1 << 10;

			// allocate host vectors
			float[] A = new float[length];
			float[] B = new float[length];
			float[] C = new float[length];

			// initialize host memory
			Random rand = new Random();
			for (int i = 0; i < length; i++)
			{
				A[i] = (float)rand.Next() / short.MaxValue;
				B[i] = (float)rand.Next() / short.MaxValue;
			}

			// allocate device vectors
			Cl.Mem hDeviceMemA = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly,
				(IntPtr)(sizeof(float) * length), A, out error);
			clSafeCall(error);
			Cl.Mem hDeviceMemB = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly,
				(IntPtr)(sizeof(float) * length), B, out error);
			clSafeCall(error);
			Cl.Mem hDeviceMemC = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly,
				(IntPtr)(sizeof(float) * length), IntPtr.Zero, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, hDeviceMemA));
			clSafeCall(Cl.SetKernelArg(kernel, 1, hDeviceMemB));
			clSafeCall(Cl.SetKernelArg(kernel, 2, hDeviceMemC));
			clSafeCall(Cl.SetKernelArg(kernel, 3, length));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new[] { (IntPtr)length }, new[] { (IntPtr)256 },
				0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, hDeviceMemC, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(float) * length), C, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			for (int i = 0; i < length; i++)
			{
				float sum = A[i] + B[i];
				float err = Math.Abs((sum - C[i]) / sum);
				Assert.That(err, Is.LessThanOrEqualTo(1E-3F));
			}
		}

		private const uint BLOCK_SIZE = 16;

		// Matrix dimensions
		// (chosen as multiples of the thread block size for simplicity)
		private const uint WA = (3 * BLOCK_SIZE); // Matrix A width
		private const uint HA = (5 * BLOCK_SIZE); // Matrix A height
		private const uint WB = (8 * BLOCK_SIZE); // Matrix B width
		private const uint HB = WA;  // Matrix B height
		private const uint WC = WB;  // Matrix C width
		private const uint HC = HA;  // Matrix C height

		[Kernel]
		private static void MatMul([Global] float[] A, [Global] float[] B, [Global] float[] C,
			[Shared] float[] As, [Shared] float[] Bs, uint wA, uint wB)
		{
			// Thread index
			uint tx = ThreadIdx.X;
			uint ty = ThreadIdx.Y;

			// Index of the first sub-matrix of A processed by the block
			uint aBegin = wA * BLOCK_SIZE * BlockIdx.Y;

			// Index of the last sub-matrix of A processed by the block
			uint aEnd = aBegin + wA - 1;

			// Step size used to iterate through the sub-matrices of A
			uint aStep = BLOCK_SIZE;

			// Index of the first sub-matrix of B processed by the block
			uint bBegin = BLOCK_SIZE * BlockIdx.X;

			// Step size used to iterate through the sub-matrices of B
			uint bStep = BLOCK_SIZE * wB;

			// Csub is used to store the element of the block sub-matrix
			// that is computed by the thread
			float Csub = 0;

			// Loop over all the sub-matrices of A and B
			// required to compute the block sub-matrix
			for (uint a = aBegin, b = bBegin; a <= aEnd; a += aStep, b += bStep)
			{
				// Load the matrices from device memory
				// to shared memory; each thread loads
				// one element of each matrix
				As[ty * BLOCK_SIZE + tx] = A[a + wA * ty + tx];
				Bs[ty * BLOCK_SIZE + tx] = B[b + wB * ty + tx];

				// Synchronize to make sure the matrices are loaded
				BuiltinFunctions.SyncThreads();

				// Multiply the two matrices together;
				// each thread computes one element
				// of the block sub-matrix
				for (uint k = 0; k < BLOCK_SIZE; ++k)
					Csub += As[ty * BLOCK_SIZE + k] * Bs[k * BLOCK_SIZE + tx];

				// Synchronize to make sure that the preceding
				// computation is done before loading two new
				// sub-matrices of A and B in the next iteration
				BuiltinFunctions.SyncThreads();
			}

			// Write the block sub-matrix to device memory;
			// each thread writes one element
			uint c = wB * BLOCK_SIZE * BlockIdx.Y + BLOCK_SIZE * BlockIdx.X;
			C[c + wB * ty + tx] = Csub;
		}

		[Test()]
		public void MatMul()
		{
			if (!prepared)
			{
				Prepare(this.BuildIR().InlineIR());
				prepared = true;
			}

			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "MatMul", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// allocate host matrices
			float[] A = new float[WA * HA];
			float[] B = new float[WB * HB];
			float[] C = new float[WC * HC];

			// initialize host memory
			Random rand = new Random();
			for (int i = 0; i < A.Length; i++)
				A[i] = (float)rand.Next() / short.MaxValue;
			for (int i = 0; i < B.Length; i++)
				B[i] = (float)rand.Next() / short.MaxValue;

			// allocate device vectors
			Cl.Mem hDeviceMemA = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly,
				(IntPtr)(sizeof(float) * A.Length), A, out error);
			clSafeCall(error);
			Cl.Mem hDeviceMemB = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly,
				(IntPtr)(sizeof(float) * B.Length), B, out error);
			clSafeCall(error);
			Cl.Mem hDeviceMemC = Cl.CreateBuffer(context, Cl.MemFlags.WriteOnly,
				(IntPtr)(sizeof(float) * C.Length), IntPtr.Zero, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, hDeviceMemA));
			clSafeCall(Cl.SetKernelArg(kernel, 1, hDeviceMemB));
			clSafeCall(Cl.SetKernelArg(kernel, 2, hDeviceMemC));
			clSafeCall(Cl.SetKernelArg(kernel, 3, BLOCK_SIZE * BLOCK_SIZE * sizeof(float), null));
			clSafeCall(Cl.SetKernelArg(kernel, 4, BLOCK_SIZE * BLOCK_SIZE * sizeof(float), null));
			clSafeCall(Cl.SetKernelArg(kernel, 5, WA));
			clSafeCall(Cl.SetKernelArg(kernel, 6, WB));

			// execute kernel
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, new[] { (IntPtr)WC, (IntPtr)HC },
				new[] { (IntPtr)BLOCK_SIZE, (IntPtr)BLOCK_SIZE }, 0, null, out clevent));

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, hDeviceMemC, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(float) * C.Length), C, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			for (int i = 0; i < HA; ++i)
			{
				for (int j = 0; j < WB; ++j)
				{
					float sum = 0;
					for (int k = 0; k < WA; ++k)
					{
						sum += A[i * WA + k] * B[k * WB + j];
					}
					float err = Math.Abs((sum - C[i * WB + j]) / sum);
					Assert.That(err, Is.LessThanOrEqualTo(1E-3F));
				}
			}
		}

		private const uint TILE_SIZE_X = 16;
		private const uint TILE_SIZE_Y = 16;
		private const uint TILES_PER_BLOCK_X = 4;
		private const uint TILES_PER_BLOCK_Y = 1;
		private const uint AREA_SIZE_X = TILE_SIZE_X * TILES_PER_BLOCK_X;
		private const uint AREA_SIZE_Y = TILE_SIZE_Y * TILES_PER_BLOCK_Y;

		private static uint IdxBuf(uint row, uint col)
		{
			return row * (AREA_SIZE_X + 2) + col;
		}

		private static float J(float x, float y)
		{
			return BuiltinFunctions.Sin(x * y) * (x * x + y * y);
		}

		[Kernel]
		private static void PoissonJacobi([Global] float[] input, [Global] float[] output, [Shared] float[] buf,
			uint dimX, uint dimY, uint stride,
			float a1, float a2, float a3, float a4, float a,
			float hx, float hy, float x0, float y0)
		{
			uint col_cnt = BuiltinFunctions.Min(AREA_SIZE_X + 2, dimX - BlockIdx.X * AREA_SIZE_X);
			uint row_cnt = BuiltinFunctions.Min(AREA_SIZE_Y + 2, dimY - BlockIdx.Y * AREA_SIZE_Y);

			for (uint row = ThreadIdx.Y; row < row_cnt; row += BlockDim.Y)
			{
				uint x = ThreadIdx.X + BlockIdx.X * AREA_SIZE_X;
				uint y = row + BlockIdx.Y * AREA_SIZE_Y;
				uint idx = x + y * stride;
				for (uint col = ThreadIdx.X; col < col_cnt; col += BlockDim.X, idx += BlockDim.X)
					buf[IdxBuf(row, col)] = input[idx];
			}

			BuiltinFunctions.SyncThreads();

			col_cnt -= 2;
			row_cnt -= 2;

			for (uint row = ThreadIdx.Y; row < row_cnt; row += BlockDim.Y)
			{
				uint x = 1 + ThreadIdx.X + BlockIdx.X * AREA_SIZE_X;
				uint y = 1 + row + BlockIdx.Y * AREA_SIZE_Y;
				uint idx = x + y * stride;
				for (uint col = ThreadIdx.X; col < col_cnt; col += BlockDim.X, idx += BlockDim.X, x += BlockDim.X)
				{
					float F = 2 * hx * hy * J(x0 + x * hx, y0 + y * hy);
					output[idx] = (a1 * buf[IdxBuf(row + 2, col + 1)] + a2 * buf[IdxBuf(row + 1, col + 2)] +
						a3 * buf[IdxBuf(row, col + 1)] + a4 * buf[IdxBuf(row + 1, col)] + F) / a;
				}
			}
        }

		private static float u(float x, float y)
		{
			return (float)Math.Sin(x * y) + 1.0f;
		}

		[Test]
		public void PoissonJacobi()
		{
			if (!prepared)
			{
				Prepare(this.BuildIR().InlineIR());
				prepared = true;
			}

			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "PoissonJacobi", out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);

			// initialize host memory

			uint dimX = 162;
			uint dimY = 122;
			uint N = 15000;

			float x0 = (float)(-0.25 * Math.PI);
			float y0 = (float)(-0.25 * Math.PI);

			float hx = 2.0f * Math.Abs(x0) / dimX;
			float hy = 2.0f * Math.Abs(y0) / dimY;

			float[] hData = new float[dimX * dimY];

			uint stride = dimX;

			//boundary values

			for (uint i = 1; i < dimY - 1; i++)
			{
				uint y_idx = i * stride;
				float y_val = y0 + i * hy;
				hData[y_idx] = u(x0, y_val);
				hData[y_idx + dimX - 1] = u(x0 + (dimX - 1) * hx, y_val);
			}

			for (uint j = 1; j < dimX - 1; j++)
			{
				float x_val = x0 + j * hx;
				hData[j] = u(x_val, y0);
				hData[j + (dimY - 1) * stride] = u(x_val, y0 + (dimY - 1) * hy);
			}

			// allocate device vectors
			Cl.Mem input = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(float) * hData.Length), hData, out error);
			clSafeCall(error);
			Cl.Mem output = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(float) * hData.Length), hData, out error);
			clSafeCall(error);

			float a1 = 2 * hy / hx;
    		float a2 = 2 * hx / hy;
    		float a3 = a1;
    		float a4 = a2;
    		float a = a1 + a2 + a3 + a4;

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 2, (AREA_SIZE_Y + 2) * (AREA_SIZE_X + 2) * sizeof(float), null));
			clSafeCall(Cl.SetKernelArg(kernel, 3, dimX));
			clSafeCall(Cl.SetKernelArg(kernel, 4, dimY));
			clSafeCall(Cl.SetKernelArg(kernel, 5, stride));
			clSafeCall(Cl.SetKernelArg(kernel, 6, a1));
			clSafeCall(Cl.SetKernelArg(kernel, 7, a2));
			clSafeCall(Cl.SetKernelArg(kernel, 8, a3));
			clSafeCall(Cl.SetKernelArg(kernel, 9, a4));
			clSafeCall(Cl.SetKernelArg(kernel, 10, a));
			clSafeCall(Cl.SetKernelArg(kernel, 11, hx));
			clSafeCall(Cl.SetKernelArg(kernel, 12, hy));
			clSafeCall(Cl.SetKernelArg(kernel, 13, x0));
			clSafeCall(Cl.SetKernelArg(kernel, 14, y0));

			IntPtr[] lo = { (IntPtr)16, (IntPtr)16 };
			IntPtr[] gl = { (IntPtr)((dimX - 2 + AREA_SIZE_X - 1) / AREA_SIZE_X * 16),
				(IntPtr)((dimY - 2 + AREA_SIZE_Y - 1) / AREA_SIZE_Y * 16)};

			Cl.Mem curIn = input;
			Cl.Mem curOut = output;

			// execute kernel (and perform data transfering silently)
			clSafeCall(Cl.SetKernelArg(kernel, 0, curIn));
			clSafeCall(Cl.SetKernelArg(kernel, 1, curOut));
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			for (uint idx = 1; idx < N; idx++)
			{
				// swap buffers
				Cl.Mem temp = curIn;
				curIn = curOut;
				curOut = temp;

				// execute kernel
				clSafeCall(Cl.SetKernelArg(kernel, 0, curIn));
				clSafeCall(Cl.SetKernelArg(kernel, 1, curOut));
				clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));
			}

			clSafeCall(Cl.Finish(cmdQueue));

			stopwatch.Stop();

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, curOut, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(float) * hData.Length), hData, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));
			
			float avgerr = 0, maxerr = 0;
            for (uint i = 1; i < dimY - 1; i++)
			{
				for (uint j = 1; j < dimX - 1; j++)
				{
					float theory = u(x0 + j * hx, y0 + i * hy);
					float err = Math.Abs(theory - hData[j + i * stride]) / Math.Abs(theory);
					avgerr += err;
					maxerr = Math.Max(maxerr, err);
				}
			}
			avgerr /= dimX * dimY;

			long elapsedTime = stopwatch.ElapsedMilliseconds;
			double dataSizePerIteration = dimX * dimY * 2 * sizeof(float);
			double dataSizeTotal = dataSizePerIteration * N;
			double elapsedSeconds = elapsedTime * 0.001;
			double gigabyteFactor = 1 << 30;
			double bandwidth = dataSizeTotal / (gigabyteFactor * elapsedSeconds);
			
			Console.WriteLine("avgerr = {0} maxerr = {1} elapsedTime = {2} ms bandwidth = {3} GB/s",
				avgerr, maxerr, elapsedTime, bandwidth);

			Assert.That(maxerr, Is.LessThanOrEqualTo(5E-2F));
			Assert.That(avgerr, Is.LessThanOrEqualTo(1E-2F));
		}
	}
}

