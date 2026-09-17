using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;

namespace FlasherCore.Firmware.Unpackers
{
    internal class UpdateAppUnpacker : BaseUnpacker, IDisposable
    {
        // Magic: 0x55, 0xAA, 0x5A, 0xA5 (Little Endian: 0xA55AAA55)
        private const uint HeaderMagic = 0xA55AAA55;
        private const int HeaderStructSize = 98; // sizeof(UpdateAppFileInfo)

        // 内部包装结构，保存文件信息和计算出的数据绝对偏移
        private struct EntryInfo
        {
            public UpdateAppFileInfo Info;
            public long DataOffset;
        }

        private EntryInfo[] _entries;

        public UpdateAppUnpacker(Stream source, uint bufferSize, bool leaveOpen = false)
            : base(source, bufferSize, leaveOpen)
        {
            _entries = Array.Empty<EntryInfo>();
        }

        public override int FileInfoCount => _entries.Length;
        public override UPFirmwareType FirmwareType => UPFirmwareType.UpdateApp;

        public override bool Parse()
        {
            try
            {
                // Huawei UPDATE.APP 通常从 92 字节处开始解析 Entry
                if (_source.Length < 92) return false;
                _source.Seek(92, SeekOrigin.Begin);

                var entriesList = new List<EntryInfo>();
                long streamLength = _source.Length;

                // 使用栈内存作为临时 Header 缓冲区，避免 GC
                Span<byte> headerBuffer = stackalloc byte[HeaderStructSize];

                while (_source.Position < streamLength)
                {
                    long currentStartPos = _source.Position;

                    // 1. 预读取 Magic (4 bytes)
                    // 注意：这里我们预读 headerBuffer 的前4字节来检查 Magic，
                    // 如果匹配，说明这是一个有效的 header，然后读取剩余部分。
                    // 为了性能，我们直接尝试读取整个 Struct 大小，然后检查 HeaderId 字段。

                    int read = _source.Read(headerBuffer);
                    if (read < HeaderStructSize) break; // EOF or partial header

                    // 2. 将字节转换为结构体
                    var info = MemoryMarshal.Read<UpdateAppFileInfo>(headerBuffer);

                    // 3. 校验 Magic
                    if (info.HeaderId != HeaderMagic)
                    {
                        // 如果 Magic 不匹配，说明可能解析错误或文件结束/填充
                        // 尝试回退并停止 (参考原逻辑 throw InvalidFile)
                        // 这里我选择安全退出
                        break;
                    }

                    // 4. 计算偏移量
                    // 数据起始位置 = 当前Header起始位置 + HeaderSize (HeaderSize 包含了 HeaderStruct 及其后的 Padding/Checksum)
                    long dataOffset = currentStartPos + info.HeaderSize;

                    // 添加到列表
                    entriesList.Add(new EntryInfo
                    {
                        Info = info,
                        DataOffset = dataOffset
                    });

                    // 5. 跳转到下一个 Header
                    // 下一个 Header 位置 = dataOffset + FileSize + 4字节对齐
                    long nextOffset = dataOffset + info.FileSize;

                    // 4字节对齐处理
                    long remainder = nextOffset % 4;
                    if (remainder > 0)
                    {
                        nextOffset += (4 - remainder);
                    }

                    _source.Seek(nextOffset, SeekOrigin.Begin);
                }

                _entries = entriesList.ToArray();
                return _entries.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public override bool DumpToStream(Stream output, int id)
        {
            if (id < 0 || id >= _entries.Length) return false;

            var entry = _entries[id];

            _source.Seek(entry.DataOffset, SeekOrigin.Begin);

            long remaining = entry.Info.FileSize;
            long total = remaining;
            var speedTracker = new SpeedTracker(_speed);

            int bufSize = (int)_bufferSize;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);

            try
            {
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(bufSize, remaining);
                    int read = _source.Read(buffer, 0, toRead);
                    if (read == 0) break;

                    output.Write(buffer, 0, read);
                    remaining -= read;

                    speedTracker.Update((ulong)read);
                    _progress?.Invoke((ulong)(total - remaining), (ulong)total);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
        {
            if (_entries.Length == 0) return 0;

            int count = Math.Min(entries.Length, _entries.Length);

            fixed (UPFirmwareEntry* pDestBase = entries)
            fixed (EntryInfo* pSourceBase = _entries)
            {
                for (int i = 0; i < count; i++)
                {
                    UPFirmwareEntry* pDest = pDestBase + i;
                    EntryInfo* pSrc = pSourceBase + i; 

                    pDest->Index = (ushort)i;
                    pDest->Start = (ulong)pSrc->DataOffset;
                    pDest->Length = pSrc->Info.FileSize;
                    pDest->RealLength = pSrc->Info.FileSize;

                    new Span<byte>(pDest->Name, 128).Clear();
                    new Span<byte>(pDest->FileName, 128).Clear();

                    var type = pSrc->Info.GetFileType();
                    type.CopyToPtr(pDest->Name, 128);
                    (type+ ".img").CopyToPtr(pDest->FileName, 128);
                }
            }

            return count;
        }
        public override bool OpenEntryStream(out Stream? output, ushort id)
        {
            output = null;
            if (id < 0 || id >= _entries.Length) return false;

            var entry = _entries[id];

            try
            {
                output = new SubStream(_source, entry.DataOffset, entry.Info.FileSize);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to open UpdateApp entry stream: {e.Message}");
                return false;
            }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}