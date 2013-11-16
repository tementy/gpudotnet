//
// PoissonRBSOR.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using OpenCL.Net;
using UniGPU;
using UniGPU.Core;
using UniGPU.Core.IR;
using UniGPU.Core.CIL;

namespace PoissonRBSOR
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			int dimX = GetParameter("--dimX", 322, args);
			int dimY = GetParameter("--dimY", 242, args);
			int N = GetParameter("--N", 97760, args);
			bool lmem = GetFlag("--lmem", args);

			Console.WriteLine("Poisson equation solver: dimX = {0} dimY = {1} N = {2}, LMem: {3}", dimX, dimY, N, lmem);
			string options = string.Join(" ", args.Where(arg => arg.IndexOf("--") == -1));
			Console.WriteLine("OpenCL program build options: " + options);

			Cl.ErrorCode error;

			Cl.Device device = (from platformid in Cl.GetPlatformIDs(out error)
				from deviceid in Cl.GetDeviceIDs(platformid, Cl.DeviceType.Gpu, out error)
				select deviceid).First();
			clSafeCall(error);

			Cl.Context context = Cl.CreateContext(null, 1, new[] { device }, null, IntPtr.Zero, out error);
			clSafeCall(error);

			// create program from C# kernel
			IRBuildOptions.AutoInline = GetFlag("--inline", args);
			IRBuildOptions.WasteRegisters = GetFlag("--rwaste", args);
			Console.WriteLine("IR code build options: AutoInline: {0}; WasteRegisters: {1}",
				IRBuildOptions.AutoInline, IRBuildOptions.WasteRegisters);
			Cl.Program pcsharp = typeof(MainClass).BuildIR().ToGPUClProgram(device, context);

			// create program from OpenCL kernel
			Cl.Program popencl = Cl.CreateProgramWithSource(context, 1, new[] { PoissonRBSORCl }, null, out error);
			clSafeCall(error);

			// perform bandwidth comparison
			
			float x0 = (float)(-0.5 * Math.PI);
			float y0 = (float)(-0.5 * Math.PI);
			float x1 = -x0;
			float y1 = -y0;
			float omega = 0.8f;
			
			Console.WriteLine("C# benchmark:");
			long tcsharp = PoissonRBSOR(device, context, pcsharp, lmem,
				x0, y0, x1, y1, dimX, dimY, N, omega, "unigpu.bin", options);

			pcsharp.Dispose();

			Console.WriteLine("OpenCL benchmark:");
			long topencl = PoissonRBSOR(device, context, popencl, lmem,
				x0, y0, x1, y1, dimX, dimY, N, omega, "opencl.bin", options);

			popencl.Dispose();

			Console.WriteLine("OpenCL advantage: {0}", (double)tcsharp / (double)topencl);

			context.Dispose();
		}

		private static T GetParameter<T>(string name, T defval, string[] args)
		{
			string param = args.SingleOrDefault(arg => arg.IndexOf(name + "=") == 0);
			return (param == null) ? defval :
				(T)Convert.ChangeType(param.Substring(name.Length + 1), typeof(T));
		}

		private static bool GetFlag(string name, string[] args)
		{
			return args.SingleOrDefault(arg => arg == name) != null;
		}

		private static void clSafeCall(Cl.ErrorCode error)
		{
			if (error != Cl.ErrorCode.Success)
				throw new Exception(error.ToString());
		}

		private static string PoissonRBSORCl = @"
#define TILE_SIZE_X 16
#define TILE_SIZE_Y 16
#define TILES_PER_BLOCK_X 4
#define TILES_PER_BLOCK_Y 1
#define AREA_SIZE_X (TILE_SIZE_X * TILES_PER_BLOCK_X)
#define AREA_SIZE_Y (TILE_SIZE_Y * TILES_PER_BLOCK_Y)

#define BUF(row, col) buf[(col) + (row) * (AREA_SIZE_X + 2)]

__kernel void PoissonRBSOR_LMem(__global float* grid, __global float* laplacian,
	int dimX, int dimY, int gstride, int lstride,
	float hx, float hy, float omega, int color,
	__local float* buf)
{
	int threadIdxX = get_local_id(0);
	int threadIdxY = get_local_id(1);
	int blockDimX = get_local_size(0);
	int blockDimY = get_local_size(1);
	int blockIdxX = get_group_id(0);
	int blockIdxY = get_group_id(1);

	int col_cnt = min(AREA_SIZE_X + 2, dimX - blockIdxX * AREA_SIZE_X);
	int row_cnt = min(AREA_SIZE_Y + 2, dimY - blockIdxY * AREA_SIZE_Y);

	for (int row = threadIdxY; row < row_cnt; row += blockDimY)
	{
		int x = threadIdxX + blockIdxX * AREA_SIZE_X;
		int y = row + blockIdxY * AREA_SIZE_Y;
		int index = x + y * gstride;
		for (int col = threadIdxX; col < col_cnt; col += blockDimX, index += blockDimX)
			BUF(row, col) = grid[index];
	}

	barrier(CLK_LOCAL_MEM_FENCE);

	col_cnt -= 2;
	row_cnt -= 2;

	int col_start = 2 * threadIdxX;
	int col_delta = 2 * blockDimX;

	float b = 2 * hx * hy;
	float a1 = 2 * hy / hx;
	float a2 = 2 * hx / hy;
	float p = 0.5f * omega / (a1 + a2);
	float q = 1 - omega;

	for (int row = threadIdxY; row < row_cnt; row += blockDimY)
	{
		int col_offset = col_start + (color + row) % 2;
		int x = col_offset + blockIdxX * AREA_SIZE_X;
		int y = row + blockIdxY * AREA_SIZE_Y;
		int index = x + 1 + (y + 1) * gstride;

		for (int col = col_offset; col < col_cnt; col += col_delta, index += col_delta, x += col_delta)
			grid[index] = (b * laplacian[x + y * lstride] +
				a1 * (BUF(row + 2, col + 1) + BUF(row, col + 1)) +
				a2 * (BUF(row + 1, col + 2) + BUF(row + 1, col))) * p +
				BUF(row + 1, col + 1) * q;
	}
}

__kernel void PoissonRBSOR(__global float* grid, __global float* laplacian,
	int dimX, int dimY, int gstride, int lstride,
	float hx, float hy, float omega, int color)
{
	int threadIdxX = get_local_id(0);
	int threadIdxY = get_local_id(1);
	int blockDimX = get_local_size(0);
	int blockDimY = get_local_size(1);
	int blockIdxX = get_group_id(0);
	int blockIdxY = get_group_id(1);

	int x = 2 * threadIdxX + (color + threadIdxY) % 2 + blockIdxX * blockDimX;
	int y = threadIdxY + blockIdxY * blockDimY;

	if (x < dimX - 2 && y < dimY - 2)
	{
		float b = 2 * hx * hy;
		float a1 = 2 * hy / hx;
		float a2 = 2 * hx / hy;
		float p = 0.5f * omega / (a1 + a2);
		float q = 1 - omega;
		
		int index = x + 1 + (y + 1) * gstride;
	
		grid[index] = (b * laplacian[x + y * lstride] +
			a1 * (grid[index + gstride] + grid[index - gstride]) +
			a2 * (grid[index + 1] + grid[index - 1])) * p +
			grid[index] * q;
	}
}
		";
		private const int TILE_SIZE_X = 16;
		private const int TILE_SIZE_Y = 16;
		private const int TILES_PER_BLOCK_X = 4;
		private const int TILES_PER_BLOCK_Y = 1;
		private const int AREA_SIZE_X = TILE_SIZE_X * TILES_PER_BLOCK_X;
		private const int AREA_SIZE_Y = TILE_SIZE_Y * TILES_PER_BLOCK_Y;

		private static int IdxBuf(int row, int col)
		{
			return row * (AREA_SIZE_X + 2) + col;
		}

		[Kernel]
		private static void PoissonRBSOR_LMem([Global] float[] grid, [Global] float[] laplacian,
			int dimX, int dimY, int gstride, int lstride,
			float hx, float hy, float omega, int color,
			[Shared] float[] buf)
		{
			int threadIdxX = (int)ThreadIdx.X;
			int threadIdxY = (int)ThreadIdx.Y;
			int blockDimX = (int)BlockDim.X;
			int blockDimY = (int)BlockDim.Y;
			int blockIdxX = (int)BlockIdx.X;
			int blockIdxY = (int)BlockIdx.Y;

			int col_cnt = BuiltinFunctions.Min(AREA_SIZE_X + 2, dimX - blockIdxX * AREA_SIZE_X);
			int row_cnt = BuiltinFunctions.Min(AREA_SIZE_Y + 2, dimY - blockIdxY * AREA_SIZE_Y);
		
			for (int row = threadIdxY; row < row_cnt; row += blockDimY)
			{
				int x = threadIdxX + blockIdxX * AREA_SIZE_X;
				int y = row + blockIdxY * AREA_SIZE_Y;
				int index = x + y * gstride;
				for (int col = threadIdxX; col < col_cnt; col += blockDimX, index += blockDimX)
					buf[IdxBuf(row, col)] = grid[index];
			}

			BuiltinFunctions.SyncThreads();

			col_cnt -= 2;
			row_cnt -= 2;

			int col_start = 2 * threadIdxX;
			int col_delta = 2 * blockDimX;
		
			float b = 2 * hx * hy;
			float a1 = 2 * hy / hx;
			float a2 = 2 * hx / hy;
			float p = 0.5f * omega / (a1 + a2);
			float q = 1 - omega;

			for (int row = threadIdxY; row < row_cnt; row += blockDimY)
			{
				int col_offset = col_start + (color + row) % 2;
				int x = col_offset + blockIdxX * AREA_SIZE_X;
				int y = row + blockIdxY * AREA_SIZE_Y;
				int index = x + 1 + (y + 1) * gstride;

				for (int col = col_offset; col < col_cnt; col += col_delta, index += col_delta, x += col_delta)
					grid[index] = (b * laplacian[x + y * lstride] +
						a1 * (buf[IdxBuf(row + 2, col + 1)] + buf[IdxBuf(row, col + 1)]) +
						a2 * (buf[IdxBuf(row + 1, col + 2)] + buf[IdxBuf(row + 1, col)])) * p +
						buf[IdxBuf(row + 1, col + 1)] * q;
			}
		}

		[Kernel]
		private static void PoissonRBSOR([Global] float[] grid, [Global] float[] laplacian,
			int dimX, int dimY, int gstride, int lstride,
			float hx, float hy, float omega, int color)
		{
			int threadIdxX = (int)ThreadIdx.X;
			int threadIdxY = (int)ThreadIdx.Y;
			int blockDimX = (int)BlockDim.X;
			int blockDimY = (int)BlockDim.Y;
			int blockIdxX = (int)BlockIdx.X;
			int blockIdxY = (int)BlockIdx.Y;

			int x = 2 * threadIdxX + (color + threadIdxY) % 2 + blockIdxX * blockDimX;
			int y = threadIdxY + blockIdxY * blockDimY;
		
			if (x < dimX - 2 && y < dimY - 2)
			{
				float b = 2 * hx * hy;
				float a1 = 2 * hy / hx;
				float a2 = 2 * hx / hy;
				float p = 0.5f * omega / (a1 + a2);
				float q = 1 - omega;
				
				int index = x + 1 + (y + 1) * gstride;
			
				grid[index] = (b * laplacian[x + y * lstride] +
					a1 * (grid[index + gstride] + grid[index - gstride]) +
					a2 * (grid[index + 1] + grid[index - 1])) * p +
					grid[index] * q;
			}
		}

		private static float u(float x, float y)
		{
			return (float)Math.Sin(x * y) + 1.5f;
		}
		
		private static float J(float x, float y)
		{
			return (float)Math.Sin(x * y) * (x * x + y * y);
		}

		private static long PoissonRBSOR(Cl.Device device, Cl.Context context, Cl.Program program, bool lmem,
			float x0, float y0, float x1, float y1, int dimX, int dimY, int N, float omega, 
			string fileName = null, string options = "")
		{
			Cl.ErrorCode error;
			Cl.Event clevent;

			// build program
			clSafeCall(Cl.BuildProgram(program, 1, new[] { device }, options, null, IntPtr.Zero));
			Cl.BuildStatus status = Cl.GetProgramBuildInfo(program, device, Cl.ProgramBuildInfo.Status, out error).CastTo<Cl.BuildStatus>();
			if (status != Cl.BuildStatus.Success)
				throw new Exception(status.ToString());

			// save binary
			if (fileName != null)
			{
				Cl.InfoBuffer binarySizes = Cl.GetProgramInfo(program, Cl.ProgramInfo.BinarySizes, out error);
				clSafeCall(error);
				Cl.InfoBufferArray binaries = new Cl.InfoBufferArray(
					binarySizes.CastToEnumerable<IntPtr>(Enumerable.Range(0, 1)).Select(sz => new Cl.InfoBuffer(sz)).ToArray());
				IntPtr szRet;
				clSafeCall(Cl.GetProgramInfo(program, Cl.ProgramInfo.Binaries, binaries.Size, binaries, out szRet));
				byte[] binary = binaries[0].CastToArray<byte>(binarySizes.CastTo<IntPtr>(0).ToInt32());
				File.WriteAllBytes(fileName, binary);
			}

			// create kernel
			Cl.Kernel kernel = Cl.CreateKernel(program, "PoissonRBSOR" + (lmem ? "_LMem" : ""), out error);
			clSafeCall(error);

			// create command queue
			Cl.CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, Cl.CommandQueueProperties.None, out error);
			clSafeCall(error);
			
			float hx = (x1 - x0) / dimX;
			float hy = (y1 - y0) / dimY;
			
			// boundary values
			
			float[] hgrid = new float[dimX * dimY];

			int gstride = dimX;

			for (int i = 1; i < dimY - 1; i++)
			{
				int y_idx = i * gstride;
				float y_val = y0 + i * hy;
				hgrid[y_idx] = u(x0, y_val);
				hgrid[y_idx + dimX - 1] = u(x0 + (dimX - 1) * hx, y_val);
			}

			for (int j = 1; j < dimX - 1; j++)
			{
				float x_val = x0 + j * hx;
				hgrid[j] = u(x_val, y0);
				hgrid[j + (dimY - 1) * gstride] = u(x_val, y0 + (dimY - 1) * hy);
			}
			
			// laplacian values
			
			float[] hlaplacian = new float[(dimX - 2) * (dimY - 2)];
			
			int lstride = dimX - 2;

			for (int i = 1; i < dimY - 1; i++)
				for (int j = 1; j < dimX - 1; j++)
					hlaplacian[j - 1 + (i - 1) * lstride] = J(x0 + j * hx, y0 + i * hy);
			
			// allocate device vectors
			Cl.Mem dgrid = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite,
				(IntPtr)(sizeof(float) * hgrid.Length), hgrid, out error);
			clSafeCall(error);
			Cl.Mem dlaplacian = Cl.CreateBuffer(context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly,
				(IntPtr)(sizeof(float) * hlaplacian.Length), hlaplacian, out error);
			clSafeCall(error);

			// setup kernel arguments
			clSafeCall(Cl.SetKernelArg(kernel, 0, dgrid));
			clSafeCall(Cl.SetKernelArg(kernel, 1, dlaplacian));
			clSafeCall(Cl.SetKernelArg(kernel, 2, dimX));
			clSafeCall(Cl.SetKernelArg(kernel, 3, dimY));
			clSafeCall(Cl.SetKernelArg(kernel, 4, gstride));
			clSafeCall(Cl.SetKernelArg(kernel, 5, lstride));
			clSafeCall(Cl.SetKernelArg(kernel, 6, hx));
			clSafeCall(Cl.SetKernelArg(kernel, 7, hy));
			clSafeCall(Cl.SetKernelArg(kernel, 8, omega));
			if (lmem)
				clSafeCall(Cl.SetKernelArg(kernel, 10, (AREA_SIZE_Y + 2) * (AREA_SIZE_X + 2) * sizeof(float), null));

			IntPtr[] lo = { (IntPtr)TILE_SIZE_X, (IntPtr)TILE_SIZE_Y };
			IntPtr[] gl = {
				(IntPtr)((dimX - 2 + (lmem ? AREA_SIZE_X : TILE_SIZE_X) - 1) /
				         (lmem ? AREA_SIZE_X : TILE_SIZE_X) * TILE_SIZE_X),
				(IntPtr)((dimY - 2 + (lmem ? AREA_SIZE_Y : TILE_SIZE_Y) - 1) /
				         (lmem ? AREA_SIZE_Y : TILE_SIZE_Y) * TILE_SIZE_Y)
			};

			// execute RED kernel
			clSafeCall(Cl.SetKernelArg(kernel, 9, 1));
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));

			// execute BLACK kernel
			clSafeCall(Cl.SetKernelArg(kernel, 9, 0));
			clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			for (int idx = 1; idx < N; idx++)
			{
				// execute RED kernel
				clSafeCall(Cl.SetKernelArg(kernel, 9, 1));
				clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));

				// execute BLACK kernel
				clSafeCall(Cl.SetKernelArg(kernel, 9, 0));
				clSafeCall(Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, gl, lo, 0, null, out clevent));
			}
			
			clSafeCall(Cl.Finish(cmdQueue));

			stopwatch.Stop();

			// copy results from device back to host
			clSafeCall(Cl.EnqueueReadBuffer(cmdQueue, dgrid, Cl.Bool.True, IntPtr.Zero,
                (IntPtr)(sizeof(float) * hgrid.Length), hgrid, 0, null, out clevent));

			clSafeCall(Cl.Finish(cmdQueue));

			cmdQueue.Dispose();
			kernel.Dispose();
			dgrid.Dispose();
			
			float avgerr = 0, maxerr = 0;
			for (int i = 1; i < dimY - 1; i++)
			{
				for (int j = 1; j < dimX - 1; j++)
				{
					float theory = u(x0 + j * hx, y0 + i * hy);
					float err = Math.Abs(theory - hgrid[j + i * gstride]) / Math.Abs(theory);
					avgerr += err;
					maxerr = Math.Max(maxerr, err);
				}
			}
			avgerr /= dimX * dimY;

			long elapsedTime = stopwatch.ElapsedMilliseconds;
			
			Console.WriteLine("average error = {0}%\nmaximal error = {1}%\nelapsed time: {2}ms\niterations per second: {3}",
				avgerr * 100, maxerr * 100, elapsedTime, (double)N / (double)elapsedTime * 1000.0d);

			return elapsedTime;
		}
	}
}
