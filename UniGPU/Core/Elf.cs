//
// Elf.cs
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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UniGPU.Core.Utils;

namespace UniGPU.Core.Elf
{
    using Elf32_Addr = UInt32;
    using Elf32_Half = UInt16;
    using Elf32_Off = UInt32;
    using Elf32_Word = UInt32;

    /// <summary>
    /// ELF Header
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class Elf32_Ehdr
    {
        /// <summary>
        /// Magic number
        /// </summary>
        public UInt32 ei_mag;
        /// <summary>
        /// File class
        /// </summary>
        public byte ei_class;
        /// <summary>
        /// Data encoding
        /// </summary>
        public byte ei_data;
        /// <summary>
        /// File version
        /// </summary>
        public byte ei_version;
        /// <summary>
        /// Reserved padding
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public byte[] ei_pad;        
        /// <summary>
        /// This member identifies the object file type.
        /// </summary>        
        public Elf32_Half e_type;
        /// <summary>
        /// This member’s value specifies the required architecture for an individual file.
        /// </summary>
        public Elf32_Half e_machine;
        /// <summary>
        /// This member identifies the object file version. The value 1 signifies the original
        /// file format; extensions will create new versions with higher numbers.
        /// </summary>
        public Elf32_Word e_version;
        /// <summary>
        /// This member gives the virtual address to which the system first transfers control, thus
        /// starting the process. If the file has no associated entry point, this member holds zero.
        /// </summary>
        public Elf32_Addr e_entry;
        /// <summary>
        /// This member holds the program header table’s file offset in bytes. If the file has no
        /// program header table, this member holds zero.
        /// </summary>
        public Elf32_Off e_phoff;
        /// <summary>        
        /// This member holds the section header table’s file offset in bytes. If the file has no 
        /// section header table, this member holds zero.
        /// </summary>
        public Elf32_Off e_shoff;
        /// <summary>
        /// This member holds processor-specific flags associated with the file.
        /// </summary>
        public Elf32_Word e_flags;
        /// <summary>
        /// This member holds the ELF header’s size in bytes.
        /// </summary>
        public Elf32_Half e_ehsize;
        /// <summary>
        /// This member holds the size in bytes of one entry in the file’s program header table; all
        /// entries are the same size.
        /// </summary>
        public Elf32_Half e_phentsize;
        /// <summary>
        /// This member holds the number of entries in the program header table. Thus the product
        /// of e_phentsize and e_phnum gives the table’s size in bytes. If a file has no program
        /// header table, e_phnum holds the value zero.
        /// </summary>
        public Elf32_Half e_phnum;
        /// <summary>
        /// This member holds a section header’s size in bytes. A section header is one entry in
        /// the section header table; all entries are the same size.
        /// </summary>
        public Elf32_Half e_shentsize;
        /// <summary>
        /// This member holds the number of entries in the section header table. Thus the product
        /// of e_shentsize and e_shnum gives the section header table’s size in bytes. If a file
        /// has no section header table, e_shnum holds the value zero.
        /// </summary>
        public Elf32_Half e_shnum;
        /// <summary>
        /// This member holds the section header table index of the entry associated with the section
        /// name string table.
        /// </summary>
        public Elf32_Half e_shstrndx;
    }

    /// <summary>
    /// Section Header
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class Elf32_Shdr
    {
        /// <summary>
        /// This member specifies the name of the section. Its value is an index into the section
        /// header string table section, giving the location of a nullterminated string.
        /// </summary>
        public Elf32_Word sh_name;
        /// <summary>
        /// This member categorizes the section’s contents and semantics.
        /// </summary>
        public Elf32_Word sh_type;
        /// <summary>
        /// Sections support 1-bit flags that describe miscellaneous attributes.
        /// </summary>
        public Elf32_Word sh_flags;
        /// <summary>
        /// If the section will appear in the memory image of a process, this member gives the address
        /// at which the section’s first byte should reside. Otherwise, the member contains 0.
        /// </summary>
        public Elf32_Addr sh_addr;
        /// <summary>
        /// This member’s value gives the byte offset from the beginning of the file to the first
        /// byte in the section.
        /// </summary>
        public Elf32_Off sh_offset;
        /// <summary>
        /// This member gives the section’s size in bytes.
        /// </summary>
        public Elf32_Word sh_size;
        /// <summary>
        /// This member holds a section header table index link, whose interpretation depends
        /// on the section type.
        /// </summary>
        public Elf32_Word sh_link;
        /// <summary>
        /// This member holds extra information, whose interpretation depends on the section type.
        /// </summary>
        public Elf32_Word sh_info;
        /// <summary>
        /// Some sections have address alignment constraints. For example, if a section holds a
        /// doubleword, the system must ensure doubleword alignment for the entire section.
        /// That is, the value of sh_addr must be congruent to 0, modulo the value of
        /// sh_addralign. Currently, only 0 and positive integral powers of two are allowed.
        /// Values 0 and 1 mean the section has no alignment constraints.
        /// </summary>
        public Elf32_Word sh_addralign;
        /// <summary>
        /// Some sections hold a table of fixed-size entries, such as a symbol table. For such a section,
        /// this member gives the size in bytes of each entry. The member contains 0 if the section does 
        /// not hold a table of fixed-size entries.
        /// </summary>
        public Elf32_Word sh_entsize;
    }

    public enum SectionTypes
    {
        SHT_NULL = 0,
        SHT_PROGBITS = 1,
        SHT_SYMTAB = 2,
        SHT_STRTAB = 3,
        SHT_RELA = 4,
        SHT_HASH = 5,
        SHT_DYNAMIC = 6,
        SHT_NOTE = 7,
        SHT_NOBITS = 8,
        SHT_REL = 9,
        SHT_SHLIB = 10,
        SHT_DYNSYM = 11,
        SHT_LOPROC = 0x70000000,
        SHT_HIPROC = 0x7fffffff,
        SHT_LOUSER = unchecked((int)0x80000000),
        SHT_HIUSER = unchecked((int)0xffffffff)
    }

    public class Section : ILabeled<string>
    {
        protected Elf32_Shdr header;
        protected byte[] data;
        protected LinkingView owner;

        public Elf32_Shdr Header
        {
            get
            {
                return header;
            }
        }

        public byte[] Data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
                header.sh_size = (uint)data.Length;
            }
        }

        public LinkingView Owner
        {
            get
            {
                return owner;
            }
        }

        public Section(Elf32_Shdr header, byte[] data, LinkingView owner)
        {
            if (header != null)
                this.header = header;
            else
                throw new ArgumentNullException("header");

            if (data != null)
                this.data = data;
            else
                throw new ArgumentNullException("data");

            if (owner != null)
                this.owner = owner;
            else
                throw new ArgumentNullException("owner");
        }

        public T ReadData<T>(int offset) where T : struct
        {
            if (offset >= 0 && offset < Data.Length)
                return (T)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(Data, offset), typeof(T));
            else
                throw new ArgumentOutOfRangeException();
        }

        public void WriteData<T>(int offset, T value) where T : struct
        {
            if (offset >= 0 && offset < Data.Length)
                Marshal.StructureToPtr(value, Marshal.UnsafeAddrOfPinnedArrayElement(Data, offset), false);
            else
                throw new ArgumentOutOfRangeException();
        }

        public string AnsiStringByOffset(int offset)
        {
            if (offset >= 0 && offset < Data.Length)
                return Marshal.PtrToStringAnsi(Marshal.UnsafeAddrOfPinnedArrayElement(Data, offset));
            else
                throw new ArgumentOutOfRangeException();
        }

        public string AnsiStringByOffset(int offset, int len)
        {
            if (offset >= 0 && offset < Data.Length)
                if (offset + len <= Data.Length)
                    return Marshal.PtrToStringAnsi(Marshal.UnsafeAddrOfPinnedArrayElement(Data, offset), len);
                else
                    throw new ArgumentOutOfRangeException("len");
            else
                throw new ArgumentOutOfRangeException("offset");
        }

        public void Resize(int sh_size)
        {
            if (Header.sh_size != sh_size)
            {
                Array.Resize<byte>(ref data, sh_size);
                Header.sh_size = (uint)sh_size;
            }
        }

        public string Label
        {
            get
            {
                return Owner.Names.AnsiStringByOffset((int)Header.sh_name);
            }
        }
    }

    /// <summary>
    /// Wraps "Symbol Table Entry" structure in underlying memory buffer
    /// </summary>
    public class SymbolWrapper : ILabeled<string>
    {
        protected SymTabSection owner;
        protected int index;

        public SymTabSection Owner
        {
            get
            {
                return owner;
            }
            protected set
            {
                if (value != null)
                    owner = value;
                else
                    throw new ArgumentNullException();
            }
        }

        public int Index
        {
            get
            {
                return index;
            }
            set
            {
                if (value >= 0 && value < Owner.SymbolCount)
                    index = value;
                else
                    throw new ArgumentOutOfRangeException();
            }
        }        

        protected internal SymbolWrapper(SymTabSection owner, int index)
        {
            Owner = owner;
            Index = index;
        }

        private T ReadField<T>(int fieldOffset) where T : struct
        {
            return Owner.ReadData<T>((int)(Index * Owner.Header.sh_entsize + fieldOffset));
        }

        private void WriteField<T>(int fieldOffset, T value) where T : struct
        {
            Owner.WriteData<T>((int)(Index * Owner.Header.sh_entsize + fieldOffset), value);
        }

        /// <summary>
        /// This member holds an index into the object file’s symbol string table, which holds the
        /// character representations of the symbol names. If the value is non-zero, it represents a
        /// string table index that gives the symbol name. Otherwise, the symbol table entry has no
        /// name.
        /// </summary>
        public Elf32_Word st_name
        {
            get
            {
                return ReadField<Elf32_Word>(0);
            }
            set
            {
                WriteField<Elf32_Word>(0, value);
            }
        }

        /// <summary>
        /// This member gives the value of the associated symbol. Depending on the context, this
        /// may be an absolute value, an address, etc.
        /// </summary>
        public Elf32_Addr st_value
        {
            get
            {
                return ReadField<Elf32_Addr>(sizeof(Elf32_Word));
            }
            set
            {
                WriteField<Elf32_Addr>(sizeof(Elf32_Word), value);
            }
        }

        /// <summary>
        /// Many symbols have associated sizes. For example, a data object’s size is the number of
        /// bytes contained in the object. This member holds 0 if the symbol has no size or an
        /// unknown size.
        /// </summary>
        public Elf32_Word st_size
        {
            get
            {
                return ReadField<Elf32_Word>(sizeof(Elf32_Word) + sizeof(Elf32_Addr));
            }
            set
            {
                WriteField<Elf32_Word>(sizeof(Elf32_Word) + sizeof(Elf32_Addr), value);
            }
        }

        /// <summary>
        /// This member specifies the symbol’s type and binding attributes.
        /// </summary>
        public byte st_info
        {
            get
            {
                return ReadField<byte>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr));
            }
            set
            {
                WriteField<byte>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr), value);
            }
        }

        /// <summary>
        /// This member currently holds 0 and has no defined meaning.
        /// </summary>
        public byte st_other
        {
            get
            {
                return ReadField<byte>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr) + sizeof(byte));
            }
            set
            {
                WriteField<byte>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr) + sizeof(byte), value);
            }
        }

        /// <summary>
        /// Every symbol table entry is ‘‘defined’’ in relation to some section; this member holds the
        /// relevant section header table index.
        /// </summary>
        public Elf32_Half st_shndx
        {
            get
            {
                return ReadField<Elf32_Half>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr) + 2 * sizeof(byte));
            }
            set
            {
                WriteField<Elf32_Half>(2 * sizeof(Elf32_Word) + sizeof(Elf32_Addr) + 2 * sizeof(byte), value);
            }
        }

        public string Label
        {
            get
            {
                return Owner.Names.AnsiStringByOffset((int)st_name);
            }
        }
    }

    public sealed class SymTabSection : Section
    {
        public SymTabSection(Elf32_Shdr hdr, byte[] data, LinkingView owner)
            : base(hdr, data, owner)
        {
        }

        public StrTabSection Names
        {
            get
            {
                return (StrTabSection)Owner[(int)Header.sh_link];
            }
        }

        public int SymbolCount
        {
            get
            {
                return (int)(Header.sh_size / Header.sh_entsize);
            }
        }

        public SymbolWrapper this[int symbolIndex]
        {
            get
            {
                return new SymbolWrapper(this, symbolIndex);
            }
        }

        public SymbolWrapper this[string symbolName]
        {
            get
            {
                if (SymbolCount == 0)
                    return null;
                else
                {
                    SymbolWrapper target = new SymbolWrapper(this, 0);
                    while (!target.Label.Equals(symbolName) && target.Index < SymbolCount) target.Index++;
                    return (target.Index < SymbolCount) ? target : null;
                }
            }
        }

        public void SwapSymbols(int idx1, int idx2)
        {
            if (idx1 != idx2)
            {
                byte[] temp = new byte[Header.sh_entsize];
                Array.Copy(Data, idx2 * Header.sh_entsize, temp, 0, Header.sh_entsize);
                Array.Copy(Data, idx1 * Header.sh_entsize, Data, idx2 * Header.sh_entsize, Header.sh_entsize);
                Array.Copy(temp, 0, Data, idx1 * Header.sh_entsize, Header.sh_entsize);
            }
        }

        public void SwapSymbols(string name1, string name2)
        {
            if (!name1.Equals(name2))
                SwapSymbols(this[name1].Index, this[name2].Index);
        }

        public void DeleteSymbol(int symbolIndex)
        {
            if (symbolIndex >= 0 && symbolIndex < SymbolCount)
            {
                if (symbolIndex < SymbolCount - 1)
                    Array.Copy(Data, (symbolIndex + 1) * Header.sh_entsize, Data, symbolIndex * Header.sh_entsize, SymbolCount - symbolIndex - 1);
                Resize((int)(Header.sh_entsize * (SymbolCount - 1)));
            }
            else
                throw new ArgumentOutOfRangeException();
        }

        public bool DeleteSymbol(string name)
        {
            SymbolWrapper target = this[name];
            if (target != null)
            {
                DeleteSymbol(target.Index);
                return true;
            }
            else 
                return false;
        }

        public SymbolWrapper InsertSymbol(int position)
        {
            if (position >= SymbolCount)
                Resize((int)(Header.sh_entsize * (position + 1)));
            else
            {
                Resize((int)(Header.sh_entsize * (SymbolCount + 1)));
                Array.Copy(Data, position * Header.sh_entsize, Data, (position + 1) * Header.sh_entsize, 
                    (SymbolCount - 1 - position) * Header.sh_entsize);
            }
            return this[position];
        }
    }

    public class StrTabSection : Section
    {
        protected List<int> offsets = new List<int>();

        public StrTabSection(Elf32_Shdr hdr, byte[] data, LinkingView owner)
            : base(hdr, data, owner)
        {
            int stringStart = 0;
            for (int i = 0; i < Header.sh_size; i++)
                if (Data[i] == 0)
                {
                    offsets.Add(stringStart);
                    stringStart = i + 1;
                }
        }

        public int StringCount
        {
            get
            {
                return offsets.Count;
            }
        }

        public int StringOffset(int stringIndex)
        {
            return offsets[stringIndex];
        }

        public int StringIndex(int stringOffset)
        {
            return offsets.IndexOf(stringOffset);
        }

        public void InsertString(int position, string value)
        {
            if (position > StringCount)
                throw new ArgumentOutOfRangeException("position");

            int stringOffset = (position < StringCount) ? offsets[position] : (int)Header.sh_size;
            int tailSize = (int)(Header.sh_size - stringOffset);
            
            Resize((int)(Header.sh_size + value.Length + 1));
            if (tailSize > 0)
            {
                Array.Copy(Data, stringOffset, Data, stringOffset + value.Length + 1, tailSize);
                for (int i = position; i < StringCount; i++)
                    offsets[i] += value.Length + 1;
            }

            offsets.Insert(position, stringOffset);
            IntPtr ptr = Marshal.StringToHGlobalAnsi(value);
            Marshal.Copy(ptr, Data, stringOffset, value.Length + 1);
            Marshal.FreeHGlobal(ptr);
        }

        public void DeleteString(int stringIndex)
        {
            if (stringIndex >= 0 && stringIndex < StringCount)
            {
                if (stringIndex < StringCount - 1)
                {
                    Array.Copy(Data, offsets[stringIndex + 1], Data, offsets[stringIndex], Header.sh_size - offsets[stringIndex + 1]);
                    int delta = offsets[stringIndex + 1] - offsets[stringIndex];
                    for (int i = stringIndex + 1; i < StringCount; i++)
                        offsets[i] -= delta;                    
                    Resize((int)(Header.sh_size - delta));
                }
                else
                {
                    Resize(offsets[stringIndex]);
                }
                offsets.RemoveAt(stringIndex);                
            }
            else
                throw new ArgumentOutOfRangeException();
        }

        public string this[int stringIndex]
        {
            get
            {
                return AnsiStringByOffset(offsets[stringIndex]);
            }
            set
            {
                int stringOffset = offsets[stringIndex];
                int tailPos = (stringIndex < StringCount - 1) ? offsets[stringIndex + 1] : (int)Header.sh_size;
                int tailSize = (int)(Header.sh_size - tailPos);
                int deltaSize = tailPos - stringOffset - value.Length - 1;
                
                if (deltaSize != 0)
                {
                    if (deltaSize > 0)
                        Resize((int)(Header.sh_size + deltaSize));
                    if (tailSize > 0)
                    {
                        Array.Copy(Data, offsets[stringIndex + 1], Data, offsets[stringIndex + 1] + deltaSize, tailSize);
                        for (int i = stringIndex + 1; i < StringCount; i++)
                            offsets[i] += deltaSize;
                    }
                    if (deltaSize < 0)
                        Resize((int)(Header.sh_size + deltaSize));
                }

                IntPtr ptr = Marshal.StringToHGlobalAnsi(value);
                Marshal.Copy(ptr, Data, stringOffset, value.Length + 1);
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public class LinkingView
    {
        protected Elf32_Ehdr header = new Elf32_Ehdr();
        protected List<Section> sections = new List<Section>();

        public Elf32_Ehdr Header
        { 
            get
            {
                return header;
            }
        }

        public List<Section> Sections
        {
            get
            {
                return sections;
            }
        }

        public StrTabSection Names
        {
            get
            {
                return (StrTabSection)this[(int)Header.e_shstrndx];
            }
        }

        public LinkingView()
        {
            header = new Elf32_Ehdr();
            sections = new List<Section>();
        }

        public LinkingView(byte[] binaryData) : this()
        {
            Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(binaryData, 0), header);            
            for (int section = 0; section < Header.e_shnum; section++)
            {
                Elf32_Shdr sectionHeader = (Elf32_Shdr)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(binaryData, 
                    (int)(Header.e_shoff + section * Header.e_shentsize)), typeof(Elf32_Shdr));
                byte[] sectionData = new byte[sectionHeader.sh_size];
                Array.Copy(binaryData, sectionHeader.sh_offset, sectionData, 0, sectionHeader.sh_size);
                switch (sectionHeader.sh_type)
                {
                    case (uint)SectionTypes.SHT_STRTAB: Sections.Add(new StrTabSection(sectionHeader, sectionData, this)); break;
                    case (uint)SectionTypes.SHT_SYMTAB: Sections.Add(new SymTabSection(sectionHeader, sectionData, this)); break;
                    default: Sections.Add(new Section(sectionHeader, sectionData, this)); break;
                }
            }
        }

        protected byte[] CollectSectionsData(ref int shoff, uint offset, int sectionIndex)
        {
            if (sectionIndex < Sections.Count)
            {
                Section section = Sections[sectionIndex];
                Elf32_Shdr header = section.Header;
                header.sh_offset = (header.sh_addralign > 1) ? (uint)Util.WideToFold(offset, header.sh_addralign) : offset;
                byte[] binaryData = CollectSectionsData(ref shoff, header.sh_offset + header.sh_size, sectionIndex + 1);
                Array.Copy(section.Data, 0, binaryData, header.sh_offset, header.sh_size);
                Marshal.StructureToPtr(header, Marshal.UnsafeAddrOfPinnedArrayElement(binaryData, (int)(shoff + sectionIndex * Header.e_shentsize)), false);
                return binaryData;
            }
            else
            {
                shoff = (int)Util.WideToFold(offset, 4);
                return new byte[shoff + Sections.Count * Header.e_shentsize];
            }
        }

        public byte[] BuildBinary()
        {
            int shoff = 0;
            byte[] binaryData = CollectSectionsData(ref shoff, Header.e_ehsize, 0);
            Header.e_shnum = (Elf32_Half)Sections.Count;
            Header.e_shoff = (Elf32_Word)shoff;
            Marshal.StructureToPtr(Header, Marshal.UnsafeAddrOfPinnedArrayElement(binaryData, 0), false);
            return binaryData;
        }

        public string SectionName(int sectionIndex)
        {
            return Sections[sectionIndex].Label;
        }

        public int SectionIndex(string sectionName)
        {
            return Sections.FindIndexByLabel(sectionName);
        }

        public Section this[int sectionIndex]
        {
            get
            {
                return Sections[sectionIndex];
            }
        }

        public Section this[string sectionName]
        {
            get
            {
                return Sections.FindByLabel(sectionName);
            }
        }
    }
}
