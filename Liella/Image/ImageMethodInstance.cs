using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Liella.MSIL;

namespace Liella.Image {
    public class ImageMethodInstance : MethodInstance {
        protected ImageCompilationUnitSet m_TypeEnv;
        protected ImageMethodEntry m_Entry;
        protected MetadataReader m_Reader;
        protected MethodDefinition m_Definition;
        protected MethodBodyBlock m_Body;
        protected StandaloneSignature m_LocalSignature;
        protected ImageTypeInfo m_DeclaringType;
        protected ImmutableArray<byte> m_ILSequence;
        protected ImmutableArray<ushort> m_ILCodeStart = ImmutableArray<ushort>.Empty;
        protected ImmutableArray<ushort> m_ILBranch = ImmutableArray<ushort>.Empty;
        protected IGenericParameterContext m_ParentGenericContext;
        protected uint m_GenericParamStart;
        protected SortedList<Interval, MethodBasicBlock> m_BasicBlocks = new SortedList<Interval, MethodBasicBlock>(new Interval.IntervalTreeComparer());
        protected MethodSignature<TypeEntry> m_Signature;
        protected ImmutableArray<TypeEntry> m_LocalVaribleTypes = ImmutableArray<TypeEntry>.Empty;

        protected ImmutableDictionary<TypeEntry, CustomAttribute> m_CustomAttributes = ImmutableDictionary<TypeEntry, CustomAttribute>.Empty;
        protected ImmutableArray<ExceptionRegion> m_ExceptionTable;
        public override MethodAttributes Attributes => m_Entry.MethodDef.Attributes;
        public override MethodImplAttributes ImplAttributes => m_Entry.MethodDef.ImplAttributes;
        public override ImageMethodEntry Entry => m_Entry;
        public override ImageCompilationUnitSet TypeEnv => m_TypeEnv;
        public MethodBodyBlock Body { get => m_Body; }
        public MetadataReader Reader { get => m_Reader; }
        public override bool IsDummy => m_Body == null;
        public override ImageTypeInfo DeclType { get => m_DeclaringType; }
        public override MethodSignature<TypeEntry> Signature => m_Signature;
        public override SortedList<Interval, MethodBasicBlock> BasicBlocks => m_BasicBlocks;
        public override ImmutableArray<TypeEntry> LocalVaribleTypes => m_LocalVaribleTypes;
        public override ImmutableDictionary<TypeEntry, CustomAttribute> CustomAttributes => m_CustomAttributes;
        public override string ToString() {
            return m_Entry.Name;
        }

        public override string ResolveStringToken(UserStringHandle stringHandle) {
            return m_Reader.GetUserString(stringHandle);
        }
        public override StandaloneSignature ResolveSignatureToken(EntityHandle sigToken) {
            return m_Reader.GetStandaloneSignature((StandaloneSignatureHandle)sigToken);
        }
        public override FieldInfo ResolveFieldToken(EntityHandle fieldToken, out TypeEntry declType) {
            var field = m_TypeEnv.ResolveFieldByHandle(fieldToken, m_Reader, this);
            declType = field.DeclType;
            return field;
        }
        public override FieldInfo ResolveStaticFieldToken(EntityHandle fieldToken, out TypeEntry declType) {
            var field = m_TypeEnv.ResolveStaticFieldByHandle(fieldToken, m_Reader, this);
            declType = field.DeclType;
            return field;
        }
        public override MethodEntry ResolveMethodToken(EntityHandle methodToken, out TypeEntry typeEntry, out MethodSignature<TypeEntry> callSiteSig) {
            var factory = m_TypeEnv.TypeEntryFactory;
            var reader = m_Reader;

            switch (methodToken.Kind) {
                case HandleKind.MethodDefinition: {
                    var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)methodToken);
                    var declType = reader.GetTypeDefinition(methodDef.GetDeclaringType());
                    typeEntry = factory.CreateTypeEntry(declType);

                    var methodEntry = factory.CreateMethodEntry(typeEntry, methodDef, System.Collections.Immutable.ImmutableArray<TypeEntry>.Empty);
                    callSiteSig = m_TypeEnv.ActiveMethods[methodEntry].Signature;
                    return methodEntry;
                }
                case HandleKind.MemberReference: {
                    var methodDef = m_TypeEnv.ResolveMethodDefFromMemberRef(methodToken, m_Reader, this, out typeEntry, out callSiteSig);
                    var methodEntry = factory.CreateMethodEntry(typeEntry, methodDef, System.Collections.Immutable.ImmutableArray<TypeEntry>.Empty);
                    return methodEntry;
                }
                case HandleKind.MethodSpecification: {
                    var methodDef = m_TypeEnv.ResolveMethodDefFromMethodSpec((MethodSpecificationHandle)methodToken, m_Reader, this, out typeEntry, out var specTypes);
                    var methodEntry = factory.CreateMethodEntry(typeEntry, methodDef, specTypes);

                    callSiteSig = m_TypeEnv.ActiveMethods[methodEntry].Signature;
                    return methodEntry;
                }
            }
            throw new InvalidProgramException();
        }
        public override TypeEntry ResolveTypeToken(EntityHandle handle) {
            return m_TypeEnv.ResolveTypeByHandle(handle, m_Reader, this);
        }

        public ImageMethodInstance(ImageCompilationUnitSet typeEnv, ImageMethodEntry entry, MethodBodyBlock impl, ImageTypeInfo declType) {
            var declTypeDef = ((ImageRealTypeEntry)declType.Entry).TypeDef;


            m_TypeEnv = typeEnv;
            m_Reader = MetadataHelper.GetMetadataReader(ref declTypeDef);
            m_Body = impl;
            m_Entry = entry;
            m_Definition = entry.MethodDef;

            m_DeclaringType = declType;
            m_LocalSignature = (impl == null || impl.LocalSignature.IsNil) ? default : m_Reader.GetStandaloneSignature(impl.LocalSignature);
            m_ILSequence = (impl != null) ? impl.GetILContent() : ImmutableArray<byte>.Empty;
            m_ParentGenericContext = declType;
            m_Signature = entry.MethodDef.DecodeSignature(declType.TypeEnv.SignatureDecoder, this);
            m_LocalVaribleTypes = (impl == null || impl.LocalSignature.IsNil) ? ImmutableArray<TypeEntry>.Empty : m_LocalSignature.DecodeLocalSignature(declType.TypeEnv.SignatureDecoder, this);
            m_ExceptionTable = (impl != null) ? impl.ExceptionRegions : ImmutableArray<ExceptionRegion>.Empty;

            var customAttrBuilder = m_CustomAttributes.ToBuilder();
            var customAttrs = entry.MethodDef.GetCustomAttributes().Select(e => m_Reader.GetCustomAttribute(e)).ToArray();
            foreach (var i in customAttrs) {
                var ctorMethod = typeEnv.ResolveMethodByHandle(i.Constructor, m_Reader, m_DeclaringType, out var attrType, out _);
                customAttrBuilder.Add(attrType, i);
            }
            m_CustomAttributes = customAttrBuilder.ToImmutable();

        }
        // Determine the start points of instructions
        public unsafe void PreprocessILCode() {
            if (m_ILCodeStart != ImmutableArray<ushort>.Empty) return;
            var builder = m_ILCodeStart.ToBuilder();
            var length = m_ILSequence.Length;
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                for (int i = 0; i < length;) {
                    builder.Add((ushort)i);
                    var ilOpcode = (ILOpCode)pBuffer[i++];
                    if (((uint)ilOpcode) >= 249) {
                        ilOpcode = (ILOpCode)((((uint)ilOpcode) << 8) + pBuffer[i++]);
                    }
                    //if (ilOpcode == ILOpCode.Ldc_r8) Debugger.Break();
                    var operandLength = ilOpcode.GetOperandSize();
                    if (ilOpcode == ILOpCode.Switch) {
                        operandLength += 4 * (*(int*)&pBuffer[i]);
                    }
                    i += operandLength;
                }
            }
            m_ILCodeStart = builder.ToImmutable();
        }
        public unsafe void CollectToken(OperandType type, Action<uint> callback) {
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                foreach (var i in m_ILCodeStart) {
                    var start = i;
                    uint ilCode = pBuffer[start++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[start++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();

                    if (opcode.OperandType == type) callback(*(uint*)&pBuffer[start]);
                }
            }
        }

        public override TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index) {
            if (m_Entry.GenericType.Length <= index) throw new ArgumentOutOfRangeException(nameof(index));
            return m_Entry.GenericType[(int)(index)];
        }

        public override TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index) {
            return m_ParentGenericContext.GetTypeGenericByIndex(env, index);
        }
        public override unsafe void MakeBasicBlocks() {

            if (!m_ILBranch.IsEmpty) return;
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var builder = m_ILBranch.ToBuilder();
                var pBuffer = (byte*)memBuffer.Pointer;
                var codeStart = m_ILCodeStart;
                foreach (var i in codeStart) {
                    var codePos = i; ;
                    uint ilCode = pBuffer[codePos++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[codePos++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();
                    if (opcode == OpCodes.Leave_S || opcode == OpCodes.Leave
                        || opcode == OpCodes.Endfinally || opcode == OpCodes.Endfilter) continue;
                    if (opcode.FlowControl == (FlowControl.Branch) || opcode.FlowControl == (FlowControl.Cond_Branch) || opcode.FlowControl == (FlowControl.Return)) {
                        var branchOffset = m_ILCodeStart.BinarySearch(i);
                        builder.Add((ushort)branchOffset);
                    }
                }
                m_ILBranch = builder.ToImmutable();
                var mainBlock = new MethodBasicBlock(this, 0, (uint)m_ILCodeStart.Length);
                m_BasicBlocks.Add(mainBlock.Interval, mainBlock);
                foreach (var i in m_ILBranch) {
                    // locate jump target
                    var codePos = m_ILCodeStart[(int)i]; ;
                    uint ilCode = pBuffer[codePos++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[codePos++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();


                    // cut false branch
                    var falseBranch = MethodBasicBlock.CutBasicBlock((uint)(i + 1), m_BasicBlocks);
                    if (falseBranch != null) {
                        var branchInstBlock0 = m_BasicBlocks[new Interval(i, i)];
                        branchInstBlock0.FalseExit = falseBranch;
                    }

                    switch ((ILOpCode)ilCode) {
                        case ILOpCode.Ret: {
                            continue;
                        }
                        case ILOpCode.Switch: {
                            var branchListLength = *(int*)&pBuffer[codePos];
                            codePos += 4; // skip length operand
                            var trueBranches = new MethodBasicBlock[branchListLength];
                            var branchOffsetStart = codePos + (branchListLength) * 4;
                            for (var j = 0u; j < branchListLength; j++) {
                                var switchTarget = (uint)((*(int*)&pBuffer[codePos + j * 4]) + branchOffsetStart);
                                var switchBranchIndex = (uint)m_ILCodeStart.BinarySearch((ushort)switchTarget);

                                trueBranches[j] = MethodBasicBlock.CutBasicBlock(switchBranchIndex, m_BasicBlocks);
                            }
                            var switchBlock = m_BasicBlocks[new Interval(i, i)];
                            switchBlock.TrueExit = trueBranches;
                            continue;
                        }
                        case ILOpCode.Br:
                        case ILOpCode.Br_s: {
                            m_BasicBlocks[new Interval(i, i)].FalseExit = null;
                            break;
                        }
                    }

                    uint branchTarget = 0;
                    if (opcode.OperandType == OperandType.ShortInlineBrTarget) {
                        branchTarget = (uint)((sbyte)pBuffer[codePos] + (codePos + 1));
                    } else {
                        branchTarget = (uint)((*(int*)&pBuffer[codePos]) + (codePos + 4));
                    }
                    var targetBranchIndex = (uint)m_ILCodeStart.BinarySearch((ushort)branchTarget);

                    // cut true branch

                    var trueBranch = MethodBasicBlock.CutBasicBlock(targetBranchIndex, m_BasicBlocks);
                    var branchInstBlock = m_BasicBlocks[new Interval(i, i)];
                    branchInstBlock.TrueExit = new MethodBasicBlock[] { trueBranch };
                    continue;
                }

            }
            /*foreach(var i in m_ExceptionTable) {
                var tryBlockIndex = (uint)m_ILCodeStart.BinarySearch((ushort)i.TryOffset);
                var tryBlockEndIndex = (uint)m_ILCodeStart.BinarySearch((ushort)(i.TryOffset + i.TryLength));
                MethodBasicBlock.CutBasicBlock(tryBlockEndIndex, m_BasicBlocks);
                MethodBasicBlock.CutBasicBlock(tryBlockIndex, m_BasicBlocks);

                //var handlerIndex = (uint)m_ILCodeStart.
            }*/
            foreach (var i in m_BasicBlocks) {
                var stackDelta = 0;
                //if (i.Key.Left == 42 && i.Key.Right == 48) Debugger.Break();
                ForEachIL(i.Key, (opcode, operand) => {
                    var delta = ILCodeExtension.StackDeltaTable[opcode];
                    if (delta != int.MaxValue) {
                        stackDelta += delta;
                    } else {
                        switch (opcode) {
                            case ILOpCode.Calli: {
                                var signature = (StandaloneSignatureHandle)MetadataHelper.CreateHandle((uint)operand);
                                var sigObj = Reader.GetStandaloneSignature(signature);
                                var targetSignature = sigObj.DecodeMethodSignature(TypeEnv.SignatureDecoder, this);
                                stackDelta -= targetSignature.ParameterTypes.Length;
                                stackDelta--; // ftn
                                if (targetSignature.ReturnType.ToString()!=("System::Void")) {
                                    stackDelta++;
                                }
                                break;
                            }
                            case ILOpCode.Newobj: {
                                var method = m_TypeEnv.ResolveMethodByHandle(MetadataHelper.CreateHandle((uint)operand), Reader, this, out var declType, out var signature);
                                stackDelta -= signature.ParameterTypes.Length;
                                stackDelta++;
                                break;
                            }
                            case ILOpCode.Callvirt:
                            case ILOpCode.Call: {
                                var method = m_TypeEnv.ResolveMethodByHandle(MetadataHelper.CreateHandle((uint)operand), Reader, this, out var declType, out var signature);
                                stackDelta -= signature.ParameterTypes.Length;
                                if (!method.MethodDef.Attributes.HasFlag(MethodAttributes.Static)) stackDelta--;
                                if (signature.ReturnType.ToString()!=("System::Void")) {
                                    stackDelta++;
                                }
                                break;
                            }
                            case ILOpCode.Ret: {
                                if (m_Signature.ReturnType.ToString()!=("System::Void")) {
                                    stackDelta--;
                                }
                                break;
                            }
                            default: {
                                throw new NotImplementedException();
                            }
                        }

                    }
                }, false);
                i.Value.StackDepthDelta = stackDelta;
            }
        }
        public override unsafe void ForEachIL(Interval iv, Action<ILOpCode, ulong> callback, bool makeVirtualExit) {
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                var lastOpCode = ILOpCode.Nop;
                for (int i = (int)iv.Left; i < iv.Right; i++) {
                    var instStart = m_ILCodeStart[i];
                    uint ilCode = pBuffer[instStart++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[instStart++];
                    //if (ilCode == (uint)ILOpCode.Switch) Debugger.Break();
                    var opcode = (lastOpCode = (ILOpCode)ilCode).ToOpCode();
                    ulong operand = 0;
                    switch (opcode.OperandType) {
                        case OperandType.InlineBrTarget:
                        case OperandType.InlineField:
                        case OperandType.InlineI:
                        case OperandType.InlineMethod:
                        case OperandType.InlineSig:
                        case OperandType.InlineString:
                        case OperandType.InlineTok:
                        case OperandType.InlineType:
                        case OperandType.InlineVar:
                        case OperandType.InlineSwitch:
                        case OperandType.ShortInlineR: {
                            operand = *(uint*)&pBuffer[instStart];
                            //instStart += 4;
                            break;
                        }
                        case OperandType.ShortInlineBrTarget:
                        case OperandType.ShortInlineI:
                        case OperandType.ShortInlineVar: {
                            operand = pBuffer[instStart];
                            break;
                        }
                        case OperandType.InlineR:
                        case OperandType.InlineI8: {
                            operand = *(ulong*)&pBuffer[instStart];
                            //instStart += 8;
                            break;
                        }
                    }
                    callback((ILOpCode)ilCode, operand);
                }
                if (!makeVirtualExit) return;
                if (!lastOpCode.IsBranch() & lastOpCode.ToOpCode().FlowControl != FlowControl.Return & lastOpCode != ILOpCode.Switch) {
                    var currentBlock = m_BasicBlocks[iv];
                    var nextIV = new Interval(currentBlock.Interval.Right, currentBlock.Interval.Right);
                    currentBlock.TrueExit = new MethodBasicBlock[] { m_BasicBlocks[nextIV] };
                    callback(ILOpCode.Br, 0); // virtual branch
                }
            }
        }
    }

}
