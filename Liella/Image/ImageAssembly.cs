using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageAssembly {
        protected Stream m_AssemblyStream;
        protected MetadataReader m_Reader;
        protected PEReader m_PEReader;
        public PEReader PEReader { get => m_PEReader; }
        public MetadataReader Reader { get => m_Reader; }
        public unsafe ImageAssembly(Stream stream) {
            m_AssemblyStream = stream;
            m_PEReader = new PEReader(stream);
            var metadata = m_PEReader.GetMetadata();
            m_Reader = MetadataHelper.CreateMetadataReader(metadata.Pointer, metadata.Length);
        }
    }
}
