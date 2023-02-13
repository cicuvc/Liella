using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler {
    public sealed class ILCodeHandlerAttribute: Attribute {
        public readonly ILOpCode[] ILOpcodes;
        public ILCodeHandlerAttribute(params ILOpCode[] code) {
            ILOpcodes = code;
        }
    }
    public abstract class ILCodeHandler {
        protected delegate void EmitHandler(ILOpCode opcode,ulong operand);
        protected Dictionary<ILOpCode, EmitHandler> m_DispatchMap = new Dictionary<ILOpCode, EmitHandler>();
        public ILCodeHandler() {
            var type = GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ;
            foreach(var i in methods) {
                var attribute = i.GetCustomAttribute<ILCodeHandlerAttribute>();
                if(attribute != null) {
                    var delegateValue = Delegate.CreateDelegate(typeof(EmitHandler), this, i);
                    foreach (var j in attribute.ILOpcodes) {
                        m_DispatchMap.Add(j, (EmitHandler)delegateValue);
                    }
                }
            }

        }
        public void Emit(ILOpCode code, ulong operand) {
            if (m_DispatchMap.ContainsKey(code)) {
                m_DispatchMap[code](code, operand);
            } else {
                throw new NotImplementedException($"Handler for MSIL code {code} not yet implemented");
            }
        }
    }
}
