using Liella.Metadata;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Liella.Compiler.LLVM {
    public static class IRHelper {
        public static LLVMValueRef AllocObjectDefault(LLVMCompiler compiler, LLVMClassTypeInfo declType, LLVMBuilderRef builder) {
            var runtimeHelpers = compiler.ResolveLLVMType(compiler.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var gcHeapAlloc = compiler.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("GCHeapAlloc").Entry);

            var dataStorAddr = builder.BuildCall2(gcHeapAlloc.FunctionType, gcHeapAlloc.Function, new LLVMValueRef[] {
                            declType.ReferenceType.LLVMType.ElementType.SizeOf
                        });
            var objectBody = builder.BuildBitCast(dataStorAddr, ((LLVMClassTypeInfo)declType).ReferenceType.LLVMType);
            var vtblPtr = builder.BuildGEP(objectBody, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(0)
                        });
            builder.BuildStore(declType.VtableBody, vtblPtr);

            var pthis = builder.BuildGEP(objectBody, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(1)
                        });
            return pthis;
        }
        public static void MakeCall(LLVMMethodInfoWrapper targetMethod, LLVMValueRef targetFunction, LLVMValueRef[] argumentList, LLVMBuilderRef builder, Stack<LLVMCompValue> evalStack) {
            var returnValue = builder.BuildCall2(targetFunction.TypeOf.ElementType, targetFunction, argumentList);

            if (targetMethod.ReturnType.LLVMType != LLVMTypeRef.Void)
                evalStack.Push(LLVMCompValue.CreateValue(returnValue, targetMethod.ReturnType));
        }
        public static LLVMValueRef GetInstanceFieldAddress(LLVMCompValue obj, uint fieldToken, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMCompType fieldType) {
            var unkToken = MetadataHelper.CreateHandle(fieldToken);
            var typeEnv = context.Context.TypeEnvironment;

            var fieldInfo = context.Method.ResolveFieldToken(unkToken, out var declType);

            var llvmType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(declType);
            var dataStorPtrType = LLVMTypeRef.CreatePointer(llvmType.DataStorageType, 0);

            var objDataAddr = builder.BuildBitCast(obj.Value, dataStorPtrType);

            fieldType = context.Context.ResolveLLVMInstanceType(fieldInfo.Type);
            var fieldIndex = fieldInfo.FieldIndex;
            if (llvmType.BaseType != null) fieldIndex++; // skip base storage

            var fieldPtr = builder.BuildGEP(objDataAddr, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, fieldIndex) });
            return fieldPtr;
        }


        public static LLVMValueRef LookupVtable(LLVMValueRef pthis, int index, LLVMTypeRef typeVTable, LLVMBuilderRef builder) {
            var vtablePtrType = LLVMTypeRef.CreatePointer(typeVTable, 0);
            var objectHeaderPtrType = LLVMTypeRef.CreatePointer(vtablePtrType, 0);
            var instancePtr = builder.BuildBitCast(pthis, objectHeaderPtrType);
            var pVtbl = builder.BuildGEP(instancePtr, new LLVMValueRef[] {
                LLVMHelpers.CreateConstU32(-1)
            });
            var pTypedVtbl = builder.BuildLoad(pVtbl);
            var funcPtr = builder.BuildGEP(pTypedVtbl,
                new LLVMValueRef[] {
                    LLVMHelpers.CreateConstU32(0),LLVMHelpers.CreateConstU32(index)
                });
            var vptr = builder.BuildLoad(funcPtr);
            //LLVMHelpers.AddMetadataForInst(vptr, "invariant.load", builder);
            return vptr;
        }
        public static LLVMMethodInfoWrapper GetMethodInfo(uint token, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMTypeInfo llvmType, out MethodSignature<TypeEntry> callSiteSig) {
            var methodToken = MetadataHelper.CreateHandle(token);
            var methodEntry = context.Method.ResolveMethodToken(methodToken, out var declType, out callSiteSig);

            llvmType = context.Context.ResolveLLVMType(declType);
            return context.Context.ResolveLLVMMethod(methodEntry);
        }
        public static LLVMValueRef GetStaticFieldAddress(uint operand, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMCompType fieldType) {
            var typeEnv = context.Context.TypeEnvironment;
            var fieldToken = MetadataHelper.CreateHandle(operand);

            var fieldInfo = context.Method.ResolveStaticFieldToken(fieldToken, out var declType);

            fieldType = context.Context.ResolveLLVMInstanceType(fieldInfo.Type);

            var staticClass = context.Context.ResolveLLVMType(declType);

            var staticStorage = staticClass.StaticStorage;
            return builder.BuildGEP(staticStorage, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, fieldInfo.FieldIndex) });
        }
    }

}
