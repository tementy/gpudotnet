//
// ClBinaryBuilder.cs
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
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using OpenCL.Net;
using UniGPU.Core;
using UniGPU.Core.IR;
using UniGPU.Core.Elf;
using UniGPU.Core.Utils;

namespace UniGPU
{
	public static class ClBinaryBuilder
	{
		private static byte[] ToAMDClBinary(this Program irprog, Cl.Device device)
		{
			// create OpenCL stub before registers naming

			string clstub =	string.Join("\n", irprog.Kernels.Select(kernel => string.Format(
				"__kernel void {0}({1}) {{ {2} }}",
				kernel.Name,
				string.Join(", ", kernel.FormalParameters.Select(
				(fp, idx) => {
				switch (fp.StateSpace)
				{
				case StateSpaces.GLOBAL:
					return string.Format("__global {0}* p{1}", fp.UnderlyingType.FormatC(), idx);
				case StateSpaces.SHARED:
					return string.Format("__local {0}* p{1}", fp.UnderlyingType.FormatC(), idx);
				case StateSpaces.CONSTANT:
					return string.Format("__constant {0}* p{1}", fp.UnderlyingType.FormatC(), idx);
				case StateSpaces.REG:
				default:
					return string.Format("{0} p{1}", fp.UnderlyingType.FormatC(), idx);
				}})),
				string.Join(" ", kernel.FormalParameters.Where(
				fp => fp.StateSpace != StateSpaces.REG && fp.StateSpace != StateSpaces.CONSTANT).Select(
				fp => string.Format("*p{0} = 0;", kernel.FormalParameters.IndexOf(fp)))))));

			// create template binary from OpenCL stub

			Cl.ErrorCode error;

			Cl.Context context = Cl.CreateContext(null, 1, new[] { device }, null, IntPtr.Zero, out error);
			clSafeCall(error);

			Cl.Program program = Cl.CreateProgramWithSource(context, 1, new[] { clstub }, null, out error);
			clSafeCall(error);

			clSafeCall(Cl.BuildProgram(program, 1, new[] { device },
				"-fno-bin-source -fno-bin-llvmir -fno-bin-exe -fbin-amdil", null, IntPtr.Zero));
			Cl.BuildStatus status = Cl.GetProgramBuildInfo(program, device, Cl.ProgramBuildInfo.Status, out error).CastTo<Cl.BuildStatus>();
			if (status != Cl.BuildStatus.Success)
				throw new Exception(status.ToString());

			Cl.InfoBuffer binarySizes = Cl.GetProgramInfo(program, Cl.ProgramInfo.BinarySizes, out error);
			clSafeCall(error);
			Cl.InfoBufferArray binaries = new Cl.InfoBufferArray(
				binarySizes.CastToEnumerable<IntPtr>(Enumerable.Range(0, 1)).Select(sz => new Cl.InfoBuffer(sz)).ToArray());
			IntPtr szRet;
			clSafeCall(Cl.GetProgramInfo(program, Cl.ProgramInfo.Binaries, binaries.Size, binaries, out szRet));

			program.Dispose();
			context.Dispose();

			// inject generated code into the elf binary

			LinkingView elf = new LinkingView(binaries[0].CastToArray<byte>(binarySizes.CastTo<IntPtr>(0).ToInt32()));
			SymTabSection symtab = (SymTabSection)elf[".symtab"];                				
            Section amdil = elf[".amdil"];
			Section rodata = elf[".rodata"];

			MemoryStream amdilcode = new MemoryStream();
			foreach (Kernel kernel in irprog.Kernels)
			{
				SymbolWrapper _metadata = symtab["__OpenCL_" + kernel.Name + "_metadata"];

				string[] str_metadata = Marshal.PtrToStringAnsi(Marshal.UnsafeAddrOfPinnedArrayElement(
					rodata.Data, (int)_metadata.st_value), (int)_metadata.st_size).Split('\n');

				int setup_id = (from line in str_metadata let prms = line.Split(':')
				                where prms[0] == ";uniqueid" select int.Parse(prms[1])).Single();

				int raw_uav_id = (from line in str_metadata let prms = line.Split(':')
				                  where prms[0] == ";uavid" select int.Parse(prms[1])).Single();

				SymbolWrapper _fmetadata = symtab["__OpenCL_" + kernel.Name + "_fmetadata"];

				string[] str_fmetadata = Marshal.PtrToStringAnsi(Marshal.UnsafeAddrOfPinnedArrayElement(
					rodata.Data, (int)_fmetadata.st_value), (int)_fmetadata.st_size).Split('\n');

				int func_id = (from line in str_fmetadata let prms = line.Split(':')
				               where prms[0] == ";uniqueid" select int.Parse(prms[1])).Single();

				// ugly, i know!!!
				raw_uav_id = Math.Max(raw_uav_id, 11);
				int arena_uav_id = raw_uav_id;

				byte[] code = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, Encoding.Unicode.GetBytes(
					irprog.ToAMDIL(kernel.Name, setup_id, func_id, raw_uav_id, arena_uav_id)));

				SymbolWrapper _amdil = symtab["__OpenCL_" + kernel.Name + "_amdil"];

				_amdil.st_value = (uint)amdilcode.Position;
				_amdil.st_size = (uint)code.Length;

				foreach (byte b in code)
					amdilcode.WriteByte(b);
			}

			amdil.Data = amdilcode.ToArray();

			return elf.BuildBinary();
		}

		private static byte[] ToNVIDIAClBinary(this Program irprog, Cl.Device device)
		{
			Cl.ErrorCode error;

			int cchi = Cl.GetDeviceInfo(device, Cl.DeviceInfo.ComputeCapabilityMajorNV, out error).CastTo<int>();
			clSafeCall(error);
			int cclo = Cl.GetDeviceInfo(device, Cl.DeviceInfo.ComputeCapabilityMinorNV, out error).CastTo<int>();
			clSafeCall(error);

			return Encoding.Convert(
				Encoding.Unicode, Encoding.ASCII,
				Encoding.Unicode.GetBytes(irprog.ToPTX(string.Format("sm_{0}{1}", cchi, cclo))));
		}

		public static byte[] ToGPUClBinary(this Program irprog, Cl.Device device)
		{
			Cl.ErrorCode error;
			Cl.Platform platform = Cl.GetDeviceInfo(device, Cl.DeviceInfo.Platform, out error).CastTo<Cl.Platform>();
			clSafeCall(error);
			string platformName = Cl.GetPlatformInfo(platform, Cl.PlatformInfo.Name, out error).ToString();
			clSafeCall(error);

			switch (platformName)
			{
				case "NVIDIA CUDA":
					return irprog.ToNVIDIAClBinary(device);
				case "AMD Accelerated Parallel Processing":
					return irprog.ToAMDClBinary(device);
				default:
					throw new NotSupportedException(platformName);
			}
		}

		public static Cl.Program ToGPUClProgram(this Program irprog, Cl.Device device, Cl.Context context)
		{
			byte[] code = irprog.ToGPUClBinary(device);

			Cl.ErrorCode error;

			Cl.ErrorCode[] binariesStatus = { Cl.ErrorCode.InvalidBinary };

			Cl.Program program = Cl.CreateProgramWithBinary(
				context,
				1,
				new[] { device },
				new IntPtr[] { (IntPtr)code.Length }, 
				new Cl.InfoBufferArray(new Cl.InfoBuffer(code)),
				binariesStatus,
				out error
			);
			clSafeCall(error);
			clSafeCall(binariesStatus[0]);

			return program;
		}

		private static void clSafeCall(Cl.ErrorCode error)
		{
			if (Cl.ErrorCode.Success != error)
				throw new Exception(error.ToString());
		}
	}
}

