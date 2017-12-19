﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : SysV.cs
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
// Copyright © 2011-2018 Natalia Portillo
// ****************************************************************************/

using System.IO;
using DiscImageChef.CommonTypes;
using DiscImageChef.Filesystems;
using DiscImageChef.Filters;
using DiscImageChef.ImagePlugins;
using NUnit.Framework;

namespace DiscImageChef.Tests.Filesystems
{
    [TestFixture]
    public class SysV
    {
        readonly string[] testfiles = {
            "amix.adf.lz",
            "att_unix_svr4v2.1_dsdd.img.lz", "att_unix_svr4v2.1_mf2dd.img.lz", "att_unix_svr4v2.1_mf2hd.img.lz",
            "scoopenserver_5.0.7hw_dmf.img.lz", "scoopenserver_5.0.7hw_dshd.img.lz", "scoopenserver_5.0.7hw_mf2dd.img.lz", "scoopenserver_5.0.7hw_mf2ed.img.lz",
            "scoopenserver_5.0.7hw_mf2hd.img.lz",
        };

        readonly MediaType[] mediatypes = {
            MediaType.CBM_AMIGA_35_DD,
            MediaType.DOS_525_DS_DD_9,MediaType.DOS_35_DS_DD_9,MediaType.DOS_35_HD,
            MediaType.DMF,MediaType.DOS_525_HD,MediaType.DOS_35_DS_DD_9,MediaType.DOS_35_ED,
            MediaType.DOS_35_HD,
        };

        readonly ulong[] sectors = {
            1760,
            720, 1440, 2880,
            3360, 2400, 1440, 5760,
            2880,
        };

        readonly uint[] sectorsize = {
            512,
            512, 512, 512,
            512, 512, 512, 512,
            512,
        };

        readonly long[] clusters = {
            880,
            360, 720, 1440,
            1680, 1200, 720, 2880,
            1440,
        };

        readonly int[] clustersize = {
            1024,
            1024, 1024, 1024,
            1024, 1024, 1024, 1024,
            1024,
        };

        readonly string[] volumename = {
            "",
            "", "", "",
            "", "", "", "",
            "",
        };

        readonly string[] volumeserial = {
            null,
            null, null, null,
            null, null, null, null,
            null,
        };

        readonly string[] type = {
            "SVR4 fs",
            "SVR4 fs", "SVR4 fs", "SVR4 fs",
            "SVR4 fs", "SVR4 fs", "SVR4 fs", "SVR4 fs",
            "SVR4 fs",
        };

        [Test]
        public void Test()
        {
            for(int i = 0; i < testfiles.Length; i++)
            {
                string location = Path.Combine(Consts.TestFilesRoot, "filesystems", "s5fs", testfiles[i]);
                Filter filter = new LZip();
                filter.Open(location);
                ImagePlugin image = new ZZZRawImage();
                Assert.AreEqual(true, image.OpenImage(filter), testfiles[i]);
                Assert.AreEqual(mediatypes[i], image.ImageInfo.mediaType, testfiles[i]);
                Assert.AreEqual(sectors[i], image.ImageInfo.sectors, testfiles[i]);
                Assert.AreEqual(sectorsize[i], image.ImageInfo.sectorSize, testfiles[i]);
                Filesystem fs = new DiscImageChef.Filesystems.SysVfs();
                Partition wholePart = new Partition
                {
                    Name = "Whole device",
                    Length = image.ImageInfo.sectors,
                    Size = image.ImageInfo.sectors * image.ImageInfo.sectorSize
                };
                Assert.AreEqual(true, fs.Identify(image, wholePart), testfiles[i]);
                fs.GetInformation(image, wholePart, out string information);
                Assert.AreEqual(clusters[i], fs.XmlFSType.Clusters, testfiles[i]);
                Assert.AreEqual(clustersize[i], fs.XmlFSType.ClusterSize, testfiles[i]);
                Assert.AreEqual(type[i], fs.XmlFSType.Type, testfiles[i]);
                Assert.AreEqual(volumename[i], fs.XmlFSType.VolumeName, testfiles[i]);
                Assert.AreEqual(volumeserial[i], fs.XmlFSType.VolumeSerial, testfiles[i]);
            }
        }
    }
}
