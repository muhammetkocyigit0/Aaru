// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : AppleDouble.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Filters.
//
// --[ Description ] ----------------------------------------------------------
//
//     Provides a filter to open AppleDouble files.
//
// --[ License ] --------------------------------------------------------------
//
//     This library is free software; you can redistribute it and/or modify
//     it under the terms of the GNU Lesser General Public License as
//     published by the Free Software Foundation; either version 2.1 of the
//     License, or (at your option) any later version.
//
//     This library is distributed in the hope that it will be useful, but
//     WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//     Lesser General Public License for more details.
//
//     You should have received a copy of the GNU Lesser General Public
//     License along with this library; if not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2020 Natalia Portillo
// ****************************************************************************/

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Aaru.CommonTypes.Interfaces;
using Aaru.Helpers;
using Marshal = Aaru.Helpers.Marshal;

namespace Aaru.Filters
{
    /// <summary>Decodes AppleDouble files</summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public sealed class AppleDouble : IFilter
    {
        const uint MAGIC    = 0x00051607;
        const uint VERSION  = 0x00010000;
        const uint VERSION2 = 0x00020000;
        readonly byte[] _dosHome =
        {
            0x4D, 0x53, 0x2D, 0x44, 0x4F, 0x53, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };

        readonly byte[] _macintoshHome =
        {
            0x4D, 0x61, 0x63, 0x69, 0x6E, 0x74, 0x6F, 0x73, 0x68, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };
        readonly byte[] _osxHome =
        {
            0x4D, 0x61, 0x63, 0x20, 0x4F, 0x53, 0x20, 0x58, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };
        readonly byte[] _proDosHome =
        {
            0x50, 0x72, 0x6F, 0x44, 0x4F, 0x53, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };
        readonly byte[] _unixHome =
        {
            0x55, 0x6E, 0x69, 0x78, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };
        readonly byte[] _vmsHome =
        {
            0x56, 0x41, 0x58, 0x20, 0x56, 0x4D, 0x53, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
        };
        string            _basePath;
        DateTime          _creationTime;
        AppleDoubleEntry  _dataFork;
        AppleDoubleHeader _header;
        string            _headerPath;
        DateTime          _lastWriteTime;
        bool              _opened;
        AppleDoubleEntry  _rsrcFork;

        public string Name   => "AppleDouble";
        public Guid   Id     => new Guid("1B2165EE-C9DF-4B21-BBBB-9E5892B2DF4D");
        public string Author => "Natalia Portillo";

        public void Close() => _opened = false;

        public string GetBasePath() => _basePath;

        public DateTime GetCreationTime() => _creationTime;

        public long GetDataForkLength() => _dataFork.length;

        public Stream GetDataForkStream() => new FileStream(_basePath, FileMode.Open, FileAccess.Read);

        public string GetFilename() => Path.GetFileName(_basePath);

        public DateTime GetLastWriteTime() => _lastWriteTime;

        public long GetLength() => _dataFork.length + _rsrcFork.length;

        public string GetParentFolder() => Path.GetDirectoryName(_basePath);

        public string GetPath() => _basePath;

        public long GetResourceForkLength() => _rsrcFork.length;

        public Stream GetResourceForkStream()
        {
            if(_rsrcFork.length == 0)
                return null;

            return new OffsetStream(_headerPath, FileMode.Open, FileAccess.Read, _rsrcFork.offset,
                                    (_rsrcFork.offset + _rsrcFork.length) - 1);
        }

        public bool HasResourceFork() => _rsrcFork.length > 0;

        public bool Identify(byte[] buffer) => false;

        public bool Identify(Stream stream) => false;

        public bool Identify(string path)
        {
            string filename      = Path.GetFileName(path);
            string filenameNoExt = Path.GetFileNameWithoutExtension(path);
            string parentFolder  = Path.GetDirectoryName(path);

            parentFolder ??= "";

            if(filename is null ||
               filenameNoExt is null)
                return false;

            // Prepend data fork name with "R."
            string proDosAppleDouble = Path.Combine(parentFolder, "R." + filename);

            // Prepend data fork name with '%'
            string unixAppleDouble = Path.Combine(parentFolder, "%" + filename);

            // Change file extension to ADF
            string dosAppleDouble = Path.Combine(parentFolder, filenameNoExt + ".ADF");

            // Change file extension to adf
            string dosAppleDoubleLower = Path.Combine(parentFolder, filenameNoExt + ".adf");

            // Store AppleDouble header file in ".AppleDouble" folder with same name
            string netatalkAppleDouble = Path.Combine(parentFolder, ".AppleDouble", filename);

            // Store AppleDouble header file in "resource.frk" folder with same name
            string daveAppleDouble = Path.Combine(parentFolder, "resource.frk", filename);

            // Prepend data fork name with "._"
            string osxAppleDouble = Path.Combine(parentFolder, "._" + filename);

            // Adds ".rsrc" extension
            string unArAppleDouble = Path.Combine(parentFolder, filename + ".rsrc");

            // Check AppleDouble created by A/UX in ProDOS filesystem
            if(File.Exists(proDosAppleDouble))
            {
                var prodosStream = new FileStream(proDosAppleDouble, FileMode.Open, FileAccess.Read);

                if(prodosStream.Length > 26)
                {
                    byte[] prodosB = new byte[26];
                    prodosStream.Read(prodosB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(prodosB);
                    prodosStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by A/UX in UFS filesystem
            if(File.Exists(unixAppleDouble))
            {
                var unixStream = new FileStream(unixAppleDouble, FileMode.Open, FileAccess.Read);

                if(unixStream.Length > 26)
                {
                    byte[] unixB = new byte[26];
                    unixStream.Read(unixB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(unixB);
                    unixStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by A/UX in FAT filesystem
            if(File.Exists(dosAppleDouble))
            {
                var dosStream = new FileStream(dosAppleDouble, FileMode.Open, FileAccess.Read);

                if(dosStream.Length > 26)
                {
                    byte[] dosB = new byte[26];
                    dosStream.Read(dosB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(dosB);
                    dosStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by A/UX in case preserving FAT filesystem
            if(File.Exists(dosAppleDoubleLower))
            {
                var doslStream = new FileStream(dosAppleDoubleLower, FileMode.Open, FileAccess.Read);

                if(doslStream.Length > 26)
                {
                    byte[] doslB = new byte[26];
                    doslStream.Read(doslB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(doslB);
                    doslStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by Netatalk
            if(File.Exists(netatalkAppleDouble))
            {
                var netatalkStream = new FileStream(netatalkAppleDouble, FileMode.Open, FileAccess.Read);

                if(netatalkStream.Length > 26)
                {
                    byte[] netatalkB = new byte[26];
                    netatalkStream.Read(netatalkB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(netatalkB);
                    netatalkStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by DAVE
            if(File.Exists(daveAppleDouble))
            {
                var daveStream = new FileStream(daveAppleDouble, FileMode.Open, FileAccess.Read);

                if(daveStream.Length > 26)
                {
                    byte[] daveB = new byte[26];
                    daveStream.Read(daveB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(daveB);
                    daveStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by Mac OS X
            if(File.Exists(osxAppleDouble))
            {
                var osxStream = new FileStream(osxAppleDouble, FileMode.Open, FileAccess.Read);

                if(osxStream.Length > 26)
                {
                    byte[] osxB = new byte[26];
                    osxStream.Read(osxB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(osxB);
                    osxStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        return true;
                }
            }

            // Check AppleDouble created by UnAr (from The Unarchiver)
            if(!File.Exists(unArAppleDouble))
                return false;

            var unarStream = new FileStream(unArAppleDouble, FileMode.Open, FileAccess.Read);

            if(unarStream.Length <= 26)
                return false;

            byte[] unarB = new byte[26];
            unarStream.Read(unarB, 0, 26);
            _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(unarB);
            unarStream.Close();

            return _header.magic == MAGIC && (_header.version == VERSION || _header.version == VERSION2);
        }

        public bool IsOpened() => _opened;

        // Now way to have two files in a single byte array
        public void Open(byte[] buffer) => throw new NotSupportedException();

        // Now way to have two files in a single stream
        public void Open(Stream stream) => throw new NotSupportedException();

        public void Open(string path)
        {
            string filename      = Path.GetFileName(path);
            string filenameNoExt = Path.GetFileNameWithoutExtension(path);
            string parentFolder  = Path.GetDirectoryName(path);

            parentFolder ??= "";

            if(filename is null ||
               filenameNoExt is null)
                throw new ArgumentNullException(nameof(path));

            // Prepend data fork name with "R."
            string proDosAppleDouble = Path.Combine(parentFolder, "R." + filename);

            // Prepend data fork name with '%'
            string unixAppleDouble = Path.Combine(parentFolder, "%" + filename);

            // Change file extension to ADF
            string dosAppleDouble = Path.Combine(parentFolder, filenameNoExt + ".ADF");

            // Change file extension to adf
            string dosAppleDoubleLower = Path.Combine(parentFolder, filenameNoExt + ".adf");

            // Store AppleDouble header file in ".AppleDouble" folder with same name
            string netatalkAppleDouble = Path.Combine(parentFolder, ".AppleDouble", filename);

            // Store AppleDouble header file in "resource.frk" folder with same name
            string daveAppleDouble = Path.Combine(parentFolder, "resource.frk", filename);

            // Prepend data fork name with "._"
            string osxAppleDouble = Path.Combine(parentFolder, "._" + filename);

            // Adds ".rsrc" extension
            string unArAppleDouble = Path.Combine(parentFolder, filename + ".rsrc");

            // Check AppleDouble created by A/UX in ProDOS filesystem
            if(File.Exists(proDosAppleDouble))
            {
                var prodosStream = new FileStream(proDosAppleDouble, FileMode.Open, FileAccess.Read);

                if(prodosStream.Length > 26)
                {
                    byte[] prodosB = new byte[26];
                    prodosStream.Read(prodosB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(prodosB);
                    prodosStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = proDosAppleDouble;
                }
            }

            // Check AppleDouble created by A/UX in UFS filesystem
            if(File.Exists(unixAppleDouble))
            {
                var unixStream = new FileStream(unixAppleDouble, FileMode.Open, FileAccess.Read);

                if(unixStream.Length > 26)
                {
                    byte[] unixB = new byte[26];
                    unixStream.Read(unixB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(unixB);
                    unixStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = unixAppleDouble;
                }
            }

            // Check AppleDouble created by A/UX in FAT filesystem
            if(File.Exists(dosAppleDouble))
            {
                var dosStream = new FileStream(dosAppleDouble, FileMode.Open, FileAccess.Read);

                if(dosStream.Length > 26)
                {
                    byte[] dosB = new byte[26];
                    dosStream.Read(dosB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(dosB);
                    dosStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = dosAppleDouble;
                }
            }

            // Check AppleDouble created by A/UX in case preserving FAT filesystem
            if(File.Exists(dosAppleDoubleLower))
            {
                var doslStream = new FileStream(dosAppleDoubleLower, FileMode.Open, FileAccess.Read);

                if(doslStream.Length > 26)
                {
                    byte[] doslB = new byte[26];
                    doslStream.Read(doslB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(doslB);
                    doslStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = dosAppleDoubleLower;
                }
            }

            // Check AppleDouble created by Netatalk
            if(File.Exists(netatalkAppleDouble))
            {
                var netatalkStream = new FileStream(netatalkAppleDouble, FileMode.Open, FileAccess.Read);

                if(netatalkStream.Length > 26)
                {
                    byte[] netatalkB = new byte[26];
                    netatalkStream.Read(netatalkB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(netatalkB);
                    netatalkStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = netatalkAppleDouble;
                }
            }

            // Check AppleDouble created by DAVE
            if(File.Exists(daveAppleDouble))
            {
                var daveStream = new FileStream(daveAppleDouble, FileMode.Open, FileAccess.Read);

                if(daveStream.Length > 26)
                {
                    byte[] daveB = new byte[26];
                    daveStream.Read(daveB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(daveB);
                    daveStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = daveAppleDouble;
                }
            }

            // Check AppleDouble created by Mac OS X
            if(File.Exists(osxAppleDouble))
            {
                var osxStream = new FileStream(osxAppleDouble, FileMode.Open, FileAccess.Read);

                if(osxStream.Length > 26)
                {
                    byte[] osxB = new byte[26];
                    osxStream.Read(osxB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(osxB);
                    osxStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = osxAppleDouble;
                }
            }

            // Check AppleDouble created by UnAr (from The Unarchiver)
            if(File.Exists(unArAppleDouble))
            {
                var unarStream = new FileStream(unArAppleDouble, FileMode.Open, FileAccess.Read);

                if(unarStream.Length > 26)
                {
                    byte[] unarB = new byte[26];
                    unarStream.Read(unarB, 0, 26);
                    _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(unarB);
                    unarStream.Close();

                    if(_header.magic == MAGIC &&
                       (_header.version == VERSION || _header.version == VERSION2))
                        _headerPath = unArAppleDouble;
                }
            }

            var fs = new FileStream(_headerPath, FileMode.Open, FileAccess.Read);
            fs.Seek(0, SeekOrigin.Begin);

            byte[] hdrB = new byte[26];
            fs.Read(hdrB, 0, 26);
            _header = Marshal.ByteArrayToStructureBigEndian<AppleDoubleHeader>(hdrB);

            AppleDoubleEntry[] entries = new AppleDoubleEntry[_header.entries];

            for(int i = 0; i < _header.entries; i++)
            {
                byte[] entry = new byte[12];
                fs.Read(entry, 0, 12);
                entries[i] = Marshal.ByteArrayToStructureBigEndian<AppleDoubleEntry>(entry);
            }

            _creationTime  = DateTime.UtcNow;
            _lastWriteTime = _creationTime;

            foreach(AppleDoubleEntry entry in entries)
                switch((AppleDoubleEntryID)entry.id)
                {
                    case AppleDoubleEntryID.DataFork:
                        // AppleDouble have datafork in separated file
                        break;
                    case AppleDoubleEntryID.FileDates:
                        fs.Seek(entry.offset, SeekOrigin.Begin);
                        byte[] datesB = new byte[16];
                        fs.Read(datesB, 0, 16);

                        AppleDoubleFileDates dates =
                            Marshal.ByteArrayToStructureBigEndian<AppleDoubleFileDates>(datesB);

                        _creationTime  = DateHandlers.UnixUnsignedToDateTime(dates.creationDate);
                        _lastWriteTime = DateHandlers.UnixUnsignedToDateTime(dates.modificationDate);

                        break;
                    case AppleDoubleEntryID.FileInfo:
                        fs.Seek(entry.offset, SeekOrigin.Begin);
                        byte[] finfo = new byte[entry.length];
                        fs.Read(finfo, 0, finfo.Length);

                        if(_macintoshHome.SequenceEqual(_header.homeFilesystem))
                        {
                            AppleDoubleMacFileInfo macinfo =
                                Marshal.ByteArrayToStructureBigEndian<AppleDoubleMacFileInfo>(finfo);

                            _creationTime  = DateHandlers.MacToDateTime(macinfo.creationDate);
                            _lastWriteTime = DateHandlers.MacToDateTime(macinfo.modificationDate);
                        }
                        else if(_proDosHome.SequenceEqual(_header.homeFilesystem))
                        {
                            AppleDoubleProDOSFileInfo prodosinfo =
                                Marshal.ByteArrayToStructureBigEndian<AppleDoubleProDOSFileInfo>(finfo);

                            _creationTime  = DateHandlers.MacToDateTime(prodosinfo.creationDate);
                            _lastWriteTime = DateHandlers.MacToDateTime(prodosinfo.modificationDate);
                        }
                        else if(_unixHome.SequenceEqual(_header.homeFilesystem))
                        {
                            AppleDoubleUnixFileInfo unixinfo =
                                Marshal.ByteArrayToStructureBigEndian<AppleDoubleUnixFileInfo>(finfo);

                            _creationTime  = DateHandlers.UnixUnsignedToDateTime(unixinfo.creationDate);
                            _lastWriteTime = DateHandlers.UnixUnsignedToDateTime(unixinfo.modificationDate);
                        }
                        else if(_dosHome.SequenceEqual(_header.homeFilesystem))
                        {
                            AppleDoubleDOSFileInfo dosinfo =
                                Marshal.ByteArrayToStructureBigEndian<AppleDoubleDOSFileInfo>(finfo);

                            _lastWriteTime =
                                DateHandlers.DosToDateTime(dosinfo.modificationDate, dosinfo.modificationTime);
                        }

                        break;
                    case AppleDoubleEntryID.ResourceFork:
                        _rsrcFork = entry;

                        break;
                }

            _dataFork = new AppleDoubleEntry
            {
                id = (uint)AppleDoubleEntryID.DataFork
            };

            if(File.Exists(path))
            {
                var dataFs = new FileStream(path, FileMode.Open, FileAccess.Read);
                _dataFork.length = (uint)dataFs.Length;
                dataFs.Close();
            }

            fs.Close();
            _opened   = true;
            _basePath = path;
        }

        enum AppleDoubleEntryID : uint
        {
            Invalid     = 0, DataFork    = 1, ResourceFork    = 2,
            RealName    = 3, Comment     = 4, Icon            = 5,
            ColorIcon   = 6, FileInfo    = 7, FileDates       = 8,
            FinderInfo  = 9, MacFileInfo = 10, ProDOSFileInfo = 11,
            DOSFileInfo = 12, ShortName  = 13, AfpFileInfo    = 14,
            DirectoryID = 15
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleHeader
        {
            public readonly uint magic;
            public readonly uint version;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public readonly byte[] homeFilesystem;
            public readonly ushort entries;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleEntry
        {
            public          uint id;
            public readonly uint offset;
            public          uint length;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleFileDates
        {
            public readonly uint creationDate;
            public readonly uint modificationDate;
            public readonly uint backupDate;
            public readonly uint accessDate;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleMacFileInfo
        {
            public readonly uint creationDate;
            public readonly uint modificationDate;
            public readonly uint backupDate;
            public readonly uint accessDate;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleUnixFileInfo
        {
            public readonly uint creationDate;
            public readonly uint accessDate;
            public readonly uint modificationDate;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleDOSFileInfo
        {
            public readonly ushort modificationDate;
            public readonly ushort modificationTime;
            public readonly ushort attributes;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct AppleDoubleProDOSFileInfo
        {
            public readonly uint   creationDate;
            public readonly uint   modificationDate;
            public readonly uint   backupDate;
            public readonly ushort access;
            public readonly ushort fileType;
            public readonly uint   auxType;
        }
    }
}