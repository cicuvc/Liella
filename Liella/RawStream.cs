using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella {
    public class RawStream {
        protected Stream m_Stream;
        protected byte[] m_ReadBuffer;
        public RawStream(Stream stream, int bufferSize = 1024) {
            m_Stream = stream;
        }
    }
}
