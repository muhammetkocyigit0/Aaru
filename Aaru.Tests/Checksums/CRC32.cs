﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : CRC32.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : DiscImageChef unit testing.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2019 Natalia Portillo
// ****************************************************************************/

using System.IO;
using Aaru.Checksums;
using Aaru.CommonTypes.Interfaces;
using NUnit.Framework;

namespace Aaru.Tests.Checksums
{
    [TestFixture]
    public class Crc32
    {
        static readonly byte[] _expectedEmpty =
        {
            0xa7, 0x38, 0xea, 0x1c
        };
        static readonly byte[] _expectedRandom =
        {
            0x2b, 0x6e, 0x68, 0x54
        };

        [Test]
        public void Crc32EmptyData()
        {
            byte[] data = new byte[1048576];

            var fs = new FileStream(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "empty"), FileMode.Open,
                                    FileAccess.Read);

            fs.Read(data, 0, 1048576);
            fs.Close();
            fs.Dispose();
            Crc32Context.Data(data, out byte[] result);
            Assert.AreEqual(_expectedEmpty, result);
        }

        [Test]
        public void Crc32EmptyFile()
        {
            byte[] result = Crc32Context.File(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "empty"));
            Assert.AreEqual(_expectedEmpty, result);
        }

        [Test]
        public void Crc32EmptyInstance()
        {
            byte[] data = new byte[1048576];

            var fs = new FileStream(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "empty"), FileMode.Open,
                                    FileAccess.Read);

            fs.Read(data, 0, 1048576);
            fs.Close();
            fs.Dispose();
            IChecksum ctx = new Crc32Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            Assert.AreEqual(_expectedEmpty, result);
        }

        [Test]
        public void Crc32RandomData()
        {
            byte[] data = new byte[1048576];

            var fs = new FileStream(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "random"),
                                    FileMode.Open, FileAccess.Read);

            fs.Read(data, 0, 1048576);
            fs.Close();
            fs.Dispose();
            Crc32Context.Data(data, out byte[] result);
            Assert.AreEqual(_expectedRandom, result);
        }

        [Test]
        public void Crc32RandomFile()
        {
            byte[] result = Crc32Context.File(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "random"));
            Assert.AreEqual(_expectedRandom, result);
        }

        [Test]
        public void Crc32RandomInstance()
        {
            byte[] data = new byte[1048576];

            var fs = new FileStream(Path.Combine(Consts.TEST_FILES_ROOT, "Checksum test files", "random"),
                                    FileMode.Open, FileAccess.Read);

            fs.Read(data, 0, 1048576);
            fs.Close();
            fs.Dispose();
            IChecksum ctx = new Crc32Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            Assert.AreEqual(_expectedRandom, result);
        }
    }
}