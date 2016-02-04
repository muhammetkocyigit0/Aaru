﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : DumpMedia.cs
// Version        : 1.0
// Author(s)      : Natalia Portillo
//
// Component      : Component
//
// Revision       : $Revision$
// Last change by : $Author$
// Date           : $Date$
//
// --[ Description ] ----------------------------------------------------------
//
// Description
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
// Copyright (C) 2011-2015 Claunia.com
// ****************************************************************************/
// //$Id$
using System;
using DiscImageChef.Console;
using System.IO;
using DiscImageChef.Devices;
using System.Text;
using System.Collections.Generic;
using Schemas;
using DiscImageChef.CommonTypes;

namespace DiscImageChef.Commands
{
    public static class DumpMedia
    {
        static bool aborted;
        static FileStream mhddFs;
        static FileStream dataFs;
        // TODO: Implement dump map

        public static void doDumpMedia(DumpMediaSubOptions options)
        {
            DicConsole.DebugWriteLine("Dump-Media command", "--debug={0}", options.Debug);
            DicConsole.DebugWriteLine("Dump-Media command", "--verbose={0}", options.Verbose);
            DicConsole.DebugWriteLine("Dump-Media command", "--device={0}", options.DevicePath);
            DicConsole.DebugWriteLine("Dump-Media command", "--output-prefix={0}", options.OutputPrefix);
            DicConsole.DebugWriteLine("Dump-Media command", "--raw={0}", options.Raw);
            DicConsole.DebugWriteLine("Dump-Media command", "--stop-on-error={0}", options.StopOnError);
            DicConsole.DebugWriteLine("Dump-Media command", "--force={0}", options.Force);
            DicConsole.DebugWriteLine("Dump-Media command", "--retry-passes={0}", options.RetryPasses);
            DicConsole.DebugWriteLine("Dump-Media command", "--persistent={0}", options.Persistent);

            if (!System.IO.File.Exists(options.DevicePath))
            {
                DicConsole.ErrorWriteLine("Specified device does not exist.");
                return;
            }

            if (options.DevicePath.Length == 2 && options.DevicePath[1] == ':' &&
                options.DevicePath[0] != '/' && Char.IsLetter(options.DevicePath[0]))
            {
                options.DevicePath = "\\\\.\\" + Char.ToUpper(options.DevicePath[0]) + ':';
            }

            mhddFs = null;

            Device dev = new Device(options.DevicePath);

            if (dev.Error)
            {
                DicConsole.ErrorWriteLine("Error {0} opening device.", dev.LastError);
                return;
            }

            Core.Statistics.AddDevice(dev);

            switch (dev.Type)
            {
                case DeviceType.ATA:
                    doATAMediaScan(options, dev);
                    break;
                case DeviceType.MMC:
                case DeviceType.SecureDigital:
                    doSDMediaScan(options, dev);
                    break;
                case DeviceType.NVMe:
                    doNVMeMediaScan(options, dev);
                    break;
                case DeviceType.ATAPI:
                case DeviceType.SCSI:
                    doSCSIMediaScan(options, dev);
                    break;
                default:
                    throw new NotSupportedException("Unknown device type.");
            }

            Core.Statistics.AddCommand("dump-media");
        }

        static void doATAMediaScan(DumpMediaSubOptions options, Device dev)
        {
            throw new NotImplementedException("ATA devices not yet supported.");
        }

        static void doNVMeMediaScan(DumpMediaSubOptions options, Device dev)
        {
            throw new NotImplementedException("NVMe devices not yet supported.");
        }

        static void doSDMediaScan(DumpMediaSubOptions options, Device dev)
        {
            throw new NotImplementedException("MMC/SD devices not yet supported.");
        }

        static void doSCSIMediaScan(DumpMediaSubOptions options, Device dev)
        {
            byte[] cmdBuf = null;
            byte[] senseBuf = null;
            bool sense = false;
            double duration;
            ulong blocks = 0;
            uint blockSize = 0;
            byte[] tmpBuf;
            MediaType dskType = MediaType.Unknown;
            bool opticalDisc = false;

            if (dev.IsRemovable)
            {
                sense = dev.ScsiTestUnitReady(out senseBuf, dev.Timeout, out duration);
                if (sense)
                {
                    Decoders.SCSI.FixedSense? decSense = Decoders.SCSI.Sense.DecodeFixed(senseBuf);
                    if (decSense.HasValue)
                    {
                        if (decSense.Value.ASC == 0x3A)
                        {
                            int leftRetries = 5;
                            while (leftRetries > 0)
                            {
                                DicConsole.WriteLine("\rWaiting for drive to become ready");
                                System.Threading.Thread.Sleep(2000);
                                sense = dev.ScsiTestUnitReady(out senseBuf, dev.Timeout, out duration);
                                if (!sense)
                                    break;

                                leftRetries--;
                            }

                            if (sense)
                            {
                                DicConsole.ErrorWriteLine("Please insert media in drive");
                                return;
                            }
                        }
                        else if (decSense.Value.ASC == 0x04 && decSense.Value.ASCQ == 0x01)
                        {
                            int leftRetries = 10;
                            while (leftRetries > 0)
                            {
                                DicConsole.WriteLine("\rWaiting for drive to become ready");
                                System.Threading.Thread.Sleep(2000);
                                sense = dev.ScsiTestUnitReady(out senseBuf, dev.Timeout, out duration);
                                if (!sense)
                                    break;

                                leftRetries--;
                            }

                            if (sense)
                            {
                                DicConsole.ErrorWriteLine("Error testing unit was ready:\n{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                                return;
                            }
                        }
                        /*else if (decSense.Value.ASC == 0x29 && decSense.Value.ASCQ == 0x00)
                        {
                            if (!deviceReset)
                            {
                                deviceReset = true;
                                DicConsole.ErrorWriteLine("Device did reset, retrying...");
                                goto retryTestReady;
                            }

                            DicConsole.ErrorWriteLine("Error testing unit was ready:\n{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                            return;
                        }*/
                        else
                        {
                            DicConsole.ErrorWriteLine("Error testing unit was ready:\n{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                            return;
                        }
                    }
                    else
                    {
                        DicConsole.ErrorWriteLine("Unknown testing unit was ready.");
                        return;
                    }
                }
            }

            if (dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.DirectAccess ||
                dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.MultiMediaDevice ||
                dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.OCRWDevice ||
                dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.OpticalDevice ||
                dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.SimplifiedDevice ||
                dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.WriteOnceDevice)
            {
                sense = dev.ReadCapacity(out cmdBuf, out senseBuf, dev.Timeout, out duration);
                if (!sense)
                {
                    blocks = (ulong)((cmdBuf[0] << 24) + (cmdBuf[1] << 16) + (cmdBuf[2] << 8) + (cmdBuf[3]));
                    blockSize = (uint)((cmdBuf[5] << 24) + (cmdBuf[5] << 16) + (cmdBuf[6] << 8) + (cmdBuf[7]));
                }

                if (sense || blocks == 0xFFFFFFFF)
                {
                    sense = dev.ReadCapacity16(out cmdBuf, out senseBuf, dev.Timeout, out duration);

                    if (sense && blocks == 0)
                    {
                        // Not all MMC devices support READ CAPACITY, as they have READ TOC
                        if (dev.SCSIType != DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.MultiMediaDevice)
                        {
                            DicConsole.ErrorWriteLine("Unable to get media capacity");
                            DicConsole.ErrorWriteLine("{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                        }
                    }

                    if (!sense)
                    {
                        byte[] temp = new byte[8];

                        Array.Copy(cmdBuf, 0, temp, 0, 8);
                        Array.Reverse(temp);
                        blocks = BitConverter.ToUInt64(temp, 0);
                        blockSize = (uint)((cmdBuf[5] << 24) + (cmdBuf[5] << 16) + (cmdBuf[6] << 8) + (cmdBuf[7]));
                    }
                }

                if (blocks != 0 && blockSize != 0)
                {
                    blocks++;
                    DicConsole.WriteLine("Media has {0} blocks of {1} bytes/each. (for a total of {2} bytes)",
                        blocks, blockSize, blocks * (ulong)blockSize);
                }
            }

            if (dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.SequentialAccess)
            {
                throw new NotImplementedException();
            }            

            if (blocks == 0)
            {
                DicConsole.ErrorWriteLine("Unable to read medium or empty medium present...");
                return;
            }

            bool compactDisc = true;
            Decoders.CD.FullTOC.CDFullTOC? toc = null;
            byte scsiMediumType = 0;
            byte scsiDensityCode = 0;
            bool containsFloppyPage = false;

            CICMMetadataType sidecar = new CICMMetadataType();

            #region MultiMediaDevice
            if (dev.SCSIType == DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.MultiMediaDevice)
            {
                sidecar.OpticalDisc = new OpticalDiscType[1];
                sidecar.OpticalDisc[0] = new OpticalDiscType();
                opticalDisc = true;

                sense = dev.GetConfiguration(out cmdBuf, out senseBuf, 0, MmcGetConfigurationRt.Current, dev.Timeout, out duration);
                if (!sense)
                {
                    Decoders.SCSI.MMC.Features.SeparatedFeatures ftr = Decoders.SCSI.MMC.Features.Separate(cmdBuf);

                    switch (ftr.CurrentProfile)
                    {
                        case 0x0001:
                            dskType = MediaType.GENERIC_HDD;
                            goto default;
                        case 0x0005:
                            dskType = MediaType.CDMO;
                            break;
                        case 0x0008:
                            dskType = MediaType.CD;
                            break;
                        case 0x0009:
                            dskType = MediaType.CDR;
                            break;
                        case 0x000A:
                            dskType = MediaType.CDRW;
                            break;
                        case 0x0010:
                            dskType = MediaType.DVDROM;
                            goto default;
                        case 0x0011:
                            dskType = MediaType.DVDR;
                            goto default;
                        case 0x0012:
                            dskType = MediaType.DVDRAM;
                            goto default;
                        case 0x0013:
                        case 0x0014:
                            dskType = MediaType.DVDRW;
                            goto default;
                        case 0x0015:
                        case 0x0016:
                            dskType = MediaType.DVDRDL;
                            goto default;
                        case 0x0017:
                            dskType = MediaType.DVDRWDL;
                            goto default;
                        case 0x0018:
                            dskType = MediaType.DVDDownload;
                            goto default;
                        case 0x001A:
                            dskType = MediaType.DVDPRW;
                            goto default;
                        case 0x001B:
                            dskType = MediaType.DVDPR;
                            goto default;
                        case 0x0020:
                            dskType = MediaType.DDCD;
                            goto default;
                        case 0x0021:
                            dskType = MediaType.DDCDR;
                            goto default;
                        case 0x0022:
                            dskType = MediaType.DDCDRW;
                            goto default;
                        case 0x002A:
                            dskType = MediaType.DVDPRWDL;
                            goto default;
                        case 0x002B:
                            dskType = MediaType.DVDPRDL;
                            goto default;
                        case 0x0040:
                            dskType = MediaType.BDROM;
                            goto default;
                        case 0x0041:
                        case 0x0042:
                            dskType = MediaType.BDR;
                            goto default;
                        case 0x0043:
                            dskType = MediaType.BDRE;
                            goto default;
                        case 0x0050:
                            dskType = MediaType.HDDVDROM;
                            goto default;
                        case 0x0051:
                            dskType = MediaType.HDDVDR;
                            goto default;
                        case 0x0052:
                            dskType = MediaType.HDDVDRAM;
                            goto default;
                        case 0x0053:
                            dskType = MediaType.HDDVDRW;
                            goto default;
                        case 0x0058:
                            dskType = MediaType.HDDVDRDL;
                            goto default;
                        case 0x005A:
                            dskType = MediaType.HDDVDRWDL;
                            goto default;
                        default:
                            compactDisc = false;
                            break;
                    }
                }

                #region CompactDisc
                if (compactDisc)
                {
                    // We discarded all discs that falsify a TOC before requesting a real TOC
                    // No TOC, no CD (or an empty one)
                    bool tocSense = dev.ReadRawToc(out cmdBuf, out senseBuf, 1, dev.Timeout, out duration);
                    if (!tocSense)
                    {
                        toc = Decoders.CD.FullTOC.Decode(cmdBuf);
                        if (toc.HasValue)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 2];
                            Array.Copy(cmdBuf, 2, tmpBuf, 0, cmdBuf.Length - 2);
                            sidecar.OpticalDisc[0].TOC = new DumpType();
                            sidecar.OpticalDisc[0].TOC.Image = options.OutputPrefix + ".toc.bin";
                            sidecar.OpticalDisc[0].TOC.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].TOC.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].TOC.Image, tmpBuf);

                            // ATIP exists on blank CDs
                            sense = dev.ReadAtip(out cmdBuf, out senseBuf, dev.Timeout, out duration);
                            if (!sense)
                            {
                                Decoders.CD.ATIP.CDATIP? atip = Decoders.CD.ATIP.Decode(cmdBuf);
                                if (atip.HasValue)
                                {
                                    if (blocks == 0)
                                    {
                                        DicConsole.ErrorWriteLine("Cannot dump blank media.");
                                        return;
                                    }

                                    // Only CD-R and CD-RW have ATIP
                                    dskType = atip.Value.DiscType ? MediaType.CDRW : MediaType.CDR;

                                    tmpBuf = new byte[cmdBuf.Length - 4];
                                    Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                    sidecar.OpticalDisc[0].ATIP = new DumpType();
                                    sidecar.OpticalDisc[0].ATIP.Image = options.OutputPrefix + ".atip.bin";
                                    sidecar.OpticalDisc[0].ATIP.Size = tmpBuf.Length;
                                    sidecar.OpticalDisc[0].ATIP.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                    writeToFile(sidecar.OpticalDisc[0].TOC.Image, tmpBuf);
                                }
                            }

                            sense = dev.ReadDiscInformation(out cmdBuf, out senseBuf, MmcDiscInformationDataTypes.DiscInformation, dev.Timeout, out duration);
                            if (!sense)
                            {
                                Decoders.SCSI.MMC.DiscInformation.StandardDiscInformation? discInfo = Decoders.SCSI.MMC.DiscInformation.Decode000b(cmdBuf);
                                if (discInfo.HasValue)
                                {
                                    // If it is a read-only CD, check CD type if available
                                    if (dskType == MediaType.CD)
                                    {
                                        switch (discInfo.Value.DiscType)
                                        {
                                            case 0x10:
                                                dskType = MediaType.CDI;
                                                break;
                                            case 0x20:
                                                dskType = MediaType.CDROMXA;
                                                break;
                                        }
                                    }
                                }
                            }

                            int sessions = 1;
                            int firstTrackLastSession = 0;

                            sense = dev.ReadSessionInfo(out cmdBuf, out senseBuf, dev.Timeout, out duration);
                            if (!sense)
                            {
                                Decoders.CD.Session.CDSessionInfo? session = Decoders.CD.Session.Decode(cmdBuf);
                                if (session.HasValue)
                                {
                                    sessions = session.Value.LastCompleteSession;
                                    firstTrackLastSession = session.Value.TrackDescriptors[0].TrackNumber;
                                }
                            }

                            if (dskType == MediaType.CD)
                            {
                                bool hasDataTrack = false;
                                bool hasAudioTrack = false;
                                bool allFirstSessionTracksAreAudio = true;
                                bool hasVideoTrack = false;

                                if (toc.HasValue)
                                {
                                    foreach (Decoders.CD.FullTOC.TrackDataDescriptor track in toc.Value.TrackDescriptors)
                                    {
                                        if (track.TNO == 1 &&
                                            ((Decoders.CD.TOC_CONTROL)(track.CONTROL & 0x0D) == Decoders.CD.TOC_CONTROL.DataTrack ||
                                            (Decoders.CD.TOC_CONTROL)(track.CONTROL & 0x0D) == Decoders.CD.TOC_CONTROL.DataTrackIncremental))
                                        {
                                            allFirstSessionTracksAreAudio &= firstTrackLastSession != 1;
                                        }

                                        if ((Decoders.CD.TOC_CONTROL)(track.CONTROL & 0x0D) == Decoders.CD.TOC_CONTROL.DataTrack ||
                                            (Decoders.CD.TOC_CONTROL)(track.CONTROL & 0x0D) == Decoders.CD.TOC_CONTROL.DataTrackIncremental)
                                        {
                                            hasDataTrack = true;
                                            allFirstSessionTracksAreAudio &= track.TNO >= firstTrackLastSession;
                                        }
                                        else
                                            hasAudioTrack = true;

                                        hasVideoTrack |= track.ADR == 4;
                                    }
                                }

                                if (hasDataTrack && hasAudioTrack && allFirstSessionTracksAreAudio && sessions == 2)
                                    dskType = MediaType.CDPLUS;
                                if (!hasDataTrack && hasAudioTrack && sessions == 1)
                                    dskType = MediaType.CDDA;
                                if (hasDataTrack && !hasAudioTrack && sessions == 1)
                                    dskType = MediaType.CDROM;
                                if (hasVideoTrack && !hasDataTrack && sessions == 1)
                                    dskType = MediaType.CDV;
                            }

                            sense = dev.ReadPma(out cmdBuf, out senseBuf, dev.Timeout, out duration);
                            if (!sense)
                            {
                                if (Decoders.CD.PMA.Decode(cmdBuf).HasValue)
                                {
                                    tmpBuf = new byte[cmdBuf.Length - 4];
                                    Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                    sidecar.OpticalDisc[0].PMA = new DumpType();
                                    sidecar.OpticalDisc[0].PMA.Image = options.OutputPrefix + ".pma.bin";
                                    sidecar.OpticalDisc[0].PMA.Size = tmpBuf.Length;
                                    sidecar.OpticalDisc[0].PMA.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                    writeToFile(sidecar.OpticalDisc[0].PMA.Image, tmpBuf);
                                }
                            }

                            sense = dev.ReadCdText(out cmdBuf, out senseBuf, dev.Timeout, out duration);
                            if (!sense)
                            {
                                if (Decoders.CD.CDTextOnLeadIn.Decode(cmdBuf).HasValue)
                                {
                                    tmpBuf = new byte[cmdBuf.Length - 4];
                                    Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                    sidecar.OpticalDisc[0].LeadInCdText = new DumpType();
                                    sidecar.OpticalDisc[0].LeadInCdText.Image = options.OutputPrefix + ".cdtext.bin";
                                    sidecar.OpticalDisc[0].LeadInCdText.Size = tmpBuf.Length;
                                    sidecar.OpticalDisc[0].LeadInCdText.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                    writeToFile(sidecar.OpticalDisc[0].LeadInCdText.Image, tmpBuf);
                                }
                            }
                        }
                    }
                }
                #endregion CompactDisc
                else
                {
                    #region Nintendo
                    if (dskType == MediaType.Unknown && blocks > 0)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.PhysicalInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            Decoders.DVD.PFI.PhysicalFormatInformation? nintendoPfi = Decoders.DVD.PFI.Decode(cmdBuf);
                            if (nintendoPfi != null)
                            {
                                if (nintendoPfi.Value.DiskCategory == DiscImageChef.Decoders.DVD.DiskCategory.Nintendo &&
                                    nintendoPfi.Value.PartVersion == 15)
                                {
                                    throw new NotImplementedException("Dumping Nintendo GameCube or Wii discs is not yet implemented.");
                                }
                            }
                        }
                    }
                    #endregion Nintendo

                    #region All DVD and HD DVD types
                    if (dskType == MediaType.DVDDownload || dskType == MediaType.DVDPR ||
                        dskType == MediaType.DVDPRDL || dskType == MediaType.DVDPRW ||
                        dskType == MediaType.DVDPRWDL || dskType == MediaType.DVDR ||
                        dskType == MediaType.DVDRAM || dskType == MediaType.DVDRDL ||
                        dskType == MediaType.DVDROM || dskType == MediaType.DVDRW ||
                        dskType == MediaType.DVDRWDL || dskType == MediaType.HDDVDR ||
                        dskType == MediaType.HDDVDRAM || dskType == MediaType.HDDVDRDL ||
                        dskType == MediaType.HDDVDROM || dskType == MediaType.HDDVDRW ||
                        dskType == MediaType.HDDVDRWDL)
                    {

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.PhysicalInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.DVD.PFI.Decode(cmdBuf).HasValue)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].PFI = new DumpType();
                                sidecar.OpticalDisc[0].PFI.Image = options.OutputPrefix + ".pfi.bin";
                                sidecar.OpticalDisc[0].PFI.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].PFI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].PFI.Image, tmpBuf);

                                Decoders.DVD.PFI.PhysicalFormatInformation decPfi = Decoders.DVD.PFI.Decode(cmdBuf).Value;
                                    DicConsole.WriteLine("PFI:\n{0}", Decoders.DVD.PFI.Prettify(decPfi));

                                    // False book types
                                    if(dskType == MediaType.DVDROM)
                                    {
                                        switch(decPfi.DiskCategory)
                                        {
                                            case Decoders.DVD.DiskCategory.DVDPR:
                                                dskType = MediaType.DVDPR;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDPRDL:
                                                dskType = MediaType.DVDPRDL;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDPRW:
                                                dskType = MediaType.DVDPRW;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDPRWDL:
                                                dskType = MediaType.DVDPRWDL;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDR:
                                                if(decPfi.PartVersion == 6)
                                                    dskType = MediaType.DVDRDL;
                                                else
                                                    dskType = MediaType.DVDR;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDRAM:
                                                dskType = MediaType.DVDRAM;
                                                break;
                                            default:
                                                dskType = MediaType.DVDROM;
                                                break;
                                            case Decoders.DVD.DiskCategory.DVDRW:
                                                if(decPfi.PartVersion == 3)
                                                    dskType = MediaType.DVDRWDL;
                                                else
                                                    dskType = MediaType.DVDRW;
                                                break;
                                            case Decoders.DVD.DiskCategory.HDDVDR:
                                                dskType = MediaType.HDDVDR;
                                                break;
                                            case Decoders.DVD.DiskCategory.HDDVDRAM:
                                                dskType = MediaType.HDDVDRAM;
                                                break;
                                            case Decoders.DVD.DiskCategory.HDDVDROM:
                                                dskType = MediaType.HDDVDROM;
                                                break;
                                            case Decoders.DVD.DiskCategory.HDDVDRW:
                                                dskType = MediaType.HDDVDRW;
                                                break;
                                            case Decoders.DVD.DiskCategory.Nintendo:
                                                if(decPfi.DiscSize == DiscImageChef.Decoders.DVD.DVDSize.Eighty)
                                                    dskType = MediaType.GOD;
                                                else    
                                                    dskType = MediaType.WOD;
                                                break;
                                            case Decoders.DVD.DiskCategory.UMD:
                                                dskType = MediaType.UMD;
                                                break;
                                        }
                                    }
                            }
                        }
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DiscManufacturingInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.Xbox.DMI.IsXbox(cmdBuf) || Decoders.Xbox.DMI.IsXbox360(cmdBuf))
                                throw new NotImplementedException("Dumping Xbox discs is not yet implemented.");
                            
                            if (cmdBuf.Length == 2052)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].DMI = new DumpType();
                                sidecar.OpticalDisc[0].DMI.Image = options.OutputPrefix + ".dmi.bin";
                                sidecar.OpticalDisc[0].DMI.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].DMI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].DMI.Image, tmpBuf);
                            }
                        }
                    }
                    #endregion All DVD and HD DVD types

                    #region DVD-ROM
                    if (dskType == MediaType.DVDDownload || dskType == MediaType.DVDROM)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.CopyrightInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.DVD.CSS_CPRM.DecodeLeadInCopyright(cmdBuf).HasValue)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].CMI = new DumpType();
                                sidecar.OpticalDisc[0].CMI.Image = options.OutputPrefix + ".cmi.bin";
                                sidecar.OpticalDisc[0].CMI.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].CMI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].CMI.Image, tmpBuf);

                                Decoders.DVD.CSS_CPRM.LeadInCopyright cpy = Decoders.DVD.CSS_CPRM.DecodeLeadInCopyright(cmdBuf).Value;
                                if(cpy.CopyrightType != DiscImageChef.Decoders.DVD.CopyrightType.NoProtection)
                                    sidecar.OpticalDisc[0].CopyProtection = cpy.CopyrightType.ToString();
                            }
                        }
                    }
                    #endregion DVD-ROM

                    #region DVD-ROM and HD DVD-ROM
                    if (dskType == MediaType.DVDDownload || dskType == MediaType.DVDROM ||
                        dskType == MediaType.HDDVDROM)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.BurstCuttingArea, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].BCA = new DumpType();
                            sidecar.OpticalDisc[0].BCA.Image = options.OutputPrefix + ".bca.bin";
                            sidecar.OpticalDisc[0].BCA.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].BCA.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].BCA.Image, tmpBuf);
                        }
                    }
                    #endregion DVD-ROM and HD DVD-ROM

                    #region DVD-RAM and HD DVD-RAM
                    if (dskType == MediaType.DVDRAM || dskType == MediaType.HDDVDRAM)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DVDRAM_DDS, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.DVD.DDS.Decode(cmdBuf).HasValue)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].DDS = new DumpType();
                                sidecar.OpticalDisc[0].DDS.Image = options.OutputPrefix + ".dds.bin";
                                sidecar.OpticalDisc[0].DDS.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].DDS.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].DDS.Image, tmpBuf);
                            }
                        }

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DVDRAM_SpareAreaInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.DVD.Spare.Decode(cmdBuf).HasValue)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].SAI = new DumpType();
                                sidecar.OpticalDisc[0].SAI.Image = options.OutputPrefix + ".sai.bin";
                                sidecar.OpticalDisc[0].SAI.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].SAI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].SAI.Image, tmpBuf);
                            }
                        }
                    }
                    #endregion DVD-RAM and HD DVD-RAM

                    #region DVD-R and DVD-RW
                    if (dskType == MediaType.DVDR || dskType == MediaType.DVDRW)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.PreRecordedInfo, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].PRI = new DumpType();
                            sidecar.OpticalDisc[0].PRI.Image = options.OutputPrefix + ".pri.bin";
                            sidecar.OpticalDisc[0].PRI.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].PRI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].SAI.Image, tmpBuf);
                        }
                    }
                    #endregion DVD-R and DVD-RW

                    #region DVD-R, DVD-RW and HD DVD-R
                    if (dskType == MediaType.DVDR || dskType == MediaType.DVDRW || dskType == MediaType.HDDVDR)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DVDR_MediaIdentifier, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].MediaID = new DumpType();
                            sidecar.OpticalDisc[0].MediaID.Image = options.OutputPrefix + ".mid.bin";
                            sidecar.OpticalDisc[0].MediaID.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].MediaID.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].MediaID.Image, tmpBuf);
                        }

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DVDR_PhysicalInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].PFIR = new DumpType();
                            sidecar.OpticalDisc[0].PFIR.Image = options.OutputPrefix + ".pfir.bin";
                            sidecar.OpticalDisc[0].PFIR.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].PFIR.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].PFIR.Image, tmpBuf);
                        }
                    }
                    #endregion DVD-R, DVD-RW and HD DVD-R

                    #region All DVD+
                    if (dskType == MediaType.DVDPR || dskType == MediaType.DVDPRDL ||
                        dskType == MediaType.DVDPRW || dskType == MediaType.DVDPRWDL)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.ADIP, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].ADIP = new DumpType();
                            sidecar.OpticalDisc[0].ADIP.Image = options.OutputPrefix + ".adip.bin";
                            sidecar.OpticalDisc[0].ADIP.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].ADIP.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].ADIP.Image, tmpBuf);
                        }

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.DCB, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].DCB = new DumpType();
                            sidecar.OpticalDisc[0].DCB.Image = options.OutputPrefix + ".dcb.bin";
                            sidecar.OpticalDisc[0].DCB.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].DCB.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].DCB.Image, tmpBuf);
                        }
                    }
                    #endregion All DVD+

                    #region HD DVD-ROM
                    if (dskType == MediaType.HDDVDROM)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.DVD, 0, 0, MmcDiscStructureFormat.HDDVD_CopyrightInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].CMI = new DumpType();
                            sidecar.OpticalDisc[0].CMI.Image = options.OutputPrefix + ".cmi.bin";
                            sidecar.OpticalDisc[0].CMI.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].CMI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].CMI.Image, tmpBuf);
                        }
                    }
                    #endregion HD DVD-ROM

                    #region All Blu-ray
                    if (dskType == MediaType.BDR || dskType == MediaType.BDRE || dskType == MediaType.BDROM ||
                        dskType == MediaType.BDRXL || dskType == MediaType.BDREXL)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.BD, 0, 0, MmcDiscStructureFormat.DiscInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            if (Decoders.Bluray.DI.Decode(cmdBuf).HasValue)
                            {
                                tmpBuf = new byte[cmdBuf.Length - 4];
                                Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                                sidecar.OpticalDisc[0].DI = new DumpType();
                                sidecar.OpticalDisc[0].DI.Image = options.OutputPrefix + ".di.bin";
                                sidecar.OpticalDisc[0].DI.Size = tmpBuf.Length;
                                sidecar.OpticalDisc[0].DI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                                writeToFile(sidecar.OpticalDisc[0].DI.Image, tmpBuf);
                            }
                        }

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.BD, 0, 0, MmcDiscStructureFormat.PAC, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].PAC = new DumpType();
                            sidecar.OpticalDisc[0].PAC.Image = options.OutputPrefix + ".pac.bin";
                            sidecar.OpticalDisc[0].PAC.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].PAC.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].PAC.Image, tmpBuf);
                        }
                    }
                    #endregion All Blu-ray


                    #region BD-ROM only
                    if (dskType == MediaType.BDROM)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.BD, 0, 0, MmcDiscStructureFormat.BD_BurstCuttingArea, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].BCA = new DumpType();
                            sidecar.OpticalDisc[0].BCA.Image = options.OutputPrefix + ".bca.bin";
                            sidecar.OpticalDisc[0].BCA.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].BCA.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].BCA.Image, tmpBuf);
                        }
                    }
                    #endregion BD-ROM only

                    #region Writable Blu-ray only
                    if (dskType == MediaType.BDR || dskType == MediaType.BDRE ||
                        dskType == MediaType.BDRXL || dskType == MediaType.BDREXL)
                    {
                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.BD, 0, 0, MmcDiscStructureFormat.BD_DDS, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].DDS = new DumpType();
                            sidecar.OpticalDisc[0].DDS.Image = options.OutputPrefix + ".dds.bin";
                            sidecar.OpticalDisc[0].DDS.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].DDS.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].DDS.Image, tmpBuf);
                        }

                        sense = dev.ReadDiscStructure(out cmdBuf, out senseBuf, MmcDiscStructureMediaType.BD, 0, 0, MmcDiscStructureFormat.BD_SpareAreaInformation, 0, dev.Timeout, out duration);
                        if (!sense)
                        {
                            tmpBuf = new byte[cmdBuf.Length - 4];
                            Array.Copy(cmdBuf, 4, tmpBuf, 0, cmdBuf.Length - 4);
                            sidecar.OpticalDisc[0].SAI = new DumpType();
                            sidecar.OpticalDisc[0].SAI.Image = options.OutputPrefix + ".sai.bin";
                            sidecar.OpticalDisc[0].SAI.Size = tmpBuf.Length;
                            sidecar.OpticalDisc[0].SAI.Checksums = Core.Checksum.GetChecksums(tmpBuf).ToArray();
                            writeToFile(sidecar.OpticalDisc[0].SAI.Image, tmpBuf);
                        }
                    }
                    #endregion Writable Blu-ray only
                }
            }
            #endregion MultiMediaDevice
            else
            {
                compactDisc = false;
                sidecar.BlockMedia = new BlockMediaType[1];
                sidecar.BlockMedia[0] = new BlockMediaType();

                // All USB flash drives report as removable, even if the media is not removable
                if (!dev.IsRemovable || dev.IsUSB)
                {
                    if (dev.IsUSB)
                    {
                        sidecar.BlockMedia[0].USB = new USBType();
                        sidecar.BlockMedia[0].USB.ProductID = dev.USBProductID;
                        sidecar.BlockMedia[0].USB.VendorID = dev.USBVendorID;
                        sidecar.BlockMedia[0].USB.Descriptors = new DumpType();
                        sidecar.BlockMedia[0].USB.Descriptors.Image = options.OutputPrefix + ".usbdescriptors.bin";
                        sidecar.BlockMedia[0].USB.Descriptors.Size = dev.USBDescriptors.Length;
                        sidecar.BlockMedia[0].USB.Descriptors.Checksums = Core.Checksum.GetChecksums(dev.USBDescriptors).ToArray();
                        writeToFile(sidecar.BlockMedia[0].USB.Descriptors.Image, dev.USBDescriptors);
                    }

                    if (dev.Type == DeviceType.ATAPI)
                    {
                        DiscImageChef.Decoders.ATA.AtaErrorRegistersCHS errorRegs;
                        sense = dev.AtapiIdentify(out cmdBuf, out errorRegs);
                        if (!sense)
                        {
                            sidecar.BlockMedia[0].ATA = new ATAType();
                            sidecar.BlockMedia[0].ATA.Identify = new DumpType();
                            sidecar.BlockMedia[0].ATA.Identify.Image = options.OutputPrefix + ".identify.bin";
                            sidecar.BlockMedia[0].ATA.Identify.Size = cmdBuf.Length;
                            sidecar.BlockMedia[0].ATA.Identify.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                            writeToFile(sidecar.BlockMedia[0].ATA.Identify.Image, cmdBuf);
                        }
                    }

                    sense = dev.ScsiInquiry(out cmdBuf, out senseBuf);
                    if (!sense)
                    {
                        sidecar.BlockMedia[0].SCSI = new SCSIType();
                        sidecar.BlockMedia[0].SCSI.Inquiry = new DumpType();
                        sidecar.BlockMedia[0].SCSI.Inquiry.Image = options.OutputPrefix + ".inquiry.bin";
                        sidecar.BlockMedia[0].SCSI.Inquiry.Size = cmdBuf.Length;
                        sidecar.BlockMedia[0].SCSI.Inquiry.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                        writeToFile(sidecar.BlockMedia[0].SCSI.Inquiry.Image, cmdBuf);

                        sense = dev.ScsiInquiry(out cmdBuf, out senseBuf, 0x00);
                        if (!sense)
                        {
                            byte[] pages = Decoders.SCSI.EVPD.DecodePage00(cmdBuf);

                            if (pages != null)
                            {
                                List<EVPDType> evpds = new List<EVPDType>();
                                foreach (byte page in pages)
                                {
                                    sense = dev.ScsiInquiry(out cmdBuf, out senseBuf, page);
                                    if (!sense)
                                    {
                                        EVPDType evpd = new EVPDType();
                                        evpd.Image = String.Format("{0}.evpd_{1:X2}h.bin", options.OutputPrefix, page);
                                        evpd.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                                        evpd.Size = cmdBuf.Length;
                                        evpd.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                                        writeToFile(evpd.Image, cmdBuf);
                                        evpds.Add(evpd);
                                    }
                                }

                                if (evpds.Count > 0)
                                    sidecar.BlockMedia[0].SCSI.EVPD = evpds.ToArray();
                            }
                        }

                        sense = dev.ModeSense10(out cmdBuf, out senseBuf, false, true, ScsiModeSensePageControl.Current, 0x3F, 0xFF, 5, out duration);
                        if (!sense || dev.Error)
                        {
                            sense = dev.ModeSense10(out cmdBuf, out senseBuf, false, true, ScsiModeSensePageControl.Current, 0x3F, 0x00, 5, out duration);
                        }

                        Decoders.SCSI.Modes.DecodedMode? decMode = null;

                        if (!sense && !dev.Error)
                        {
                            if (Decoders.SCSI.Modes.DecodeMode10(cmdBuf, dev.SCSIType).HasValue)
                            {
                                decMode = Decoders.SCSI.Modes.DecodeMode10(cmdBuf, dev.SCSIType);
                                sidecar.BlockMedia[0].SCSI.ModeSense10 = new DumpType();
                                sidecar.BlockMedia[0].SCSI.ModeSense10.Image = options.OutputPrefix + ".modesense10.bin";
                                sidecar.BlockMedia[0].SCSI.ModeSense10.Size = cmdBuf.Length;
                                sidecar.BlockMedia[0].SCSI.ModeSense10.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                                writeToFile(sidecar.BlockMedia[0].SCSI.ModeSense10.Image, cmdBuf);
                            }
                        }

                        sense = dev.ModeSense6(out cmdBuf, out senseBuf, false, ScsiModeSensePageControl.Current, 0x3F, 0x00, 5, out duration);
                        if (sense || dev.Error)
                            sense = dev.ModeSense6(out cmdBuf, out senseBuf, false, ScsiModeSensePageControl.Current, 0x3F, 0x00, 5, out duration);
                        if (sense || dev.Error)
                            sense = dev.ModeSense(out cmdBuf, out senseBuf, 5, out duration);

                        if (!sense && !dev.Error)
                        {
                            if (Decoders.SCSI.Modes.DecodeMode6(cmdBuf, dev.SCSIType).HasValue)
                            {
                                decMode = Decoders.SCSI.Modes.DecodeMode10(cmdBuf, dev.SCSIType);
                                sidecar.BlockMedia[0].SCSI.ModeSense = new DumpType();
                                sidecar.BlockMedia[0].SCSI.ModeSense.Image = options.OutputPrefix + ".modesense.bin";
                                sidecar.BlockMedia[0].SCSI.ModeSense.Size = cmdBuf.Length;
                                sidecar.BlockMedia[0].SCSI.ModeSense.Checksums = Core.Checksum.GetChecksums(cmdBuf).ToArray();
                                writeToFile(sidecar.BlockMedia[0].SCSI.ModeSense.Image, cmdBuf);
                            }
                        }

                        if(decMode.HasValue)
                        {
                            scsiMediumType = (byte)decMode.Value.Header.MediumType;
                            if(decMode.Value.Header.BlockDescriptors != null && decMode.Value.Header.BlockDescriptors.Length >= 1)
                                scsiDensityCode = (byte)decMode.Value.Header.BlockDescriptors[0].Density;

                            foreach(Decoders.SCSI.Modes.ModePage modePage in decMode.Value.Pages)
                                if(modePage.Page == 0x05)
                                    containsFloppyPage = true;
                        }
                    }
                }
            }

            if(dskType == MediaType.Unknown)
                dskType = MediaTypeFromSCSI.Get((byte)dev.SCSIType, dev.Manufacturer, dev.Model, scsiMediumType, scsiDensityCode, blocks, blockSize);

            if(dskType == MediaType.Unknown && dev.IsUSB && containsFloppyPage)
                dskType = MediaType.FlashDrive;

            DicConsole.WriteLine("Media identified as {0}", dskType);

            byte[] readBuffer;
            uint blocksToRead = 64;

            ulong errored = 0;
            DateTime start;
            DateTime end;
            double totalDuration = 0;
            double totalChkDuration = 0;
            double currentSpeed = 0;
            double maxSpeed = double.MinValue;
            double minSpeed = double.MaxValue;
            List<ulong> unreadableSectors = new List<ulong>();
            Core.Checksum dataChk;

            aborted = false;
            System.Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = aborted = true;
            };

            // TODO: Raw reading
            bool read6 = false, read10 = false, read12 = false, read16 = false, readcd;
            bool readLong10 = false, readLong16 = false, hldtstReadRaw = false, necReadCDDA = false;
            bool pioneerReadCDDA = false, plextorReadCDDA = false, plextorReadRaw = false, syqReadLong6 = false, syqReadLong10 = false;

            if (compactDisc)
            {
                if (toc == null)
                {
                    DicConsole.ErrorWriteLine("Error trying to decode TOC...");
                    return;
                }

                if (options.Raw)
                {
                    throw new NotImplementedException("CD dumping not yet implemented");
                }
                else
                {
                    // TODO: Check subchannel capabilities
                    readcd = !dev.ReadCd(out readBuffer, out senseBuf, 0, 2448, 1, MmcSectorTypes.AllTypes, false, false, true, MmcHeaderCodes.AllHeaders,
                        true, true, MmcErrorField.None, MmcSubchannel.Raw, dev.Timeout, out duration);

                    if (readcd)
                        DicConsole.WriteLine("Using MMC READ CD command.");
                }

                DicConsole.WriteLine("Trying to read Lead-In...");
                bool gotLeadIn = false;
                int leadInSectorsGood = 0, leadInSectorsTotal = 0;

                initDataFile(options.OutputPrefix + ".leadin.bin");
                dataChk = new DiscImageChef.Core.Checksum();

                start = DateTime.UtcNow;

                readBuffer = null;

                for (int leadInBlock = -150; leadInBlock < 0; leadInBlock++)
                {
                    if (aborted)
                        break;

                    double cmdDuration = 0;

                    if (currentSpeed > maxSpeed && currentSpeed != 0)
                        maxSpeed = currentSpeed;
                    if (currentSpeed < minSpeed && currentSpeed != 0)
                        minSpeed = currentSpeed;

                    DicConsole.Write("\rTrying to read lead-in sector {0} ({1:F3} MiB/sec.)", (int)leadInBlock, currentSpeed);

                    sense = dev.ReadCd(out readBuffer, out senseBuf, (uint)leadInBlock, 2448, 1, MmcSectorTypes.AllTypes, false, false, true, MmcHeaderCodes.AllHeaders,
                        true, true, MmcErrorField.None, MmcSubchannel.Raw, dev.Timeout, out cmdDuration);

                    if (!sense && !dev.Error)
                    {
                        dataChk.Update(readBuffer);
                        writeToDataFile(readBuffer);
                        gotLeadIn = true;
                        leadInSectorsGood++;
                        leadInSectorsTotal++;
                    }
                    else
                    {
                        if (gotLeadIn)
                        {
                            // Write empty data
                            dataChk.Update(new byte[2448]);
                            writeToDataFile(new byte[2448]);
                            leadInSectorsTotal++;
                        }
                    }

                    currentSpeed = ((double)2448 / (double)1048576) / (cmdDuration / (double)1000);
                }

                closeDataFile();
                if (leadInSectorsGood > 0)
                {
                    sidecar.OpticalDisc[0].LeadIn = new BorderType[1];
                    sidecar.OpticalDisc[0].LeadIn[0] = new BorderType();
                    sidecar.OpticalDisc[0].LeadIn[0].Image = options.OutputPrefix + ".leadin.bin";
                    sidecar.OpticalDisc[0].LeadIn[0].Checksums = dataChk.End().ToArray();
                    sidecar.OpticalDisc[0].LeadIn[0].Size = leadInSectorsTotal * 2448;
                }
                else
                    File.Delete(options.OutputPrefix + ".leadin.bin");

                DicConsole.WriteLine();
                DicConsole.WriteLine("Got {0} lead-in sectors.", leadInSectorsGood);

                while (true)
                {
                    if (readcd)
                    {
                        sense = dev.ReadCd(out readBuffer, out senseBuf, 0, 2448, blocksToRead, MmcSectorTypes.AllTypes, false, false, true, MmcHeaderCodes.AllHeaders,
                            true, true, MmcErrorField.None, MmcSubchannel.Raw, dev.Timeout, out duration);
                        if (dev.Error)
                            blocksToRead /= 2;
                    }

                    if (!dev.Error || blocksToRead == 1)
                        break;
                }

                if (dev.Error)
                {
                    DicConsole.ErrorWriteLine("Device error {0} trying to guess ideal transfer length.", dev.LastError);
                    return;
                }

                DicConsole.WriteLine("Reading {0} sectors at a time.", blocksToRead);

                initDataFile(options.OutputPrefix + ".bin");
                initMHDDLogFile(options.OutputPrefix + ".mhddlog.bin", dev, blocks, blockSize, blocksToRead);

                for (ulong i = 0; i < blocks; i += blocksToRead)
                {
                    if (aborted)
                        break;

                    double cmdDuration = 0;

                    if ((blocks - i) < blocksToRead)
                        blocksToRead = (uint)(blocks - i);

                    if (currentSpeed > maxSpeed && currentSpeed != 0)
                        maxSpeed = currentSpeed;
                    if (currentSpeed < minSpeed && currentSpeed != 0)
                        minSpeed = currentSpeed;

                    DicConsole.Write("\rReading sector {0} of {1} ({2:F3} MiB/sec.)", i, blocks, currentSpeed);

                    if (readcd)
                    {
                        sense = dev.ReadCd(out readBuffer, out senseBuf, (uint)i, 2448, blocksToRead, MmcSectorTypes.AllTypes, false, false, true, MmcHeaderCodes.AllHeaders,
                            true, true, MmcErrorField.None, MmcSubchannel.Raw, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }

                    if (!sense && !dev.Error)
                    {
                        writeMHDDLogFile(i, cmdDuration);
                        writeToDataFile(readBuffer);
                    }
                    else
                    {
                        // TODO: Reset device after X errors
                        if (options.StopOnError)
                            return; // TODO: Return more cleanly

                        // Write empty data
                        writeToDataFile(new byte[2448 * blocksToRead]);

                        // TODO: Record error on mapfile

                        errored += blocksToRead;
                        unreadableSectors.Add(i);
                        DicConsole.DebugWriteLine("Dump-Media", "READ error:\n{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                        if (cmdDuration < 500)
                            writeMHDDLogFile(i, 65535);
                        else
                            writeMHDDLogFile(i, cmdDuration);
                    }

                    currentSpeed = ((double)2448 * blocksToRead / (double)1048576) / (cmdDuration / (double)1000);
                }
                DicConsole.WriteLine();
                closeMHDDLogFile();

                dataChk = new Core.Checksum();
                dataFs.Seek(0, SeekOrigin.Begin);
                blocksToRead = 500;

                for (ulong i = 0; i < blocks; i += blocksToRead)
                {
                    if (aborted)
                        break;

                    if ((blocks - i) < blocksToRead)
                        blocksToRead = (uint)(blocks - i);

                    DicConsole.Write("\rChecksumming sector {0} of {1} ({2:F3} MiB/sec.)", i, blocks, currentSpeed);

                    DateTime chkStart = DateTime.UtcNow;
                    byte[] dataToCheck = new byte[blockSize * blocksToRead];
                    dataFs.Read(dataToCheck, 0, (int)(blockSize * blocksToRead));
                    dataChk.Update(dataToCheck);
                    DateTime chkEnd = DateTime.UtcNow;

                    double chkDuration = (chkEnd - chkStart).TotalMilliseconds;
                    totalChkDuration += chkDuration;

                    currentSpeed = ((double)blockSize * blocksToRead / (double)1048576) / (chkDuration / (double)1000);
                }
                DicConsole.WriteLine();
                closeDataFile();
                end = DateTime.UtcNow;

                // TODO: Correct this
                sidecar.OpticalDisc[0].Checksums = dataChk.End().ToArray();
                sidecar.OpticalDisc[0].DumpHardwareArray = new DumpHardwareType[1];
                sidecar.OpticalDisc[0].DumpHardwareArray[0] = new DumpHardwareType();
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents = new ExtentType[1];
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0] = new ExtentType();
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0].Start = 0;
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0].End = (int)(blocks - 1);
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Manufacturer = dev.Manufacturer;
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Model = dev.Model;
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Revision = dev.Revision;
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Software = new SoftwareType();
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.Name = "DiscImageChef";
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.OperatingSystem = dev.PlatformID.ToString();
                sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.Version = typeof(MainClass).Assembly.GetName().Version.ToString();
                sidecar.OpticalDisc[0].Image = new ImageType();
                sidecar.OpticalDisc[0].Image.format = "Raw disk image (sector by sector copy)";
                sidecar.OpticalDisc[0].Image.Value = options.OutputPrefix + ".bin";
                sidecar.OpticalDisc[0].Sessions = 1;
                sidecar.OpticalDisc[0].Tracks = new []{1};
                sidecar.OpticalDisc[0].Track = new TrackType[1];
                sidecar.OpticalDisc[0].Track[0] = new TrackType();
                sidecar.OpticalDisc[0].Track[0].BytesPerSector = (int)blockSize;
                sidecar.OpticalDisc[0].Track[0].Checksums = sidecar.OpticalDisc[0].Checksums;
                sidecar.OpticalDisc[0].Track[0].EndSector = (long)(blocks - 1);
                sidecar.OpticalDisc[0].Track[0].Image = new ImageType();
                sidecar.OpticalDisc[0].Track[0].Image.format = "BINARY";
                sidecar.OpticalDisc[0].Track[0].Image.offset = 0;
                sidecar.OpticalDisc[0].Track[0].Image.offsetSpecified = true;
                sidecar.OpticalDisc[0].Track[0].Image.Value = sidecar.OpticalDisc[0].Image.Value;
                sidecar.OpticalDisc[0].Track[0].Sequence = new TrackSequenceType();
                sidecar.OpticalDisc[0].Track[0].Sequence.Session = 1;
                sidecar.OpticalDisc[0].Track[0].Sequence.TrackNumber = 1;
                sidecar.OpticalDisc[0].Track[0].Size = (long)(blocks * blockSize);
                sidecar.OpticalDisc[0].Track[0].StartSector = 0;
                sidecar.OpticalDisc[0].Track[0].TrackType1 = TrackTypeTrackType.mode1;
                sidecar.OpticalDisc[0].Dimensions = Metadata.Dimensions.DimensionsFromMediaType(dskType);
                string xmlDskTyp, xmlDskSubTyp;
                Metadata.MediaType.MediaTypeToString(dskType, out xmlDskTyp, out xmlDskSubTyp);
                sidecar.OpticalDisc[0].DiscType = xmlDskTyp;
                sidecar.OpticalDisc[0].DiscSubType = xmlDskSubTyp;
            }
            else
            {
                uint longBlockSize = blockSize;
                bool rawAble = false;

                if (options.Raw)
                {
                    bool testSense;
                    Decoders.SCSI.FixedSense? decSense;

                    if (dev.SCSIType != DiscImageChef.Decoders.SCSI.PeripheralDeviceTypes.MultiMediaDevice)
                    {
                        /*testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, 0xFFFF, dev.Timeout, out duration);
                        if (testSense && !dev.Error)
                        {
                            decSense = Decoders.SCSI.Sense.DecodeFixed(senseBuf);
                            if (decSense.HasValue)
                            {
                                if (decSense.Value.SenseKey == DiscImageChef.Decoders.SCSI.SenseKeys.IllegalRequest &&
                                    decSense.Value.ASC == 0x24 && decSense.Value.ASCQ == 0x00)
                                {
                                    rawAble = true;
                                    if (decSense.Value.InformationValid && decSense.Value.ILI)
                                    {
                                        longBlockSize = 0xFFFF - (decSense.Value.Information & 0xFFFF);
                                        readLong16 = !dev.ReadLong16(out readBuffer, out senseBuf, false, 0, longBlockSize, dev.Timeout, out duration);
                                    }
                                }
                            }
                        }*/

                        testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, 0xFFFF, dev.Timeout, out duration);
                        if (testSense && !dev.Error)
                        {
                            decSense = Decoders.SCSI.Sense.DecodeFixed(senseBuf);
                            if (decSense.HasValue)
                            {
                                if (decSense.Value.SenseKey == DiscImageChef.Decoders.SCSI.SenseKeys.IllegalRequest &&
                                    decSense.Value.ASC == 0x24 && decSense.Value.ASCQ == 0x00)
                                {
                                    rawAble = true;
                                    if (decSense.Value.InformationValid && decSense.Value.ILI)
                                    {
                                        longBlockSize = 0xFFFF - (decSense.Value.Information & 0xFFFF);
                                        readLong10 = !dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, (ushort)longBlockSize, dev.Timeout, out duration);
                                    }
                                }
                            }
                        }

                        if (rawAble && longBlockSize == blockSize)
                        {
                            if (blockSize == 512)
                            {
                                // Long sector sizes for 512-byte magneto-opticals
                                foreach (ushort testSize in new []{ 600, 610, 630 })
                                {
                                    testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, testSize, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong16 = true;
                                        longBlockSize = testSize;
                                        rawAble = true;
                                        break;
                                    }

                                    testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, testSize, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong10 = true;
                                        longBlockSize = testSize;
                                        rawAble = true;
                                        break;
                                    }
                                }
                            }
                            else if (blockSize == 1024)
                            {
                                testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, 1200, dev.Timeout, out duration);
                                if (!testSense && !dev.Error)
                                {
                                    readLong16 = true;
                                    longBlockSize = 1200;
                                    rawAble = true;
                                }
                                else
                                {
                                    testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, 1200, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong10 = true;
                                        longBlockSize = 1200;
                                        rawAble = true;
                                    }
                                }
                            }
                            else if (blockSize == 2048)
                            {
                                testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, 2380, dev.Timeout, out duration);
                                if (!testSense && !dev.Error)
                                {
                                    readLong16 = true;
                                    longBlockSize = 2380;
                                    rawAble = true;
                                }
                                else
                                {
                                    testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, 2380, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong10 = true;
                                        longBlockSize = 2380;
                                        rawAble = true;
                                    }
                                }
                            }
                            else if (blockSize == 4096)
                            {
                                testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, 4760, dev.Timeout, out duration);
                                if (!testSense && !dev.Error)
                                {
                                    readLong16 = true;
                                    longBlockSize = 4760;
                                    rawAble = true;
                                }
                                else
                                {
                                    testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, 4760, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong10 = true;
                                        longBlockSize = 4760;
                                        rawAble = true;
                                    }
                                }
                            }
                            else if (blockSize == 8192)
                            {
                                testSense = dev.ReadLong16(out readBuffer, out senseBuf, false, 0, 9424, dev.Timeout, out duration);
                                if (!testSense && !dev.Error)
                                {
                                    readLong16 = true;
                                    longBlockSize = 9424;
                                    rawAble = true;
                                }
                                else
                                {
                                    testSense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, 0, 9424, dev.Timeout, out duration);
                                    if (!testSense && !dev.Error)
                                    {
                                        readLong10 = true;
                                        longBlockSize = 9424;
                                        rawAble = true;
                                    }
                                }
                            }
                        }

                        if (!rawAble && dev.Manufacturer == "SYQUEST")
                        {
                            testSense = dev.SyQuestReadLong10(out readBuffer, out senseBuf, 0, 0xFFFF, dev.Timeout, out duration);
                            if (testSense)
                            {
                                decSense = Decoders.SCSI.Sense.DecodeFixed(senseBuf);
                                if (decSense.HasValue)
                                {
                                    if (decSense.Value.SenseKey == DiscImageChef.Decoders.SCSI.SenseKeys.IllegalRequest &&
                                        decSense.Value.ASC == 0x24 && decSense.Value.ASCQ == 0x00)
                                    {
                                        rawAble = true;
                                        if (decSense.Value.InformationValid && decSense.Value.ILI)
                                        {
                                            longBlockSize = 0xFFFF - (decSense.Value.Information & 0xFFFF);
                                            syqReadLong10 = !dev.SyQuestReadLong10(out readBuffer, out senseBuf, 0, longBlockSize, dev.Timeout, out duration);
                                            ;
                                        }
                                    }
                                    else
                                    {
                                        testSense = dev.SyQuestReadLong6(out readBuffer, out senseBuf, 0, 0xFFFF, dev.Timeout, out duration);
                                        if (testSense)
                                        {
                                            decSense = Decoders.SCSI.Sense.DecodeFixed(senseBuf);
                                            if (decSense.HasValue)
                                            {
                                                if (decSense.Value.SenseKey == DiscImageChef.Decoders.SCSI.SenseKeys.IllegalRequest &&
                                                    decSense.Value.ASC == 0x24 && decSense.Value.ASCQ == 0x00)
                                                {
                                                    rawAble = true;
                                                    if (decSense.Value.InformationValid && decSense.Value.ILI)
                                                    {
                                                        longBlockSize = 0xFFFF - (decSense.Value.Information & 0xFFFF);
                                                        syqReadLong6 = !dev.SyQuestReadLong6(out readBuffer, out senseBuf, 0, longBlockSize, dev.Timeout, out duration);
                                                        ;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (!rawAble && blockSize == 256)
                            {
                                testSense = dev.SyQuestReadLong6(out readBuffer, out senseBuf, 0, 262, dev.Timeout, out duration);
                                if (!testSense && !dev.Error)
                                {
                                    syqReadLong6 = true;
                                    longBlockSize = 262;
                                    rawAble = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (dev.Manufacturer == "HL-DT-ST")
                            hldtstReadRaw = !dev.HlDtStReadRawDvd(out readBuffer, out senseBuf, 0, 1, dev.Timeout, out duration);

                        if (dev.Manufacturer == "PLEXTOR")
                            hldtstReadRaw = !dev.PlextorReadRawDvd(out readBuffer, out senseBuf, 0, 1, dev.Timeout, out duration);

                        if (hldtstReadRaw || plextorReadRaw)
                        {
                            rawAble = true;
                            longBlockSize = 2064;
                        }
                    }

                    if (blockSize == longBlockSize)
                    {
                        if (!rawAble)
                        {
                            DicConsole.ErrorWriteLine("Device doesn't seem capable of reading raw data from media.");
                        }
                        else
                        {
                            DicConsole.ErrorWriteLine("Device is capable of reading raw data but I've been unable to guess correct sector size.");
                        }

                        if (!options.Force)
                        {
                            DicConsole.ErrorWriteLine("Not continuing. If you want to continue reading cooked data when raw is not available use the force option.");
                            // TODO: Exit more gracefully
                            return;
                        }

                        DicConsole.ErrorWriteLine("Continuing dumping cooked data.");
                        options.Raw = false;
                    }
                    else
                    {
                        if(readLong16)
                            DicConsole.WriteLine("Using SCSI READ LONG (16) command.");
                        else if(readLong10)
                            DicConsole.WriteLine("Using SCSI READ LONG (10) command.");
                        else if(syqReadLong10)
                            DicConsole.WriteLine("Using SyQuest READ LONG (10) command.");
                        else if(syqReadLong6)
                            DicConsole.WriteLine("Using SyQuest READ LONG (6) command.");
                        else if(hldtstReadRaw)
                            DicConsole.WriteLine("Using HL-DT-ST raw DVD reading.");
                        else if(plextorReadRaw)
                            DicConsole.WriteLine("Using Plextor raw DVD reading.");
                        else
                            throw new AccessViolationException("Should not arrive here");

                        DicConsole.WriteLine("Reading {0} raw bytes ({1} cooked bytes) per sector.",
                            longBlockSize, blockSize);
                        blocksToRead = 1;
                        blockSize = longBlockSize;
                    }
                }

                if (!options.Raw)
                {
                    read6 = !dev.Read6(out readBuffer, out senseBuf, 0, blockSize, dev.Timeout, out duration);

                    read10 = !dev.Read10(out readBuffer, out senseBuf, 0, false, true, false, false, 0, blockSize, 0, 1, dev.Timeout, out duration);

                    read12 = !dev.Read12(out readBuffer, out senseBuf, 0, false, true, false, false, 0, blockSize, 0, 1, false, dev.Timeout, out duration);

                    read16 = !dev.Read16(out readBuffer, out senseBuf, 0, false, true, false, 0, blockSize, 0, 1, false, dev.Timeout, out duration);

                    if (!read6 && !read10 && !read12 && !read16)
                    {
                        DicConsole.ErrorWriteLine("Cannot read medium, aborting scan...");
                        return;
                    }

                    if (read6 && !read10 && !read12 && !read16 && blocks > (0x001FFFFF + 1))
                    {
                        DicConsole.ErrorWriteLine("Device only supports SCSI READ (6) but has more than {0} blocks ({1} blocks total)", 0x001FFFFF + 1, blocks);
                        return;
                    }

                    if (!read16 && blocks > ((long)0xFFFFFFFF + (long)1))
                    {
                        DicConsole.ErrorWriteLine("Device only supports SCSI READ (10) but has more than {0} blocks ({1} blocks total)", (long)0xFFFFFFFF + (long)1, blocks);
                        return;
                    }

                    if (read16)
                        DicConsole.WriteLine("Using SCSI READ (16) command.");
                    else if (read12)
                        DicConsole.WriteLine("Using SCSI READ (12) command.");
                    else if (read10)
                        DicConsole.WriteLine("Using SCSI READ (10) command.");
                    else if (read6)
                        DicConsole.WriteLine("Using SCSI READ (6) command.");

                    while (true)
                    {
                        if (read16)
                        {
                            sense = dev.Read16(out readBuffer, out senseBuf, 0, false, true, false, 0, blockSize, 0, blocksToRead, false, dev.Timeout, out duration);
                            if (dev.Error)
                                blocksToRead /= 2;
                        }
                        else if (read12)
                        {
                            sense = dev.Read12(out readBuffer, out senseBuf, 0, false, false, false, false, 0, blockSize, 0, blocksToRead, false, dev.Timeout, out duration);
                            if (dev.Error)
                                blocksToRead /= 2;
                        }
                        else if (read10)
                        {
                            sense = dev.Read10(out readBuffer, out senseBuf, 0, false, true, false, false, 0, blockSize, 0, (ushort)blocksToRead, dev.Timeout, out duration);
                            if (dev.Error)
                                blocksToRead /= 2;
                        }
                        else if (read6)
                        {
                            sense = dev.Read6(out readBuffer, out senseBuf, 0, blockSize, (byte)blocksToRead, dev.Timeout, out duration);
                            if (dev.Error)
                                blocksToRead /= 2;
                        }

                        if (!dev.Error || blocksToRead == 1)
                            break;
                    }

                    if (dev.Error)
                    {
                        DicConsole.ErrorWriteLine("Device error {0} trying to guess ideal transfer length.", dev.LastError);
                        return;
                    }
                }

                DicConsole.WriteLine("Reading {0} sectors at a time.", blocksToRead);

                initMHDDLogFile(options.OutputPrefix + ".mhddlog.bin", dev, blocks, blockSize, blocksToRead);
                initDataFile(options.OutputPrefix + ".bin");

                start = DateTime.UtcNow;

                readBuffer = null;

                for (ulong i = 0; i < blocks; i += blocksToRead)
                {
                    if (aborted)
                        break;

                    double cmdDuration = 0;

                    if ((blocks - i) < blocksToRead)
                        blocksToRead = (uint)(blocks - i);

                    if (currentSpeed > maxSpeed && currentSpeed != 0)
                        maxSpeed = currentSpeed;
                    if (currentSpeed < minSpeed && currentSpeed != 0)
                        minSpeed = currentSpeed;

                    DicConsole.Write("\rReading sector {0} of {1} ({2:F3} MiB/sec.)", i, blocks, currentSpeed);

                    if(readLong16)
                    {
                        sense = dev.ReadLong16(out readBuffer, out senseBuf, false, i, blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if(readLong10)
                    {
                        sense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, (uint)i, (ushort)blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if(syqReadLong10)
                    {
                        sense = dev.SyQuestReadLong10(out readBuffer, out senseBuf, (uint)i, blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if(syqReadLong6)
                    {
                        sense = dev.SyQuestReadLong6(out readBuffer, out senseBuf, (uint)i, blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if(hldtstReadRaw)
                    {
                        sense = dev.HlDtStReadRawDvd(out readBuffer, out senseBuf, (uint)i, blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if(plextorReadRaw)
                    {
                        sense = dev.PlextorReadRawDvd(out readBuffer, out senseBuf, (uint)i, blockSize, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if (read16)
                    {
                        sense = dev.Read16(out readBuffer, out senseBuf, 0, false, true, false, i, blockSize, 0, blocksToRead, false, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if (read12)
                    {
                        sense = dev.Read12(out readBuffer, out senseBuf, 0, false, false, false, false, (uint)i, blockSize, 0, blocksToRead, false, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if (read10)
                    {
                        sense = dev.Read10(out readBuffer, out senseBuf, 0, false, true, false, false, (uint)i, blockSize, 0, (ushort)blocksToRead, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }
                    else if (read6)
                    {
                        sense = dev.Read6(out readBuffer, out senseBuf, (uint)i, blockSize, (byte)blocksToRead, dev.Timeout, out cmdDuration);
                        totalDuration += cmdDuration;
                    }

                    if (!sense && !dev.Error)
                    {
                        writeMHDDLogFile(i, cmdDuration);
                        writeToDataFile(readBuffer);
                    }
                    else
                    {
                        // TODO: Reset device after X errors
                        if (options.StopOnError)
                            return; // TODO: Return more cleanly

                        // Write empty data
                        writeToDataFile(new byte[blockSize * blocksToRead]);

                        // TODO: Record error on mapfile

                        errored += blocksToRead;
                        unreadableSectors.Add(i);
                        DicConsole.DebugWriteLine("Dump-Media", "READ error:\n{0}", Decoders.SCSI.Sense.PrettifySense(senseBuf));
                        if (cmdDuration < 500)
                            writeMHDDLogFile(i, 65535);
                        else
                            writeMHDDLogFile(i, cmdDuration);
                    }

                    currentSpeed = ((double)blockSize * blocksToRead / (double)1048576) / (cmdDuration / (double)1000);
                }
                DicConsole.WriteLine();
                closeMHDDLogFile();

                #region Error handling
                if (unreadableSectors.Count > 0 && !aborted)
                {
                    List<ulong> tmpList = new List<ulong>();

                    foreach (ulong ur in unreadableSectors)
                    {
                        for (ulong i = ur; i < ur + blocksToRead; i++)
                            tmpList.Add(i);
                    }

                    tmpList.Sort();

                    int pass = 0;
                    bool forward = true;
                    bool runningPersistent = false;

                    unreadableSectors = tmpList;

                repeatRetry:
                    ulong [] tmpArray = unreadableSectors.ToArray();
                    foreach (ulong badSector in tmpArray)
                    {
                        if (aborted)
                            break;
                        
                        double cmdDuration = 0;

                        DicConsole.Write("\rRetrying sector {0}, pass {1}, {3}{2}", badSector, pass + 1, forward ? "forward" : "reverse", runningPersistent ? "recovering partial data, " : "");

                        if(readLong16)
                        {
                            sense = dev.ReadLong16(out readBuffer, out senseBuf, false, badSector, blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if(readLong10)
                        {
                            sense = dev.ReadLong10(out readBuffer, out senseBuf, false, false, (uint)badSector, (ushort)blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if(syqReadLong10)
                        {
                            sense = dev.SyQuestReadLong10(out readBuffer, out senseBuf, (uint)badSector, blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if(syqReadLong6)
                        {
                            sense = dev.SyQuestReadLong6(out readBuffer, out senseBuf, (uint)badSector, blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if(hldtstReadRaw)
                        {
                            sense = dev.HlDtStReadRawDvd(out readBuffer, out senseBuf, (uint)badSector, blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if(plextorReadRaw)
                        {
                            sense = dev.PlextorReadRawDvd(out readBuffer, out senseBuf, (uint)badSector, blockSize, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if (read16)
                        {
                            sense = dev.Read16(out readBuffer, out senseBuf, 0, false, true, false, badSector, blockSize, 0, 1, false, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if (read12)
                        {
                            sense = dev.Read12(out readBuffer, out senseBuf, 0, false, false, false, false, (uint)badSector, blockSize, 0, 1, false, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if (read10)
                        {
                            sense = dev.Read10(out readBuffer, out senseBuf, 0, false, true, false, false, (uint)badSector, blockSize, 0, (ushort)1, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }
                        else if (read6)
                        {
                            sense = dev.Read6(out readBuffer, out senseBuf, (uint)badSector, blockSize, (byte)1, dev.Timeout, out cmdDuration);
                            totalDuration += cmdDuration;
                        }

                        if (!sense && !dev.Error)
                        {
                            unreadableSectors.Remove(badSector);
                            writeToDataFileAtPosition(readBuffer, badSector, blockSize);
                        }
                        else if(runningPersistent)
                            writeToDataFileAtPosition(readBuffer, badSector, blockSize);
                    }

                    if (pass < options.RetryPasses && !aborted && unreadableSectors.Count > 0)
                    {
                        pass++;
                        forward = !forward;
                        unreadableSectors.Sort();
                        unreadableSectors.Reverse();
                        goto repeatRetry;
                    }

                    Decoders.SCSI.Modes.DecodedMode? currentMode = null;
                    Decoders.SCSI.Modes.ModePage? currentModePage = null;
                    byte [] md6 = null;
                    byte [] md10 = null;

                    if (!runningPersistent && options.Persistent)
                    {
                        sense = dev.ModeSense6(out readBuffer, out senseBuf, false, ScsiModeSensePageControl.Current, 0x01, dev.Timeout, out duration);
                        if (sense)
                        {
                            sense = dev.ModeSense10(out readBuffer, out senseBuf, false, ScsiModeSensePageControl.Current, 0x01, dev.Timeout, out duration);
                            if (!sense)
                                currentMode = Decoders.SCSI.Modes.DecodeMode10(readBuffer, dev.SCSIType);
                        }
                        else
                            currentMode = Decoders.SCSI.Modes.DecodeMode6(readBuffer, dev.SCSIType);

                        if (currentMode.HasValue)
                            currentModePage = currentMode.Value.Pages [0];

                        if (dev.SCSIType == Decoders.SCSI.PeripheralDeviceTypes.MultiMediaDevice)
                        {
                            Decoders.SCSI.Modes.ModePage_01_MMC pgMMC = new Decoders.SCSI.Modes.ModePage_01_MMC();
                            pgMMC.PS = false;
                            pgMMC.ReadRetryCount = 255;
                            pgMMC.Parameter = 0x20;

                            Decoders.SCSI.Modes.DecodedMode md = new Decoders.SCSI.Modes.DecodedMode();
                            md.Header = new Decoders.SCSI.Modes.ModeHeader();
                            md.Pages = new Decoders.SCSI.Modes.ModePage [1];
                            md.Pages [0] = new Decoders.SCSI.Modes.ModePage();
                            md.Pages [0].Page = 0x01;
                            md.Pages [0].Subpage = 0x00;
                            md.Pages [0].PageResponse = Decoders.SCSI.Modes.EncodeModePage_01_MMC(pgMMC);
                            md6 = Decoders.SCSI.Modes.EncodeMode6(md, dev.SCSIType);
                            md10 = Decoders.SCSI.Modes.EncodeMode10(md, dev.SCSIType);
                        }
                        else
                        {
                            Decoders.SCSI.Modes.ModePage_01 pg = new Decoders.SCSI.Modes.ModePage_01();
                            pg.PS = false;
                            pg.AWRE = false;
                            pg.ARRE = false;
                            pg.TB = true;
                            pg.RC = false;
                            pg.EER = true;
                            pg.PER = false;
                            pg.DTE = false;
                            pg.DCR = false;
                            pg.ReadRetryCount = 255;

                            Decoders.SCSI.Modes.DecodedMode md = new Decoders.SCSI.Modes.DecodedMode();
                            md.Header = new Decoders.SCSI.Modes.ModeHeader();
                            md.Pages = new Decoders.SCSI.Modes.ModePage [1];
                            md.Pages [0] = new Decoders.SCSI.Modes.ModePage();
                            md.Pages [0].Page = 0x01;
                            md.Pages [0].Subpage = 0x00;
                            md.Pages [0].PageResponse = Decoders.SCSI.Modes.EncodeModePage_01(pg);
                            md6 = Decoders.SCSI.Modes.EncodeMode6(md, dev.SCSIType);
                            md10 = Decoders.SCSI.Modes.EncodeMode10(md, dev.SCSIType);
                        }

                        sense = dev.ModeSelect(md6, out senseBuf, true, false, dev.Timeout, out duration);
                        if (sense)
                        {
                            sense = dev.ModeSelect10(md10, out senseBuf, true, false, dev.Timeout, out duration);
                        }

                        runningPersistent = true;
                        if (!sense && !dev.Error)
                        {
                            pass = 0;
                            goto repeatRetry;
                        }
                    }
                    else if (runningPersistent && options.Persistent && currentModePage.HasValue)
                    {
                        Decoders.SCSI.Modes.DecodedMode md = new Decoders.SCSI.Modes.DecodedMode();
                        md.Header = new Decoders.SCSI.Modes.ModeHeader();
                        md.Pages = new Decoders.SCSI.Modes.ModePage [1];
                        md.Pages [0] = currentModePage.Value;
                        md6 = Decoders.SCSI.Modes.EncodeMode6(md, dev.SCSIType);
                        md10 = Decoders.SCSI.Modes.EncodeMode10(md, dev.SCSIType);

                        sense = dev.ModeSelect(md6, out senseBuf, true, false, dev.Timeout, out duration);
                        if (sense)
                        {
                            sense = dev.ModeSelect10(md10, out senseBuf, true, false, dev.Timeout, out duration);
                        }
                    }

                    DicConsole.WriteLine();
                }
                #endregion Error handling

                dataChk = new Core.Checksum();
                dataFs.Seek(0, SeekOrigin.Begin);
                blocksToRead = 500;

                for (ulong i = 0; i < blocks; i += blocksToRead)
                {
                    if (aborted)
                        break;

                    if ((blocks - i) < blocksToRead)
                        blocksToRead = (uint)(blocks - i);

                    DicConsole.Write("\rChecksumming sector {0} of {1} ({2:F3} MiB/sec.)", i, blocks, currentSpeed);

                    DateTime chkStart = DateTime.UtcNow;
                    byte[] dataToCheck = new byte[blockSize * blocksToRead];
                    dataFs.Read(dataToCheck, 0, (int)(blockSize * blocksToRead));
                    dataChk.Update(dataToCheck);
                    DateTime chkEnd = DateTime.UtcNow;

                    double chkDuration = (chkEnd - chkStart).TotalMilliseconds;
                    totalChkDuration += chkDuration;

                    currentSpeed = ((double)blockSize * blocksToRead / (double)1048576) / (chkDuration / (double)1000);
                }
                DicConsole.WriteLine();
                closeDataFile();
                end = DateTime.UtcNow;

                if (opticalDisc)
                {
                    sidecar.OpticalDisc[0].Checksums = dataChk.End().ToArray();
                    sidecar.OpticalDisc[0].DumpHardwareArray = new DumpHardwareType[1];
                    sidecar.OpticalDisc[0].DumpHardwareArray[0] = new DumpHardwareType();
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents = new ExtentType[1];
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0] = new ExtentType();
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0].Start = 0;
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Extents[0].End = (int)(blocks - 1);
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Manufacturer = dev.Manufacturer;
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Model = dev.Model;
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Revision = dev.Revision;
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Software = new SoftwareType();
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.Name = "DiscImageChef";
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.OperatingSystem = dev.PlatformID.ToString();
                    sidecar.OpticalDisc[0].DumpHardwareArray[0].Software.Version = typeof(MainClass).Assembly.GetName().Version.ToString();
                    sidecar.OpticalDisc[0].Image = new ImageType();
                    sidecar.OpticalDisc[0].Image.format = "Raw disk image (sector by sector copy)";
                    sidecar.OpticalDisc[0].Image.Value = options.OutputPrefix + ".bin";
                    // TODO: Implement layers
                    //sidecar.OpticalDisc[0].Layers = new LayersType();
                    sidecar.OpticalDisc[0].Sessions = 1;
                    sidecar.OpticalDisc[0].Tracks = new []{1};
                    sidecar.OpticalDisc[0].Track = new TrackType[1];
                    sidecar.OpticalDisc[0].Track[0] = new TrackType();
                    sidecar.OpticalDisc[0].Track[0].BytesPerSector = (int)blockSize;
                    sidecar.OpticalDisc[0].Track[0].Checksums = sidecar.OpticalDisc[0].Checksums;
                    sidecar.OpticalDisc[0].Track[0].EndSector = (long)(blocks - 1);
                    sidecar.OpticalDisc[0].Track[0].Image = new ImageType();
                    sidecar.OpticalDisc[0].Track[0].Image.format = "BINARY";
                    sidecar.OpticalDisc[0].Track[0].Image.offset = 0;
                    sidecar.OpticalDisc[0].Track[0].Image.offsetSpecified = true;
                    sidecar.OpticalDisc[0].Track[0].Image.Value = sidecar.OpticalDisc[0].Image.Value;
                    sidecar.OpticalDisc[0].Track[0].Sequence = new TrackSequenceType();
                    sidecar.OpticalDisc[0].Track[0].Sequence.Session = 1;
                    sidecar.OpticalDisc[0].Track[0].Sequence.TrackNumber = 1;
                    sidecar.OpticalDisc[0].Track[0].Size = (long)(blocks * blockSize);
                    sidecar.OpticalDisc[0].Track[0].StartSector = 0;
                    switch (dskType)
                    {
                        case MediaType.DDCD:
                        case MediaType.DDCDR:
                        case MediaType.DDCDRW:
                            sidecar.OpticalDisc[0].Track[0].TrackType1 = TrackTypeTrackType.ddcd;
                            break;
                        case MediaType.DVDROM:
                        case MediaType.DVDR:
                        case MediaType.DVDRAM:
                        case MediaType.DVDRW:
                        case MediaType.DVDRDL:
                        case MediaType.DVDRWDL:
                        case MediaType.DVDDownload:
                        case MediaType.DVDPRW:
                        case MediaType.DVDPR:
                        case MediaType.DVDPRWDL:
                        case MediaType.DVDPRDL:
                            sidecar.OpticalDisc[0].Track[0].TrackType1 = TrackTypeTrackType.dvd;
                            break;
                        case MediaType.HDDVDROM:
                        case MediaType.HDDVDR:
                        case MediaType.HDDVDRAM:
                        case MediaType.HDDVDRW:
                        case MediaType.HDDVDRDL:
                        case MediaType.HDDVDRWDL:
                            sidecar.OpticalDisc[0].Track[0].TrackType1 = TrackTypeTrackType.hddvd;
                            break;
                        case MediaType.BDROM:
                        case MediaType.BDR:
                        case MediaType.BDRE:
                        case MediaType.BDREXL:
                        case MediaType.BDRXL:
                            sidecar.OpticalDisc[0].Track[0].TrackType1 = TrackTypeTrackType.bluray;
                            break;
                    }
                    sidecar.OpticalDisc[0].Dimensions = Metadata.Dimensions.DimensionsFromMediaType(dskType);
                    string xmlDskTyp, xmlDskSubTyp;
                    Metadata.MediaType.MediaTypeToString(dskType, out xmlDskTyp, out xmlDskSubTyp);
                    sidecar.OpticalDisc[0].DiscType = xmlDskTyp;
                    sidecar.OpticalDisc[0].DiscSubType = xmlDskSubTyp;
                }
                else
                {
                    sidecar.BlockMedia[0].Checksums = dataChk.End().ToArray();
                    sidecar.BlockMedia[0].Dimensions = Metadata.Dimensions.DimensionsFromMediaType(dskType);
                    string xmlDskTyp, xmlDskSubTyp;
                    Metadata.MediaType.MediaTypeToString(dskType, out xmlDskTyp, out xmlDskSubTyp);
                    sidecar.BlockMedia[0].DiskType = xmlDskTyp;
                    sidecar.BlockMedia[0].DiskSubType = xmlDskSubTyp;
                    // TODO: Implement device firmware revision
                    sidecar.BlockMedia[0].Image = new ImageType();
                    sidecar.BlockMedia[0].Image.format = "Raw disk image (sector by sector copy)";
                    sidecar.BlockMedia[0].Image.Value = options.OutputPrefix + ".bin";
                    if (dev.Type == DeviceType.ATAPI)
                        sidecar.BlockMedia[0].Interface = "ATAPI";
                    else if (dev.IsUSB)
                        sidecar.BlockMedia[0].Interface = "USB";
                    else if (dev.IsFireWire)
                        sidecar.BlockMedia[0].Interface = "FireWire";
                    else
                        sidecar.BlockMedia[0].Interface = "SCSI";
                    sidecar.BlockMedia[0].LogicalBlocks = (long)blocks;
                    sidecar.BlockMedia[0].LogicalBlockSize = (int)blockSize;
                    sidecar.BlockMedia[0].Manufacturer = dev.Manufacturer;
                    sidecar.BlockMedia[0].Model = dev.Model;
                    sidecar.BlockMedia[0].Serial = dev.Serial;
                    sidecar.BlockMedia[0].Size = (long)(blocks * blockSize);
                }
            }

            DicConsole.WriteLine();

            DicConsole.WriteLine("Took a total of {0:F3} seconds ({1:F3} processing commands, {2:F3} checksumming).", (end - start).TotalSeconds, totalDuration / 1000, totalChkDuration / 1000);
            DicConsole.WriteLine("Avegare speed: {0:F3} MiB/sec.", (((double)blockSize * (double)(blocks + 1)) / 1048576) / (totalDuration / 1000));
            DicConsole.WriteLine("Fastest speed burst: {0:F3} MiB/sec.", maxSpeed);
            DicConsole.WriteLine("Slowest speed burst: {0:F3} MiB/sec.", minSpeed);
            DicConsole.WriteLine("{0} sectors could not be read.", unreadableSectors.Count);
            if (unreadableSectors.Count > 0)
            {
                foreach (ulong bad in unreadableSectors)
                    DicConsole.WriteLine("Sector {0} could not be read", bad);
            }
            DicConsole.WriteLine();

            if (!aborted)
            {
                DicConsole.WriteLine("Writing metadata sidecar");

                FileStream xmlFs = new FileStream(options.OutputPrefix + ".cicm.xml",
                                       FileMode.Create);

                System.Xml.Serialization.XmlSerializer xmlSer = new System.Xml.Serialization.XmlSerializer(typeof(CICMMetadataType));
                xmlSer.Serialize(xmlFs, sidecar);
                xmlFs.Close();
            }

            Core.Statistics.AddMedia(dskType, true);
        }

        static void initMHDDLogFile(string outputFile, Device dev, ulong blocks, ulong blockSize, ulong blocksToRead)
        {
            if (dev != null && !string.IsNullOrEmpty(outputFile))
            {
                mhddFs = new FileStream(outputFile, FileMode.Create);

                string device;
                string mode;
                string fw;
                string sn;
                string sectors;
                string sectorsize;
                string scanblocksize;
                string ver;

                switch (dev.Type)
                {
                    case DeviceType.ATA:
                    case DeviceType.ATAPI:
                        mode = "MODE: IDE";
                        break;
                    case DeviceType.SCSI:
                        mode = "MODE: SCSI";
                        break;
                    case DeviceType.MMC:
                        mode = "MODE: MMC";
                        break;
                    case DeviceType.NVMe:
                        mode = "MODE: NVMe";
                        break;
                    case DeviceType.SecureDigital:
                        mode = "MODE: SD";
                        break;
                    default:
                        mode = "MODE: IDE";
                        break;
                }

                device = String.Format("DEVICE: {0} {1}", dev.Manufacturer, dev.Model);
                fw = String.Format("F/W: {0}", dev.Revision);
                sn = String.Format("S/N: {0}", dev.Serial);
                sectors = String.Format(new System.Globalization.CultureInfo("en-US"), "SECTORS: {0:n0}", blocks);
                sectorsize = String.Format(new System.Globalization.CultureInfo("en-US"), "SECTOR SIZE: {0:n0} bytes", blockSize);
                scanblocksize = String.Format(new System.Globalization.CultureInfo("en-US"), "SCAN BLOCK SIZE: {0:n0} sectors", blocksToRead);
                ver = "VER:2 ";

                byte[] deviceBytes = Encoding.ASCII.GetBytes(device);
                byte[] modeBytes = Encoding.ASCII.GetBytes(mode);
                byte[] fwBytes = Encoding.ASCII.GetBytes(fw);
                byte[] snBytes = Encoding.ASCII.GetBytes(sn);
                byte[] sectorsBytes = Encoding.ASCII.GetBytes(sectors);
                byte[] sectorsizeBytes = Encoding.ASCII.GetBytes(sectorsize);
                byte[] scanblocksizeBytes = Encoding.ASCII.GetBytes(scanblocksize);
                byte[] verBytes = Encoding.ASCII.GetBytes(ver);

                uint Pointer = (uint)(deviceBytes.Length + modeBytes.Length + fwBytes.Length +
                               snBytes.Length + sectorsBytes.Length + sectorsizeBytes.Length +
                               scanblocksizeBytes.Length + verBytes.Length +
                               2 * 9 + // New lines
                               4); // Pointer

                byte[] newLine = new byte[2];
                newLine[0] = 0x0D;
                newLine[1] = 0x0A;

                mhddFs.Write(BitConverter.GetBytes(Pointer), 0, 4);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(verBytes, 0, verBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(modeBytes, 0, modeBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(deviceBytes, 0, deviceBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(fwBytes, 0, fwBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(snBytes, 0, snBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(sectorsBytes, 0, sectorsBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(sectorsizeBytes, 0, sectorsizeBytes.Length);
                mhddFs.Write(newLine, 0, 2);
                mhddFs.Write(scanblocksizeBytes, 0, scanblocksizeBytes.Length);
                mhddFs.Write(newLine, 0, 2);
            }
        }

        static void closeMHDDLogFile()
        {
            if (mhddFs != null)
                mhddFs.Close();
        }

        static void writeMHDDLogFile(ulong sector, double duration)
        {
            if (mhddFs != null)
            {
                byte[] sectorBytes = BitConverter.GetBytes(sector);
                byte[] durationBytes = BitConverter.GetBytes((ulong)(duration * 1000));

                mhddFs.Write(sectorBytes, 0, 8);
                mhddFs.Write(durationBytes, 0, 8);
            }
        }

        static void writeToFile(string file, byte[] data)
        {
            FileStream fs = new FileStream(file, FileMode.Create, FileAccess.ReadWrite);
            fs.Write(data, 0, data.Length);
            fs.Close();
        }

        static void initDataFile(string outputFile)
        {
            dataFs = new FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        static void writeToDataFile(byte[] data)
        {
            dataFs.Write(data, 0, data.Length);
        }

        static void writeToDataFileAtPosition(byte[] data, ulong block, uint blockSize)
        {
            dataFs.Seek((long)(block * blockSize), SeekOrigin.Begin);
            dataFs.Write(data, 0, data.Length);
        }

        static void closeDataFile()
        {
            if (dataFs != null)
                dataFs.Close();
        }
    }
}