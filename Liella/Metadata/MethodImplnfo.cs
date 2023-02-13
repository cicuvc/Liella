using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public struct MethodImplInfo {
        private MethodEntry m_InterfacceDecl;
        private MethodEntry m_ImplBody;
        public MethodEntry InterfaceDecl => m_InterfacceDecl;
        public MethodEntry ImplBody => m_ImplBody;
        public static MethodImplInfo CreateRecord(MethodEntry interfaceDecl, MethodEntry implBody) {
            MethodImplInfo implInfo;
            implInfo.m_InterfacceDecl = interfaceDecl;
            implInfo.m_ImplBody = implBody;
            return implInfo;
        }
    }
}
