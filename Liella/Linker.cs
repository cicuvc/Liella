using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using Elf32_Half = System.UInt16;
using Elf32_Word = System.UInt16;

// re

namespace Liella {

    public enum ELFMachine:ushort {
        None = 0,
        SPARC = 2,
        Intel386 = 3,
        MIPS1 = 8,
        PowerPC = 0x14,
        ARM = 0x28,
        AMD64 = 0x3E
    }
    public enum ELFType : ushort {
        None = 0,
        Relocatable = 1,
        Executable = 2,
        DynamicLink = 3,
        Dump = 4,
    }
    public enum ELFSectionType: uint {
        None = 0x0,
        ProgramBits = 0x1,
        SymbolTable = 0x2,
        StringTable = 0x3,
        RelocationTable = 0x4,
        Hash = 0x5,
        DynamicInfo = 0x6,
        Note = 0x7,
        NoBits = 0x8,
        RawRelocationTable = 0x9,
        DynamicSymbols = 0xB,
        InitArray = 14,
        FiniteArray = 15,
        PreInitArray = 16,
        Group = 17,
        ExtendedSections = 18,
        Number = 19,
        GAttributes = 0x6ffffff5,
        GHash = 0x6ffffff6,
        GLibList = 0x6ffffff7,
        Checksum = 0x6ffffff8,
    }
    public enum ELFSymbolType {
        NoType = 0x0,
        Object = 0x1,
        Function = 0x2,
        Section = 0x3,
        File = 0x4,
        Common = 0x5,
        TLSData = 0x6,

    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ELFSectionHeader {
        private uint m_Name;
        private uint m_Type;
        private ulong m_Flags;
        private ulong m_Address;
        private ulong m_Offset;
        private ulong m_Size;
        private uint m_Link;
        private uint m_Info;
        private ulong m_AddrAlign;
        private ulong m_AuxTableSize;
        public uint NameOffset => m_Name;
        public ulong BodyOffset => m_Offset;
        public ulong BodySize => m_Size;
        public ELFSectionType Type => (ELFSectionType)m_Type;
        public uint Info => m_Info;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ELFSymbol {
        private uint m_NameOffset;
        private byte m_Info;
        private byte m_Other;
        private ushort m_SectionIndex;
        private ulong m_Value;
        private ulong m_Size;
        public uint NameOffset => m_NameOffset;
        public ELFSymbolType Type => (ELFSymbolType)(m_Info & 0xF);
        public ulong Value => m_Value;
        public ulong Size => m_Size;
        public ushort SectionIndex => m_SectionIndex;
    }
    public unsafe struct ELFSymbolInfo {
        public ELFSymbol* m_ELFSymbol;
        public ELFReader m_Reader;
        public string Name {
            get {
                if (m_ELFSymbol->Type == ELFSymbolType.Section) {
                    return m_Reader.Sections[m_ELFSymbol->SectionIndex].Name;
                } else {
                    return m_Reader.ResolveString(m_ELFSymbol->NameOffset);
                }
            }
        }
        public ulong Address {
            get {
                if (m_ELFSymbol->SectionIndex >= m_Reader.Sections.Count) return 0;
                return m_Reader.Sections[(int)m_ELFSymbol->SectionIndex].m_ELFSection->BodyOffset + m_ELFSymbol->Value;
            }
        }
        public ulong SectionOffset => m_ELFSymbol->Value;
        
    }
    public unsafe struct ELFSectionInfo {
        public ELFSectionHeader* m_ELFSection;
        public ELFReader m_Reader;

        public string Name => m_Reader.ResolveString(m_ELFSection->NameOffset);
    }
    [StructLayout(LayoutKind.Sequential, Size = 8*sizeof(ulong))]
    public unsafe struct ELFHeader {
        private fixed byte m_Magic[16];
        private ushort m_Type;
        private ushort m_Machine;
        private uint m_FileVersion;
        private ulong m_Entry;
        private ulong m_ProgHeader;
        private ulong m_SegHeader;
        private uint m_Flags;
        private ushort m_EHSize;
        private ushort m_ProgHeaderItemSize;
        private ushort m_ProgHeaderListLength;
        private ushort m_SegHeaderItemSize;
        private ushort m_SegHeaderListCount;
        private ushort m_SegNameIndex;

        public uint ProgramHeaderLength => ((uint)m_ProgHeaderItemSize) * m_ProgHeaderListLength;
        public uint SegmentHeaderLength => ((uint)m_SegHeaderItemSize) * m_SegHeaderListCount;
        public uint SegmentHeaderCount => m_SegHeaderListCount;
        public uint SegmentNameIndex => m_SegNameIndex;
        public ushort ExcHandlerSize => m_EHSize;
        public ulong EntryPoint => m_Entry;
        public ulong ProgramHeader => m_ProgHeader;
        public ulong SegmentHeader => m_SegHeader;
        public ELFType Type => (ELFType)m_Type;
        public ELFMachine Machine => (ELFMachine)m_Machine;
        public uint Version => m_FileVersion;
        public uint Flags => m_Flags;

    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ELFRelocateAddend {
        public ulong m_Offset;
        public ulong m_Info;
        public ulong m_Addend;
    }
    public enum ELFRelocationType {
        None = 0x0,
        Imm64 = 1,
        PC32 = 2,
        GOT32 = 3,
        PLT32 = 4,
        Copy = 5,
        GlobalData = 6,
        JumpSlot = 7,
        Relative = 8,

        Imm32 = 10,
        ImmSigned32 = 11,
        Imm16 = 12,
        PC16 = 13,
        Imm8 = 14,
        PC8 = 15,

        PC64 = 24,
        
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ELFRelocate {
        public ulong m_Offset;
        public uint m_Type;
        public uint m_SymbolIndex;
        
        public long m_Addend;
        public ulong Offset => m_Offset;
        public uint SymbolIndex => m_SymbolIndex;
        public ELFRelocationType Type => (ELFRelocationType)m_Type;
        public long Addend => m_Addend;
    }
    
    public unsafe class ELFReader {
        protected Memory<byte> m_ELFImage;
        protected byte* m_ImageBuffer;
        protected ELFHeader m_Header;
        protected MemoryHandle m_ImageBufferHandle;
        protected List<ELFSectionInfo> m_Sections = new List<ELFSectionInfo>();
        protected List<ELFSymbolInfo> m_Symbols = new List<ELFSymbolInfo>();
        public List<ELFSectionInfo> Sections => m_Sections;
        
        public ELFReader(void *imageBuffer, int size) {
            m_ImageBuffer = (byte*)imageBuffer;
        }
        public void ReadFile() {
            fixed(ELFHeader *pHeader = &m_Header) {
                Buffer.MemoryCopy(m_ImageBuffer, pHeader, sizeof(ELFHeader), sizeof(ELFHeader));
            }
            ELFSectionHeader* header = (ELFSectionHeader*)(m_ImageBuffer + m_Header.SegmentHeader);
            for (var i = 0u; i < m_Header.SegmentHeaderCount; i++) {
                var sectionHeader = &header[i];
                m_Sections.Add(new ELFSectionInfo(){ 
                    m_Reader = this,
                    m_ELFSection = sectionHeader
                });
            }
            foreach(var i in m_Sections) {
                Console.WriteLine($"Section={i.Name}, Offset={i.m_ELFSection->BodyOffset}, Size={i.m_ELFSection->BodySize}, Type={i.m_ELFSection->Type}");
            }
            CollectSymbols();
            CollectRelocations();
        }
        public string ResolveString(uint nameIndex) {
            return Marshal.PtrToStringAnsi(new IntPtr(m_ImageBuffer + m_Sections[(int)m_Header.SegmentNameIndex].m_ELFSection->BodyOffset + nameIndex));
        }
        public void CollectSymbols() {
            var symbolSections = m_Sections.Where(e => e.m_ELFSection->Type == ELFSectionType.SymbolTable).ToList();
            foreach(var i in symbolSections) {
                var symbolCount = i.m_ELFSection->BodySize / (uint)sizeof(ELFSymbol);
                var symbolTable = (ELFSymbol*)(m_ImageBuffer + i.m_ELFSection->BodyOffset);
                for(var j = 0u; j < symbolCount; j++) {
                    m_Symbols.Add(new ELFSymbolInfo() {
                        m_Reader = this,
                        m_ELFSymbol = &symbolTable[j]
                    });
                }
            }
            foreach(var i in m_Symbols) {
                Console.WriteLine($"[SYMBOL] Symbol = {i.Name}, Type = {i.m_ELFSymbol->Type}, Size = {i.m_ELFSymbol->Size}, Address = {i.Address}");
            }
        }
        public void CollectRelocations() {
            var relocateSections = m_Sections.Where(e => e.m_ELFSection->Type == ELFSectionType.RelocationTable);
            foreach (var i in relocateSections) {
                var relocCount = i.m_ELFSection->BodySize / (uint)sizeof(ELFRelocate);
                var relocTable = (ELFRelocate*)(m_ImageBuffer + i.m_ELFSection->BodyOffset);
                for (var j = 0u; j < relocCount; j++) {
                    var relocItem = relocTable[j];
                    Console.WriteLine($"[REL] Symbol = {m_Symbols[(int)relocItem.m_SymbolIndex].Name}, Type = {relocItem.Type}, Offset = {relocItem.Offset}, Delta = {relocItem.Addend}");
                }
            }
        }
    }
    public abstract class LiLinkerImage {
        protected List<LiLinkerSectionInfo> m_Sections = new List<LiLinkerSectionInfo>();
        public List<LiLinkerSectionInfo> Sections => m_Sections;
        public ulong EntryPoint { get; }
        
    }
    public enum LiLinkerRelocType :uint{
        None = 0x0,
        Rel32 = 0x1,
        Rel64 = 0x2,
        Imm32 = 0x3,
        Imm64 = 0x4
    }
    public enum LiLinkerSectionType : uint {
        Initialized = 0x1,
        Read = 0x2,
        Write = 0x4,
        Execute = 0x8,
    }
    public struct LiLinkerSymbolInfo {
        public LiLinkerImage m_ImageContext;
        public LiLinkerSectionInfo m_Section;
        public ulong m_Offset;
        public ulong m_Size;
    }
    public struct LiLinkerRelocInfo {
        public LiLinkerImage m_ImageContext;
        public LiLinkerRelocType m_Type;
        public ulong m_Offset;
        public LiLinkerSymbolInfo m_Symbol;
        public long m_Delta;
    }
    public struct LiLinkerImageBufferView {
        public ulong BufferStart { get; set; }
        public ulong BufferLength { get; set; }
    }
    public abstract class LiLinkerSectionInfo {
        protected LiLinkerImage m_ImageContext;
        protected LiLinkerImageBufferView m_SectionBody;

    }
    public abstract class LiLinkerProgramSection : LiLinkerSectionInfo {

    }
    public abstract class LiLinkerRelocationSection: LiLinkerSectionInfo {
        protected LiLinkerSectionInfo m_RelocatedSection;

    }
    public abstract class LiLinkerSymbolTableSection: LiLinkerSectionInfo {

    }
    public abstract class LiLinkerStringTableSection: LiLinkerSectionInfo {

    }
    public class ELFImage:LiLinkerImage {
        protected ELFHeader m_Header;
        protected Stream m_ImageStream;
        public unsafe ELFImage(Stream stream) {
            m_ImageStream = stream;

            stream.Seek(0, SeekOrigin.Begin);
            var headerData = new byte[sizeof(ELFHeader)];
            stream.Read(headerData);

            fixed (byte *pHeaderData = headerData) {
                fixed(ELFHeader *pHeader = &m_Header) {
                    Buffer.MemoryCopy(pHeaderData, pHeader, sizeof(ELFHeader), sizeof(ELFHeader));
                }
            }

            if (m_Header.SegmentHeaderCount != 0) {
                stream.Seek((uint)m_Header.SegmentHeader, SeekOrigin.Begin);
                for (var i = 0u; i < m_Header.SegmentHeaderCount; i++) {
                    
                }
            }
        }
    }
    public class ELFSymbolSection: LiLinkerSymbolTableSection {

    }
    public class Linker {
        public Linker() {

        }
    }
}
