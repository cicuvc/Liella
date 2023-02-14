using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public sealed class ImageAssembly:IDisposable {
        private Stream m_AssemblyStream;
        private MetadataReader m_Reader;
        private PEReader m_PEReader;
        private bool disposedValue;

        public PEReader PEReader { get => m_PEReader; }
        public MetadataReader Reader { get => m_Reader; }
        public unsafe ImageAssembly(Stream stream) {
            m_AssemblyStream = stream;
            m_PEReader = new PEReader(stream);
            var metadata = m_PEReader.GetMetadata();
            m_Reader = MetadataHelper.CreateMetadataReader(metadata.Pointer, metadata.Length);
        }

        private void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    m_PEReader.Dispose();
                }
                disposedValue = true;
            }
        }


        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
