//
// Utils.cs
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
using System.Runtime.InteropServices;
using System.Reflection;

namespace UniGPU.Core.Utils
{
    public interface ILabeled<LabelT, ParamT>
    {
        LabelT GetLabel(ParamT parameter);
    }

    public class ListSearchByLabel<ElemT, LabelT, ParamT> where ElemT : ILabeled<LabelT, ParamT>
    {
        protected LabelT TargetLabel { get; set; }
        protected ParamT Parameter { get; set; }

        protected ListSearchByLabel(LabelT targetLabel, ParamT parameter)
        {
            TargetLabel = targetLabel;
            Parameter = parameter;
        }

        protected bool TestPredicate(ElemT obj)
        {
            return obj.GetLabel(Parameter).Equals(TargetLabel);
        }

        public static Predicate<ElemT> GetPredicate(LabelT targetLabel, ParamT parameters)
        {
            return new ListSearchByLabel<ElemT, LabelT, ParamT>(targetLabel, parameters).TestPredicate;
        }
    }
	
    public interface ILabeled<LabelT>
    {
        LabelT Label { get; }
    }

    public class ListSearchByLabel<ElemT, LabelT> where ElemT : ILabeled<LabelT>
    {
        protected LabelT TargetLabel { get; private set; }

        protected ListSearchByLabel(LabelT targetLabel)
        {
            TargetLabel = targetLabel;
        }

        protected bool TestPredicate(ElemT obj)
        {
            return obj.Label.Equals(TargetLabel);
        }

        public static Predicate<ElemT> GetPredicate(LabelT targetLabel)
        {
            return new ListSearchByLabel<ElemT, LabelT>(targetLabel).TestPredicate;
        }
    }
	
	public static class ListSearchByLabelEx
	{
		public static ElemT FindByLabel<ElemT, LabelT, ParamT> (this List<ElemT> self, LabelT targetLabel, ParamT parameters)
			where ElemT : ILabeled<LabelT, ParamT>
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			return self.Find(ListSearchByLabel<ElemT, LabelT, ParamT>.GetPredicate(targetLabel, parameters));
		}
		
		public static int FindIndexByLabel<ElemT, LabelT, ParamT> (this List<ElemT> self, LabelT targetLabel, ParamT parameters)
			where ElemT : ILabeled<LabelT, ParamT>
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			return self.FindIndex(ListSearchByLabel<ElemT, LabelT, ParamT>.GetPredicate(targetLabel, parameters));
		}
		
		public static ElemT FindByLabel<ElemT, LabelT> (this List<ElemT> self, LabelT targetLabel)
			where ElemT : ILabeled<LabelT>
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			return self.Find(ListSearchByLabel<ElemT, LabelT>.GetPredicate(targetLabel));
		}
		
		public static int FindIndexByLabel<ElemT, LabelT> (this List<ElemT> self, LabelT targetLabel)
			where ElemT : ILabeled<LabelT>
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			return self.FindIndex(ListSearchByLabel<ElemT, LabelT>.GetPredicate(targetLabel));
		}
	}
	
    public abstract class UnsafeResourceOwner : IDisposable
    {
        private bool disposed = false;

        protected abstract void ReleaseManaged();
        protected abstract void ReleaseUnmanaged();

        public void Dispose()
        {
            DoDispose(true);
            GC.SuppressFinalize(this);
        }

        ~UnsafeResourceOwner()
        {
            DoDispose(false);
        }

        private void DoDispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                    ReleaseManaged();
                ReleaseUnmanaged();
				disposed = true;
            }
        }
    }

    public class HGlobalString : UnsafeResourceOwner
    {
        public IntPtr UnsafePointer { get; private set; }

        protected HGlobalString(IntPtr ptr)
        {
            UnsafePointer = ptr;
        }

        protected override void ReleaseManaged()
        {
        }

        protected override void ReleaseUnmanaged()
        {
            Marshal.FreeHGlobal(UnsafePointer);
            UnsafePointer = IntPtr.Zero;
        }

        public static HGlobalString AllocateAnsi(string ansiString)
        {
            return new HGlobalString(Marshal.StringToHGlobalAnsi(ansiString));
        }

        public static HGlobalString AllocateUni(string uniString)
        {
            return new HGlobalString(Marshal.StringToHGlobalUni(uniString));
        }

        public static HGlobalString AllocateAuto(string anyString)
        {
            return new HGlobalString(Marshal.StringToHGlobalAuto(anyString));
        }
    }

    public static class Util
    {
		public static readonly IList<Type> UIntTypes = new List<Type>
		{
			typeof(byte),
			typeof(ushort),
			typeof(uint),
			typeof(ulong)
		}.AsReadOnly();
		public static readonly IList<Type> SIntTypes = new List<Type>
		{
			typeof(sbyte),
			typeof(short),
			typeof(int),
			typeof(long)
		}.AsReadOnly();
		public static readonly IList<Type> RealTypes = new List<Type>
		{
			typeof(float),
			typeof(double)
		}.AsReadOnly();
		public static readonly IEnumerable<Type> IntegralTypes = SIntTypes.Concat(UIntTypes);
		public static readonly IEnumerable<Type> NumericTypes = IntegralTypes.Concat(RealTypes);
		
		public static IEnumerable<Type> CompatibleTypes(this Type src)
		{
			return src == typeof(bool) ? typeof(sbyte).CompatibleTypes() :
				SIntTypes.SkipWhile(type => type != src).Concat(
				UIntTypes.SkipWhile(type => type != src).Concat(
				RealTypes.Where(type => type == src)));
		}
		
		public static int SizeOf(this Type src)
		{
			return src == typeof(bool) ? sizeof(bool) : Marshal.SizeOf(src);
		}
		
        public static int FieldOffset<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        public static long WideToFold(long value, long div)
        {
            return value + (div - value % div) % div;
        }
		
		public static IEnumerable<T> Add<T>(this IEnumerable<T> enumerable, T value)
		{
			foreach (T elem in enumerable)
				yield return elem;
			yield return value;
		}
		
		public static int IndexOf<T>(this IEnumerable<T> obj, T value)
		{
			return obj.IndexOf(value, null);
		}
		
		public static int IndexOf<T>(this IEnumerable<T> obj, T value, IEqualityComparer<T> comparer)
		{
			comparer = comparer ?? EqualityComparer<T>.Default;
			var found = obj.Select((a, i) => new { a, i }).FirstOrDefault(x => comparer.Equals(x.a, value));
			return found == null ? -1 : found.i;
		}
		
		public static void CheckArgumentType(IEnumerable<Type> allowed, Type actual, string name)
		{
			if (!allowed.Contains(actual))
				throw new ArgumentException(string.Format("Data type must be one of: {0}, but not {1}.",
					string.Join(", ", allowed.Select(type => type.Format())), actual.Format()), name);
		}
		
		public static void CheckArgumentType(Type allowed, Type actual, string name)
		{
			if (allowed != actual)
				throw new ArgumentException(string.Format("Data type must be {0}, but not {1}.",
					allowed.Format(), actual.Format()), name);
		}
		
		public static string Format(this Type type)
		{
			switch (type.ToString())
			{
			case "System.SByte":
				return "sbyte";
			case "System.Int16":
				return "short";
			case "System.Int32":
				return "int";
			case "System.Int64":
				return "long";
			case "System.Byte":
				return "byte";
			case "System.UInt16":
				return "ushort";
			case "System.UInt32":
				return "uint";
			case "System.UInt64":
				return "ulong";
			case "System.Single":
				return "float";
			case "System.Double":
				return "double";
			case "System.Boolean":
				return "bool";
			default:
				return type.ToString();
			}
		}

		public static string FormatC(this Type type)
		{
			switch (type.ToString())
			{
			case "System.SByte":
				return "signed char";
			case "System.Int16":
				return "short";
			case "System.Int32":
				return "int";
			case "System.Int64":
				return "long";
			case "System.Byte":
				return "unsigned char";
			case "System.UInt16":
				return "unsigned short";
			case "System.UInt32":
				return "unsigned int";
			case "System.UInt64":
				return "unsigned long";
			case "System.Single":
				return "float";
			case "System.Double":
				return "double";
			case "System.Boolean":
				return "char";
			default:
				return type.ToString();
			}
		}
    }
}

