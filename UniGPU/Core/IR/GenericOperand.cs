//
// GenericOperand.cs
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
using UniGPU.Core.Utils;

namespace UniGPU.Core.IR
{
	public abstract class GenericOperand
	{
		public abstract Type DataType { get; }
		
		public string Name { get; set; }
		
		public override string ToString()
		{
			return (Name == null) ? "?" : Name;
		}
	}
	
	public class ImmediateValue : GenericOperand
	{
		public override Type DataType { get { return Value.GetType(); } }
		
		public ValueType Value { get; private set; }
		
		public override string ToString()
		{
			return (Value is float) ? string.Format("0F{0:X8}", BitConverter.ToInt32(BitConverter.GetBytes((float)Value), 0)) :
				(Value is double) ? string.Format("0D{0:X16}", BitConverter.DoubleToInt64Bits((double)Value)) : 
					Value.ToString();
		}
		
		public ImmediateValue(ValueType value)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			else
				Value = value;
		}
	}
	
	public enum StateSpaces
	{
		GLOBAL,
		CONSTANT,
		SHARED,
		REG
	}
	
	public class VirtualRegister : GenericOperand
	{
		public override Type DataType
		{
			get
			{
				return (StateSpace != StateSpaces.REG) ? typeof(int) : UnderlyingType;
			}
		}
		
		public Type UnderlyingType { get; private set; }
		
		public StateSpaces StateSpace { get; private set; }
		
		public VirtualRegister(Type type, StateSpaces stsp = StateSpaces.REG)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			
			if (!type.IsValueType)
				throw new ArgumentException("Only value types are permitted", "type");
			
			UnderlyingType = type;
			StateSpace = stsp;
		}
	}
	
	public enum PassingStyles
	{
		VAL,
		REF,
		OUT
	}
	
	public class FormalParameter : VirtualRegister
	{
		public PassingStyles PassingStyle { get; private set; }
		
		public FormalParameter(Type type, StateSpaces stsp = StateSpaces.REG, PassingStyles style = PassingStyles.VAL) :
			base(type, stsp)
		{
			PassingStyle = style;
		}
		
		public virtual FormalParameter Clone()
		{
			return new FormalParameter(UnderlyingType, StateSpace, PassingStyle) { Name = Name };
		}
	}

	public enum PredefinedValues
	{
		ThreadIdxX,
		ThreadIdxY,
		ThreadIdxZ,
		BlockDimX,
		BlockDimY,
		BlockDimZ,
		BlockIdxX,
		BlockIdxY,
		BlockIdxZ,
		GridDimX,
		GridDimY,
		GridDimZ
	}
	
	public sealed class SpecialRegister : GenericOperand
	{
		public override Type DataType { get { return typeof(uint); } }
		
		public PredefinedValues Value { get; private set; }
		
		internal SpecialRegister(PredefinedValues val)
		{
			Value = val;
		}
		
		public static IList<SpecialRegister> CreatePool()
		{
			List<SpecialRegister> srlist = new List<SpecialRegister>();
			foreach (PredefinedValues type in Enum.GetValues(typeof(PredefinedValues)))
				srlist.Add(new SpecialRegister(type));
			return srlist.AsReadOnly();
		}
	}
}
