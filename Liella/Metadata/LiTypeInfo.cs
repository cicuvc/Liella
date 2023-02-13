using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class LiTypeInfo {
        public abstract TypeAttributes Attributes { get; }
        public abstract TypeEntry Entry { get; }
        public abstract LiTypeInfo BaseType { get; }
        public abstract SortedList<string, LiFieldInfo> Fields { get; }
        public abstract SortedList<string, LiFieldInfo> StaticFields { get; }
        public abstract CompilationUnitSet TypeEnv { get; }
        public abstract Dictionary<MethodEntry, MethodInstance> Methods { get; }
        public abstract LiTypeInfo[] Interfaces { get; }
        public abstract List<MethodImplInfo> ImplInfo { get; }
        public abstract bool IsValueType { get; }
        public abstract string FullName { get; }
        public abstract TypeLayout Layout { get; }


        public abstract MethodInstance FindMethodByName(string name);
        public abstract void RegisterFields();
        public abstract void RegisterMethodInstance(MethodInstance mi);
    }
}
