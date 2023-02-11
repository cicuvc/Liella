

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/*Multiline*/
namespace EFILoader {
    [StructLayout(LayoutKind.Sequential)]
    struct EFI_HANDLE {
        private IntPtr _handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL {
        private readonly IntPtr _pad;

        public readonly delegate*<void*, void*, void*> OutputString;
    }

    public enum EFIAllocType {
        AllocateAnyPages,
        AllocMaxAddress,
        AllocAddress,
        MaxAllocType
    }
    public unsafe struct EFIOpenProtocolEntry {
        public IntPtr agent;
        public IntPtr ctrl_handle;
        public uint attribute;
        public uint open_count;
    }
    public enum EFIMemroyType {
        EfiReservedMemoryType,
        EfiLoaderCode,
        EfiLoaderData,
        EfiBootServiceCode,
        EfiBootServiceData,
        EfiRuntimeServiceCode,
        EfiRuntiemServiceData,
        EfiConvMemory,
        EfiUnstableMemory,
        EfiACPIReclaimMemory,
        EfiACPIMemoryNVS,
        EfiIOMappedMemory,
        EfiIOMappedPortMemory,
        EfiPalCode,
        EfiPersistentMemory,
        EfiMaxMemoryType,
    }
    public struct EFIGuid {
        public ulong uuPart0;
        public ulong uuPart3;

        public EFIGuid(ulong v0,ulong v1) {
            uuPart0 = v0;
            uuPart3 = v1;
        }
    }
    public unsafe struct EFIMemoryDescriptor {
        public uint type;
        public IntPtr addrPhysicalStart;
        public IntPtr addrVirtualStart;
        public ulong cnPage;
        public ulong attribute;
    }
    public enum EFILocateSearch {
        AllHandles,
        ByRegisterNotify,
        ByProtocol
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EFI_BOOT_SERVICES_MT {
        // uint RaiseTPL(ulong new_tpl)
        public delegate*<ulong, uint> RaiseTPL;
        // efi_status(*restoreTPL)(efi_tpl_t old_tpl);
        public delegate*<ulong, uint> RestoreTPL;
        /**
         efi_status(*allocPages)(
            efi_alloc_type_e type,
            efi_memory_type_e mem_type,
            uint64_t pages,
            efi_physical_address_t* memory);
         */
        public delegate*<EFIAllocType, EFIMemroyType, ulong, out IntPtr, uint> AllocPages;



        // efi_status(*freePages)(efi_physical_address_t memory, uint64_t pages);
        public delegate*<IntPtr, ulong, uint> FreePages;
        /*efi_status(*getMemoryMap)(
            uint64_t* memory_map_sizes,
            efi_memory_desc_t* mem_descs,
            uint64_t* map_key,
            uint64_t* desc_size,
            uint32_t* desc_version);*/
        public delegate*<ref ulong, ref EFIMemoryDescriptor, ref ulong, ref ulong, ref ulong, uint> GetMemoryMap;
        // efi_status(*allocPool)(efi_memory_type_e type, uint64_t size, void** pool_addr);
        public delegate*<EFIMemroyType, ulong, ref IntPtr, uint> AllocPool;


        //efi_status(*freePool)(void* pool_addr);
        public delegate*<IntPtr, uint> FreePool;
        /*efi_status(*createEvent)(
        uint32_t type,
        efi_tpl_t notify_tpl,
        EFI_EVT_NOTIFY notify_func,
        void* notify_ctx,
        efi_evt_t* evt);*/
        public delegate*<uint> CreateEvent;



        /*efi_status(*setTimer)(
            efi_evt_t evt,
            efi_timer_delay_e type,
            uint64_t trigger_time);*/
        public delegate*<uint> SetTimer;


        //efi_status(*signalEvent)(efi_evt_t evt);
        public delegate*<IntPtr, uint> SignalEvent;
        //efi_status(*waitForEvent)(uint64_t evt_count, efi_evt_t* evt, uint64_t* idx);
        public delegate*<ulong, ref IntPtr, ref ulong, uint> WaitForEvent;

        //efi_status(*closeEvent)(efi_evt_t evt);
        public delegate*<IntPtr, uint> CloseEvent;
        //efi_status(*checkEvent)(efi_evt_t evt);
        public delegate*<IntPtr, uint> CheckEvent;


        /*efi_status(*installProtocolInterface)(
            efi_handle_t* handle,
            efi_guid_t* guid,
            efi_interface_type_e type,
            void*interface);*/
        public delegate*<ref IntPtr, ref EFIGuid, uint, void*, uint> InstallProtocolInterface;

        /*efi_status(*reinstallProtocolInterface)(
            efi_handle_t* handle,
            efi_guid_t* guid,
            void* old_interface,
            void* new_interface);*/
        public delegate*<ref IntPtr, ref EFIGuid, void*, void*, uint> ReinstallProtocolInterface;
        /*efi_status(*uninstallProtocolInterface)(
        efi_handle_t* handle,
        efi_guid_t* guid,
        void* interace);*/
        public delegate*<ref IntPtr, ref EFIGuid, void*, uint> UninstallProtocolInterface;

        /*efi_status(*handleProtocol)(
            efi_handle_t handle,
            efi_guid_t* guid,
            void**interface);*/
        public delegate*<IntPtr, ref EFIGuid, ref void*, uint> HandleProtocol;

        public void* Reserved;

        /*efi_status(*registerProtocolNotify)(
            efi_guid_t* protocol,
            efi_evt_t evt,
            void** registration);*/
        public delegate*<ref EFIGuid, ulong, ref void*, uint> RegisterProtocolNotify;

        /*efi_status(*locateHandle)(
            efi_locate_search_e type,
            efi_guid_t* guid,
            void* key,
            uint64_t* buffer_size,
            efi_handle_t* buffer);*/
        public delegate*<EFILocateSearch, ref EFIGuid, void*, ref ulong, ref IntPtr, uint> LocateHandle;

        /*efi_status(*locateDevicePath)(
            efi_guid_t* guid,
            void** dev_path,
            efi_handle_t* dev);*/
        public delegate*<ref EFIGuid, ref void*, ref IntPtr, uint> LocateDevicePath;
        //efi_status(*installConfigTable)( efi_guid_t* guid,void* table);
        public delegate*<ref EFIGuid, void*, uint> InstallConfigTable;

        /*efi_status(*imageLoad)(
            uint8_t boot_policy,
            efi_handle_t parent_image,
            void* dev_path,
            void* src_buffer,
            uint64_t srcSize,
            efi_handle_t* image_handle);*/
        public delegate*<byte, IntPtr, void*, void*, ulong, ref IntPtr, uint> LoadImage;
        /*fi_status(*imageStart)(
            efi_handle_t image_handle,
            uint64_t* exit_data_size,
            uint16_t** exit_data);*/
        public delegate*<IntPtr, ref ulong, ref byte*, uint> StartImage;
        /*efi_status(*exit)(
            efi_handle_t image_handle,
            efi_status exit_code,
            uint64_t exit_data_size,
            uint16_t* exit_data);*/
        public delegate*<IntPtr, uint, ulong, byte*, uint> Exit;
        /*efi_status(*imageUnload)(
            efi_handle_t image_handle);*/
        public delegate*<IntPtr, uint> UnloadImage;
        /*efi_status(*exitBootServices)(
            efi_handle_t image_handle,
            uint64_t map_key);*/
        public delegate*<IntPtr, ulong, uint> ExitBootServices;

        /*efi_status(*getNextMonoCount)(
            uint64_t* count);*/
        public delegate*<ulong, uint> GetNextMonoCount;

        //efi_status(*stall)(uint64_t millsec);
        public delegate*<ulong, uint> Stall;
        /*efi_status(*setWatchDog)(
            uint64_t timeout,
            uint64_t wd_code,
            uint64_t data_size,
            uint16_t* wd_data);*/
        public delegate*<ulong, ulong, ulong, byte*, uint> SetWatchDog;

        /*efi_status(*connectController)(
            efi_handle_t controller_handle,
            efi_handle_t* drv_image_handle,
            void* dev_path,
            uint8_t recursive);*/
        public delegate*<IntPtr, ref IntPtr, void*, byte, uint> ConnectController;
        /*efi_status(*disconnectController)(
            efi_handle_t controller_handle,
            efi_handle_t drv_image_handle,
            efi_handle_t child_handle);*/
        public delegate*<IntPtr, IntPtr, IntPtr, uint> DisconnectController;

        /*efi_status(*openProtocol)(
            efi_handle_t handle,
            efi_guid_t* guid,
            void**interface,
            efi_handle_t agent,
            efi_handle_t ctrl_handle,
            uint32_t attribute);*/
        public delegate*<IntPtr, ref EFIGuid, out void*, IntPtr, IntPtr, EFIOpenProtocolAttribute, uint> OpenProtocol;

        /*efi_status(*closeProtocol)(
            efi_handle_t handle,
            efi_guid_t* guid,
            efi_handle_t agent,
            efi_handle_t ctrl_handle);*/
        public delegate*<IntPtr, ref EFIGuid, IntPtr, IntPtr, uint> CloseProtocol;
        /*efi_status(*openProtocolInfo)(
            efi_handle_t handle,
            efi_guid_t* guid,
            efi_open_protocol_info_entry_t** entries,
            uint64_t* entry_count);*/
        public delegate*<IntPtr, ref EFIGuid, ref EFIOpenProtocolEntry*, ref ulong, uint> OpenProtocolInfo;

        /*efi_status(*protocolsPerHandle)(
            efi_handle_t handle,
            efi_guid_t*** protocol_buffer,
            uint64_t* protocol_buffer_length);*/
        public delegate*<IntPtr, ref EFIGuid**, ref ulong, uint> ProtocolsPerHandle;

        /*efi_status(*locateHandleBuffer)(
            efi_locate_search_e type,
            efi_guid_t* guid,
            void* key,
            uint64_t* no_handles,
            efi_handle_t** buffer);*/
        public delegate*<EFILocateSearch, ref EFIGuid, void*, ref ulong, ref IntPtr*, uint> LocateHandleBuffer;
        /*efi_status(*locateProtocol)(
            efi_guid_t* guid,
            void* registration,
            void**interface);*/
        public delegate*<ref EFIGuid, void*, ref void*, uint> LocateProtocol;

        /*efi_status(*installMultiProtocolInterface)(
            efi_handle_t* handle,
            ...);*/
        public void* InstallMultiProtocolInterface;
        /*efi_status(*uninstallMultiProtocolInterface)(
            efi_handle_t* handle,
            ...);*/
        public void* UninstallMultiProtocolInterface;

        /*efi_status(*calcCRC32)(
            void* data,
            uint64_t size,
            uint32_t* crc32);*/
        public delegate*<void*, ulong, ref uint, uint> CalculateCRC32;

        /*efi_status(*copyMemory)(
            void* dest,
            void* src,
            uint64_t length);*/
        public delegate*<void*, void*, ulong, uint> CopyMemory;
        /*efi_status(*setMemory)(
            void* buffer,
            uint64_t size,
            uint8_t data);*/
        public delegate*<void*, ulong, byte> SetMemory;

        /*efi_status(*createEventEx)(
            uint32_t type,
            efi_tpl_t notify_tpl,
            EFI_EVT_NOTIFY notify_func,
            void* notify_ctx,
            efi_guid_t* evt_group,
            efi_evt_t* evt);
        }*/
        public void* CreateEventEx;
    }

    public unsafe struct _EFI_BOOT_SERVICES {
        public readonly EFI_TABLE_HEADER Hedaer;
        public readonly EFI_BOOT_SERVICES_MT Methods;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct EFI_TABLE_HEADER {
        public readonly ulong Signature;
        public readonly uint Revision;
        public readonly uint HeaderSize;
        public readonly uint Crc32;
        public readonly uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct EFI_SYSTEM_TABLE {
        public readonly EFI_TABLE_HEADER Hdr;
        public readonly char* FirmwareVendor;
        public readonly uint FirmwareRevision;
        public readonly void* ConsoleInHandle;
        public readonly void* ConIn;
        public readonly void* ConsoleOutHandle;
        public readonly EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL* ConOut;
        public readonly void* ConsoleErrHandle;
        public readonly void* ConsoleErr;

        public readonly void* RuntimeServices;
        public readonly _EFI_BOOT_SERVICES* BootServices;

        public readonly ulong TableLength;
    }
    unsafe static class FMT { 
        public static void PrintNewLine(delegate*<ushort*, void> printFunc) {
            var buffer = stackalloc ushort[3];
            buffer[0] = '\r';
            buffer[1] = '\n';
            buffer[2] = 0;
            printFunc(buffer);
        }
        public static void PrintString(delegate*<ushort*,void> printFunc,string str) {
            var strLength = str.Length;
            var strBuffer = str.buffer;
            var printBuffer = stackalloc ushort[strLength + 1];
            for(var i = 0; i < strLength; i++) {
                printBuffer[i] = strBuffer[i];
            }
            printBuffer[strLength] = 0;
            printFunc(printBuffer);
        }
        public static void PrintString(delegate*<ushort*,void> printFunc,byte *str) {
            var strLength = 0;
            for (var i = str; *i != 0; i++, strLength++) ;
            var strBuffer = str;
            var printBuffer = stackalloc ushort[strLength + 1];
            for(var i = 0; i < strLength; i++) {
                printBuffer[i] = strBuffer[i];
            }
            printBuffer[strLength] = 0;
            printFunc(printBuffer);
        }
        public static void PrintFormat(delegate*<ushort*, void> printFunc, string str, __arglist) {
            var arglist = __arglist;
            var valist = arglist.m_Valist;
            var strLength = str.Length;
            var strBuffer = str.buffer;
            var bufferPtr = 0;
            var printBuffer = stackalloc ushort[strLength + 1];
            for (var i = 0; i < strLength; i++) {
                if (strBuffer[i] == '%') {
                    printBuffer[bufferPtr++] = 0x0;
                    printFunc(printBuffer);
                    bufferPtr = 0;

                    switch (strBuffer[++i]) {
                        case (byte)'d': {
                            var value = valist.GetNextValue<long>();
                            PrintNumber(printFunc, value);
                            break;
                        }
                        case (byte)'u': {
                            var value = valist.GetNextValue<ulong>();
                            PrintNumber(printFunc, value);
                            break;
                        }
                        case (byte)'s': {
                            var value = valist.GetNextValue<IntPtr>();
                            PrintString(printFunc, (byte*)value);
                            break;
                        }
                        case (byte)'S': {
                            var value = valist.GetNextValue<IntPtr>();
                            printFunc((ushort*)value);
                            break;
                        }
                        default: {
                            printBuffer[0] = strBuffer[i];
                            printBuffer[1] = 0x0;
                            printFunc(printBuffer);
                            break;
                        }
                    }
                } else {
                    printBuffer[bufferPtr++] = strBuffer[i];
                }
            }
            if (bufferPtr != 0) {
                printBuffer[bufferPtr++] = 0;
                printFunc(printBuffer);
            }
        }
        public static void PrintNumber(delegate*<ushort*, void> printFunc,long value) {
            var length = 0;
            var isNeg = false;
            if (value < 0) {
                value = -value;
                length = 1;
                isNeg = true;
            }
            var lenValue = value;
            do {
                length++;
                lenValue /= 10;
            } while (lenValue != 0);
            var numberBuffer = stackalloc ushort[length + 1];
            for(var i = length - 1; i >= (isNeg?1:0); i--, value/=10) {
                numberBuffer[i] = (ushort)((value % 10) + 48);
            }
            if (isNeg) numberBuffer[0] = '-';
            numberBuffer[length] = 0;
            printFunc(numberBuffer);
        }
        public static void PrintNumber(delegate*<ushort*, void> printFunc, ulong value) {
            var length = 0;
            var lenValue = value;
            do {
                length++;
                lenValue /= 10;
            } while (lenValue != 0);
            var numberBuffer = stackalloc ushort[length + 1];
            for (var i = length - 1; i >= 0; i--, value /= 10) {
                numberBuffer[i] = (ushort)((value % 10) + 48);
            }
            numberBuffer[length] = 0;
            printFunc(numberBuffer);
        }
    }
    unsafe static class IoHelpers {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void IoWriteByte(ushort port, byte value) {
            UnsafeAsm.InlineAsm("outb $0,$1", "r,{dx}", __arglist((byte)value, (ushort)port));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static byte IoReadByte(ushort port) {
            return UnsafeAsm.InlineAsm8("inb $1,$0", "={al},{dx}", __arglist((ushort)port));
        }

    }
    public unsafe struct EFIFileToken {
        public readonly ulong Event;
        public readonly ulong Status;
        public readonly ulong BufferSize;
        public readonly void* Buffer;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EFISimpleFSProtocol {
        public readonly ulong Reversion;
        public delegate*<EFISimpleFSProtocol*, out EFIFileProtocol*, uint> OpenVolume;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EFIFileProtocol {
        public readonly ulong Reversion;
        public readonly delegate*<EFIFileProtocol*, out EFIFileProtocol*, ushort*, FileMode, FileAttribute, uint> Open;
        public readonly delegate*<EFIFileProtocol*, uint> Close;
        public readonly delegate*<EFIFileProtocol*, uint> Delete;
        public readonly delegate*<EFIFileProtocol*, ref ulong, void*,uint> Read;
        public readonly delegate*<EFIFileProtocol*, ref ulong, void*, uint> Write;
        public readonly delegate*<EFIFileProtocol*, ref ulong, uint> GetPosition;
        public readonly delegate*<EFIFileProtocol*, ulong, uint> SetPosition;
        public readonly delegate*<EFIFileProtocol*, ref EFIGuid, ref ulong, void*,uint> GetInfo;
        public readonly void* SetInfo;
        public readonly delegate*<EFIFileProtocol*, uint> Flush;
        public readonly delegate*<EFIFileProtocol*, out EFIFileProtocol*, ushort*, ulong, ulong, ref EFIFileToken, uint> OpenEx;
        public readonly delegate*<EFIFileProtocol*, ref EFIFileToken, uint> ReadEx;
        public readonly delegate*<EFIFileProtocol*, ref EFIFileToken, uint> WriteEx;
        public readonly delegate*<EFIFileProtocol*, ref EFIFileToken, uint> FlushEx;


        //public readonly static EFIGuid FileSystemGUID = new(0x11d26459964e5b22, 0x3b7269c9a000398e);
        public static void InitGUID(out EFIGuid guid) {
            guid = default;
            
            guid.uuPart0 = 0x11d26459964e5b22;
            guid.uuPart3 = 0x3b7269c9a000398e;
        }
        public static void InitFileInfoGUID(out EFIGuid guid) {
            guid = default;
            guid.uuPart0 = 0x11d26d3f09576e92;
            guid.uuPart3 = 0x3b7269c9a000398e;
        }
        public static void InitVolNameGUID(out EFIGuid guid) {
            guid = default;
            guid.uuPart0 = 0x11d3fe81db47d7d3;
            guid.uuPart3 = 0x4dc13f279000359a;
        }
    }
    unsafe static class SerialPortHelpers {
        public static T SerialRead<T>(ushort port) where T : unmanaged {
            T result = default;
            SerialReadBuffer(port, (byte*)(&result), (uint)sizeof(T));
            return result;
        }
        public static void SerialReadBuffer(ushort port,byte* buffer, uint length) {
            for(var i = 0u; i < length; i++) {
                var lineStatus = 0;
                do {
                    lineStatus = IoHelpers.IoReadByte((ushort)(port + 5));
                } while ((lineStatus & 0x1) == 0);
                buffer[i] = IoHelpers.IoReadByte(port);
            }
        }
        public static void SerialWriteBuffer(ushort port,byte*buffer, uint length) {
            for (var i = 0u; i < length; i++) {
                var lineStatus = 0;
                do {
                    lineStatus = IoHelpers.IoReadByte((ushort)(port + 5));
                } while ((lineStatus & 0x20) == 0);
                IoHelpers.IoWriteByte(port, buffer[i]);
            }
        }
        public static void SerialPortInit(ushort port) {
            IoHelpers.IoWriteByte((ushort)(port+3),0x3);
        }
    }
    public enum EFIOpenProtocolAttribute {
        ByHandleProtocol = 0x1,
        GetProtocol = 0x2,
        TestProtocol = 0x4,
        ByChildrenController = 0x8,
        ByDriver = 0x10,
        Exclusive = 0x20
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct EFITime {
        public ushort tiYear;
        public byte tiMonth;
        public byte tiDay;
        public byte tiHour;
        public byte tiMinute;
        public byte tiSecond;
        public byte padding0;

        public uint tiNanoSec;
        public short tiTimeZone;
        public byte tiDayLight;
        public byte padding1;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct EFIFileInfo {
        public ulong Size;
        public ulong FileSize;
        public ulong PhysicalSize;
        public EFIGuid CreationTime;
        public EFIGuid AccessTime;
        public EFIGuid ModificationTime;
        public ulong Attribute;
        public ushort FileName;
    }
    public enum LoaderCommand {
        None = 0x0,
        LoadBuffer = 0x1,
        ListDrive = 0x2,
        WriteFile = 0x3,
        Exit = 0x10,
    }
    public enum FileMode: ulong {
        Read = 0x0000000000000001UL,
        Write = 0x0000000000000002UL,
        Create = 0x8000000000000000UL

    }
    public enum FileAttribute:ulong {
        None = 0x0,
        ReadOnly = 0x0000000000000001UL,
        Hidden = 0x0000000000000002UL,
        System = 0x0000000000000004UL,
        Directory = 0x0000000000000010UL,
        Archive = 0x0000000000000020UL,
        ValidAttrib = 0x0000000000000037UL
    }
    public unsafe abstract class DebugPort {
        public abstract void WriteBuffer(byte* buffer, uint length);
        public abstract void ReadBuffer(byte* buffer, uint length);
        public T Read<T>() where T : unmanaged {
            T result = default;
            ReadBuffer((byte*)(&result), (uint)sizeof(T));
            return result;
        }
        public void Write<T>( T value) where T : unmanaged {
            WriteBuffer((byte*)(&value), (uint)sizeof(T));
        }
    }
    public unsafe class SerialDebugPort: DebugPort {
        protected ushort m_SerialIoPort;
        public SerialDebugPort(ushort port) {
            m_SerialIoPort = port;
            IoHelpers.IoWriteByte((ushort)(port + 3), 0x80);
            IoHelpers.IoWriteByte((ushort)(port + 0), 0x01);
            IoHelpers.IoWriteByte((ushort)(port + 1), 0x00);

            IoHelpers.IoWriteByte((ushort)(port + 3), 0x3);
            
        }
        public override void ReadBuffer(byte* buffer, uint length) {
            var port = m_SerialIoPort;
            for (var i = 0u; i < length; i++) {
                var lineStatus = 0;
                do {
                    lineStatus = IoHelpers.IoReadByte((ushort)(port + 5));
                    
                } while ((lineStatus & 0x1) == 0);
                buffer[i] = IoHelpers.IoReadByte(port);
            }
        }
        public override void WriteBuffer(byte* buffer, uint length) {
            var port = m_SerialIoPort;
            for (var i = 0u; i < length; i++) {
                var lineStatus = 0;
                do {
                    lineStatus = IoHelpers.IoReadByte((ushort)(port + 5));
                } while ((lineStatus & 0x20) == 0);
                IoHelpers.IoWriteByte(port, buffer[i]);
            }
        }
    }
    public unsafe class DebugClient {
        protected DebugPort m_DebugPort;
        protected EFI_SYSTEM_TABLE* m_SystemTable;
        protected EFI_BOOT_SERVICES_MT* m_BootServices;
        protected IntPtr m_ImageHandle;
        public DebugClient(DebugPort port, EFI_SYSTEM_TABLE *systemTable, IntPtr imageHandle) {
            m_DebugPort = port;
            m_SystemTable = systemTable;
            m_BootServices = &systemTable->BootServices->Methods;
            m_ImageHandle = imageHandle;
        }
        protected void CmdListDrive() {
            var bsMT = m_BootServices;
            var appImageHandle = m_ImageHandle;
            //var sfsGUID = EFIFileProtocol.FileSystemGUID;
            EFIFileProtocol.InitGUID(out var sfsGUID);
            var dbgPort = m_DebugPort;

            EFIFileProtocol.InitVolNameGUID(out var volNameProtocol);
            var handleCount = 0ul;
            IntPtr* handles = null;
            bsMT->LocateHandleBuffer(EFILocateSearch.ByProtocol, ref sfsGUID, null, ref handleCount, ref handles);

            FMT.PrintFormat(&App.PrintString, "[INFO] [ListDrive] Find %d drives\r\n",__arglist(handleCount));

            dbgPort.Write((uint)handleCount);

            for (var i = 0u; i < handleCount; i++) {
                bsMT->OpenProtocol(handles[i], ref sfsGUID, out var sfsProtocol, appImageHandle, IntPtr.Zero, EFIOpenProtocolAttribute.ByHandleProtocol);

                var protocolBody = (EFISimpleFSProtocol*)sfsProtocol;
                protocolBody->OpenVolume(protocolBody, out var rootProtocol);

                var volNameSize = 0ul;
                rootProtocol->GetInfo(rootProtocol, ref volNameProtocol, ref volNameSize, null);
                var volNameBuffer = stackalloc byte[(int)(volNameSize + 1)];
                rootProtocol->GetInfo(rootProtocol, ref volNameProtocol, ref volNameSize, volNameBuffer);

                FMT.PrintFormat(&App.PrintString, "[INFO] [ListDrive] Handle = %u, Name = %S\r\n", __arglist(rootProtocol, volNameBuffer));

                dbgPort.Write(handles[i]);
                dbgPort.Write((uint)volNameSize);
                dbgPort.WriteBuffer(volNameBuffer, (uint)volNameSize);

                bsMT->CloseProtocol(handles[i], ref sfsGUID, appImageHandle, IntPtr.Zero);
            }
        }
        public void CmdLoadBuffer() {
            var bsMT = m_BootServices;
            var appImageHandle = m_ImageHandle;
            var dbgPort = m_DebugPort;

            var bufferSize = dbgPort.Read<uint>();
            var allocResult = bsMT->AllocPages(EFIAllocType.AllocateAnyPages, EFIMemroyType.EfiBootServiceData, Math.AlignCeil(bufferSize, 4096), out var bufferPtr);
            if (allocResult != 0) {
                FMT.PrintFormat(&App.PrintString,"[ERROR] [LoadBuffer] Unable to allocate buffer [Size = %d, Result = %d]\r\n", __arglist(bufferSize, allocResult));
                App.FatalError("Unable to allocate memory\r\n");
            }

            FMT.PrintFormat(&App.PrintString, "[INFO] [LoadBuffer] Buffer = %d\r\n", __arglist(bufferPtr));

            var recvSize = 0u;
            var pBuffer = (byte*)bufferPtr;
            for (var i = 0u; i < bufferSize; ) {
                recvSize = dbgPort.Read<uint>() ;

                dbgPort.ReadBuffer(&pBuffer[i], recvSize);
                i += recvSize;

                FMT.PrintFormat(&App.PrintString, "[INFO] [LoadBuffer] Progress: %d/%d\r", __arglist(bufferSize, i));
            }
            FMT.PrintString(&App.PrintString, "\r\n[INFO] [LoadBuffer] Receive complete\r\n");

            dbgPort.Write(bufferPtr);
        }
        public void CmdWriteFile() {
            var dbgPort = m_DebugPort;
            var bsMT = m_BootServices;
            EFIFileProtocol.InitGUID(out var sfsGUID);
            //var sfsGUID = EFIFileProtocol.FileSystemGUID;
            var volumeProtocolHandle = dbgPort.Read<IntPtr>();

            bsMT->OpenProtocol(volumeProtocolHandle, ref sfsGUID, out var sfsProtocol, m_ImageHandle, IntPtr.Zero, EFIOpenProtocolAttribute.ByHandleProtocol);
            var root = (EFISimpleFSProtocol*)sfsProtocol;

            root->OpenVolume(root, out var fileRoot);

            var pathSize = dbgPort.Read<int>();
            var nameBuffer = stackalloc ushort[pathSize];
            dbgPort.ReadBuffer((byte*)nameBuffer, (uint)pathSize * 2);

            FMT.PrintFormat(&App.PrintString, "[INFO] [WriteFile] Write file %S\r\n", __arglist(nameBuffer));

            if (fileRoot->Open(fileRoot, out var file, nameBuffer, FileMode.Read | FileMode.Write, FileAttribute.None) == 0) {
                FMT.PrintString(&App.PrintString, "[INFO] [WriteFile] Origin file exist, Deleted\r\n");
                file->Delete(file);
            }


            var result = fileRoot->Open(fileRoot, out file, nameBuffer, FileMode.Create | FileMode.Read | FileMode.Write, FileAttribute.None);

            FMT.PrintFormat(&App.PrintString, "[INFO] [WriteFile] File = %d, Result = %d\r\n", __arglist(file, result));
            file->SetPosition(file, 0);

            var bufferAddress = (byte*)dbgPort.Read<IntPtr>();
            var bufferLength = (ulong)dbgPort.Read<uint>();

            FMT.PrintFormat(&App.PrintString, "[INFO] [WriteFile] Buffer = %d, Length = %d\r\n", __arglist(bufferAddress, bufferLength));

            file->Write(file, ref bufferLength, bufferAddress);

            file->Flush(file);
            file->Close(file);
            fileRoot->Close(fileRoot);

            bsMT->CloseProtocol(volumeProtocolHandle, ref sfsGUID, m_ImageHandle, IntPtr.Zero);

            FMT.PrintFormat(&App.PrintString, "[INFO] [WriteFile] Write file %S complete\r\n",__arglist(nameBuffer));

        }
        public void DebugLoop() {
            FMT.PrintString(&App.PrintString, "Enter debug loop\r\n");
            
            while (true) {
                var command = m_DebugPort.Read<uint>();
                FMT.PrintFormat(&App.PrintString, "Command = %d\r\n", __arglist(command));
                switch ((LoaderCommand)command) {
                    case LoaderCommand.Exit: {
                        FMT.PrintString(&App.PrintString, "Debug Exit\r\n");
                        return;
                    }
                    case LoaderCommand.None: continue;
                    case LoaderCommand.ListDrive: {
                        CmdListDrive();
                        break;
                    }
                    case LoaderCommand.LoadBuffer: {
                        CmdLoadBuffer();
                        break;
                    }
                    case LoaderCommand.WriteFile: {
                        CmdWriteFile();
                        break;
                    }
                }
            }
        }
        
    }
    unsafe internal class App {
        public static void FatalError(string message) {
            FMT.PrintString(&App.PrintString, message);
            while (true) {
                UnsafeAsm.InlineAsm("hlt", "",__arglist());
            }
        }
        [RuntimeExport("__chkstk")]
        public static void StackCheck() { }
        public static EFI_SYSTEM_TABLE* m_SystemTable;
        public static EFI_BOOT_SERVICES_MT* m_BootServices;
        public static void PrintString(ushort* str) {
            var protocol = m_SystemTable->ConOut;
            protocol->OutputString(protocol, str);
        }

        public static IntPtr EfiMain(IntPtr imageHandle, EFI_SYSTEM_TABLE* systemTable) {
            m_SystemTable = systemTable;
            var bsMT = m_BootServices = &m_SystemTable->BootServices->Methods;

            m_BootServices->AllocPages(EFIAllocType.AllocateAnyPages, EFIMemroyType.EfiLoaderData, 32, out var heapMemory);
            RuntimeHelpers.SetGCHeapStart((byte*)heapMemory);

            RuntimeHelpers.RunStaticConstructors();


            FMT.PrintString(&PrintString, "UEFI Remote Kernel Deployment Toolkit v0\r\n");



            var debugPort = new SerialDebugPort(0x2E8);
            var debugClient = new DebugClient(debugPort,systemTable,imageHandle);

            debugClient.DebugLoop();

            FMT.PrintString(&App.PrintString, "Exit\r\n");

            return IntPtr.Zero;

        }
    }
}
