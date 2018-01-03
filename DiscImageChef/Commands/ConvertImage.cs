﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : ConvertImage.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Verbs.
//
// --[ Description ] ----------------------------------------------------------
//
//     Converts from one media image to another.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscImageChef.Console;
using DiscImageChef.Core;
using DiscImageChef.DiscImages;
using DiscImageChef.Filters;
using Version = DiscImageChef.Interop.Version;

namespace DiscImageChef.Commands
{
    public static class ConvertImage
    {
        public static void DoConvert(ConvertImageOptions options)
        {
            DicConsole.DebugWriteLine("Analyze command", "--debug={0}",              options.Debug);
            DicConsole.DebugWriteLine("Analyze command", "--verbose={0}",            options.Verbose);
            DicConsole.DebugWriteLine("Analyze command", "--input={0}",              options.InputFile);
            DicConsole.DebugWriteLine("Analyze command", "--output={0}",             options.OutputFile);
            DicConsole.DebugWriteLine("Analyze command", "--format={0}",             options.OutputFormat);
            DicConsole.DebugWriteLine("Analyze command", "--count={0}",              options.Count);
            DicConsole.DebugWriteLine("Analyze command", "--force={0}",              options.Force);
            DicConsole.DebugWriteLine("Analyze command", "--creator={0}",            options.Creator);
            DicConsole.DebugWriteLine("Analyze command", "--media-title={0}",        options.MediaTitle);
            DicConsole.DebugWriteLine("Analyze command", "--comments={0}",           options.Comments);
            DicConsole.DebugWriteLine("Analyze command", "--media-manufacturer={0}", options.MediaManufacturer);
            DicConsole.DebugWriteLine("Analyze command", "--media-model={0}",        options.MediaModel);
            DicConsole.DebugWriteLine("Analyze command", "--media-serial={0}",       options.MediaSerialNumber);
            DicConsole.DebugWriteLine("Analyze command", "--media-barcode={0}",      options.MediaBarcode);
            DicConsole.DebugWriteLine("Analyze command", "--media-partnumber={0}",   options.MediaPartNumber);
            DicConsole.DebugWriteLine("Analyze command", "--media-sequence={0}",     options.MediaSequence);
            DicConsole.DebugWriteLine("Analyze command", "--media-lastsequence={0}", options.LastMediaSequence);
            DicConsole.DebugWriteLine("Analyze command", "--drive-manufacturer={0}", options.DriveManufacturer);
            DicConsole.DebugWriteLine("Analyze command", "--drive-model={0}",        options.DriveModel);
            DicConsole.DebugWriteLine("Analyze command", "--drive-serial={0}",       options.DriveSerialNumber);
            DicConsole.DebugWriteLine("Analyze command", "--drive-revision={0}",     options.DriveFirmwareRevision);

            if(options.Count == 0)
            {
                DicConsole.ErrorWriteLine("Need to specify more than 0 sectors to copy at once");
                return;
            }

            FiltersList filtersList = new FiltersList();
            IFilter     inputFilter = filtersList.GetFilter(options.InputFile);

            if(inputFilter == null)
            {
                DicConsole.ErrorWriteLine("Cannot open specified file.");
                return;
            }

            PluginBase  plugins     = new PluginBase();
            IMediaImage inputFormat = ImageFormat.Detect(inputFilter);

            if(inputFormat == null)
            {
                DicConsole.WriteLine("Input image format not identified, not proceeding with conversion.");
                return;
            }

            if(options.Verbose)
                DicConsole.VerboseWriteLine("Input image format identified by {0} ({1}).", inputFormat.Name,
                                            inputFormat.Id);
            else DicConsole.WriteLine("Input image format identified by {0}.", inputFormat.Name);

            try
            {
                if(!inputFormat.Open(inputFilter))
                {
                    DicConsole.WriteLine("Unable to open image format");
                    DicConsole.WriteLine("No error given");
                    return;
                }

                DicConsole.DebugWriteLine("Convert-image command", "Correctly opened image file.");
                DicConsole.DebugWriteLine("Convert-image command", "Image without headers is {0} bytes.",
                                          inputFormat.Info.ImageSize);
                DicConsole.DebugWriteLine("Convert-image command", "Image has {0} sectors.", inputFormat.Info.Sectors);
                DicConsole.DebugWriteLine("Convert-image command", "Image identifies media type as {0}.",
                                          inputFormat.Info.MediaType);

                Core.Statistics.AddMediaFormat(inputFormat.Format);
                Core.Statistics.AddMedia(inputFormat.Info.MediaType, false);
                Core.Statistics.AddFilter(inputFilter.Name);
            }
            catch(Exception ex)
            {
                DicConsole.ErrorWriteLine("Unable to open image format");
                DicConsole.ErrorWriteLine("Error: {0}", ex.Message);
                DicConsole.DebugWriteLine("Convert-image command", "Stack trace: {0}", ex.StackTrace);
                return;
            }

            List<IWritableImage> candidates = new List<IWritableImage>();

            // Try extension
            if(string.IsNullOrEmpty(options.OutputFormat))
                candidates.AddRange(plugins.WritableImages.Values.Where(t =>
                                                                            t.KnownExtensions
                                                                             .Contains(Path.GetExtension(options
                                                                                                            .OutputFile))));
            // Try Id
            else if(Guid.TryParse(options.OutputFormat, out Guid outId))
                candidates.AddRange(plugins.WritableImages.Values.Where(t => t.Id.Equals(outId)));
            // Try name
            else
                candidates.AddRange(plugins.WritableImages.Values.Where(t => string.Equals(t.Name, options.OutputFormat,
                                                                                           StringComparison
                                                                                              .InvariantCultureIgnoreCase)));

            if(candidates.Count == 0)
            {
                DicConsole.WriteLine("No plugin supports requested extension.");
                return;
            }

            if(candidates.Count > 1)
            {
                DicConsole.WriteLine("More than one plugin supports requested extension.");
                return;
            }

            IWritableImage outputFormat = candidates[0];

            if(options.Verbose)
                DicConsole.VerboseWriteLine("Output image format: {0} ({1}).", outputFormat.Name, outputFormat.Id);
            else DicConsole.WriteLine("Output image format: {0}.", outputFormat.Name);

            if(!outputFormat.SupportedMediaTypes.Contains(inputFormat.Info.MediaType))
            {
                DicConsole.ErrorWriteLine("Output format does not support media type, cannot continue...");
                return;
            }

            foreach(MediaTagType mediaTag in inputFormat.Info.ReadableMediaTags)
            {
                if(outputFormat.SupportedMediaTags.Contains(mediaTag) || options.Force) continue;

                DicConsole.ErrorWriteLine("Converting image will lose media tag {0}, not continuing...", mediaTag);
                DicConsole.ErrorWriteLine("If you don't care, use force option.");
                return;
            }

            bool useLong = inputFormat.Info.ReadableSectorTags.Count != 0;

            foreach(SectorTagType sectorTag in inputFormat.Info.ReadableSectorTags)
            {
                if(outputFormat.SupportedSectorTags.Contains(sectorTag)) continue;

                if(options.Force)
                {
                    useLong = false;
                    continue;
                }

                DicConsole.ErrorWriteLine("Converting image will lose sector tag {0}, not continuing...", sectorTag);
                DicConsole
                   .ErrorWriteLine("If you don't care, use force option. This will skip all sector tags converting only user data.");
                return;
            }

            if(!outputFormat.Create(options.OutputFile, inputFormat.Info.MediaType, new Dictionary<string, string>(),
                                    inputFormat.Info.Sectors, inputFormat.Info.SectorSize))
            {
                DicConsole.ErrorWriteLine("Error {0} creating output image.", outputFormat.ErrorMessage);
                return;
            }

            ImageInfo metadata = new ImageInfo
            {
                Application           = "DiscImageChef",
                ApplicationVersion    = Version.GetVersion(),
                Comments              = options.Comments,
                Creator               = options.Creator,
                DriveFirmwareRevision = options.DriveFirmwareRevision,
                DriveManufacturer     = options.DriveManufacturer,
                DriveModel            = options.DriveModel,
                DriveSerialNumber     = options.DriveSerialNumber,
                LastMediaSequence     = options.LastMediaSequence,
                MediaBarcode          = options.MediaBarcode,
                MediaManufacturer     = options.MediaManufacturer,
                MediaModel            = options.MediaModel,
                MediaPartNumber       = options.MediaPartNumber,
                MediaSequence         = options.MediaSequence,
                MediaSerialNumber     = options.MediaSerialNumber,
                MediaTitle            = options.MediaTitle
            };

            if(!outputFormat.SetMetadata(metadata))
            {
                DicConsole.ErrorWrite("Error {0} setting metadata, ", outputFormat.ErrorMessage);
                if(!options.Force)
                {
                    DicConsole.ErrorWriteLine("not continuing...");
                    return;
                }

                DicConsole.ErrorWriteLine("continuing...");
            }

            List<Track> tracks;

            try { tracks              = inputFormat.Tracks; }
            catch(Exception) { tracks = null; }

            if(tracks != null)
                if(!outputFormat.SetTracks(tracks))
                {
                    DicConsole.ErrorWriteLine("Error {0} sending tracks list to output image.",
                                              outputFormat.ErrorMessage);
                    return;
                }

            foreach(MediaTagType mediaTag in inputFormat.Info.ReadableMediaTags)
            {
                DicConsole.WriteLine("Converting media tag {0}", mediaTag);
                byte[] tag = inputFormat.ReadDiskTag(mediaTag);
                if(outputFormat.WriteMediaTag(tag, mediaTag)) continue;

                if(options.Force)
                    DicConsole.ErrorWriteLine("Error {0} writing media tag, continuing...", outputFormat.ErrorMessage);
                else
                {
                    DicConsole.ErrorWriteLine("Error {0} writing media tag, not continuing...",
                                              outputFormat.ErrorMessage);
                    return;
                }
            }

            DicConsole.WriteLine("{0} sectors to convert", inputFormat.Info.Sectors);
            ulong doneSectors = 0;

            if(tracks == null)
            {
                while(doneSectors < inputFormat.Info.Sectors)
                {
                    byte[] sector;

                    uint sectorsToDo;
                    if(inputFormat.Info.Sectors - doneSectors >= (ulong)options.Count)
                        sectorsToDo  = (uint)options.Count;
                    else sectorsToDo = (uint)(inputFormat.Info.Sectors - doneSectors);

                    DicConsole.Write("\rConverting sectors {0} to {1} ({2:P2} done)", doneSectors,
                                     doneSectors + sectorsToDo, doneSectors / (double)inputFormat.Info.Sectors);

                    bool result;
                    if(useLong)
                        if(sectorsToDo == 1)
                        {
                            sector = inputFormat.ReadSectorLong(doneSectors);
                            result = outputFormat.WriteSectorLong(sector, doneSectors);
                        }
                        else
                        {
                            sector = inputFormat.ReadSectorsLong(doneSectors, sectorsToDo);
                            result = outputFormat.WriteSectorsLong(sector, doneSectors, sectorsToDo);
                        }
                    else
                    {
                        if(sectorsToDo == 1)
                        {
                            sector = inputFormat.ReadSector(doneSectors);
                            result = outputFormat.WriteSector(sector, doneSectors);
                        }
                        else
                        {
                            sector = inputFormat.ReadSectors(doneSectors, sectorsToDo);
                            result = outputFormat.WriteSectors(sector, doneSectors, sectorsToDo);
                        }
                    }

                    if(!result)
                        if(options.Force)
                            DicConsole.ErrorWriteLine("Error {0} writing sector {1}, continuing...",
                                                      outputFormat.ErrorMessage, doneSectors);
                        else
                        {
                            DicConsole.ErrorWriteLine("Error {0} writing sector {1}, not continuing...",
                                                      outputFormat.ErrorMessage, doneSectors);
                            return;
                        }

                    doneSectors += sectorsToDo;
                }

                DicConsole.Write("\rConverting sectors {0} to {1} ({2:P2} done)", inputFormat.Info.Sectors,
                                 inputFormat.Info.Sectors, 1.0);
                DicConsole.WriteLine();

                foreach(SectorTagType tag in inputFormat.Info.ReadableSectorTags)
                {
                    if(!useLong) break;

                    switch(tag)
                    {
                        case SectorTagType.AppleSectorTag:
                        case SectorTagType.CdSectorSync:
                        case SectorTagType.CdSectorHeader:
                        case SectorTagType.CdSectorSubHeader:
                        case SectorTagType.CdSectorEdc:
                        case SectorTagType.CdSectorEccP:
                        case SectorTagType.CdSectorEccQ:
                        case SectorTagType.CdSectorEcc:
                            // This tags are inline in long sector
                            continue;
                    }

                    doneSectors = 0;
                    while(doneSectors < inputFormat.Info.Sectors)
                    {
                        byte[] sector;

                        uint sectorsToDo;
                        if(inputFormat.Info.Sectors - doneSectors >= (ulong)options.Count)
                            sectorsToDo  = (uint)options.Count;
                        else sectorsToDo = (uint)(inputFormat.Info.Sectors - doneSectors);

                        DicConsole.Write("\rConverting tag {2} for sectors {0} to {1} ({2:P2} done)", doneSectors,
                                         doneSectors + sectorsToDo, doneSectors / (double)inputFormat.Info.Sectors,
                                         tag);

                        bool result;
                        if(sectorsToDo == 1)
                        {
                            sector = inputFormat.ReadSectorTag(doneSectors, tag);
                            result = outputFormat.WriteSectorTag(sector, doneSectors, tag);
                        }
                        else
                        {
                            sector = inputFormat.ReadSectorsTag(doneSectors, sectorsToDo, tag);
                            result = outputFormat.WriteSectorsTag(sector, doneSectors, sectorsToDo, tag);
                        }

                        if(!result)
                            if(options.Force)
                                DicConsole.ErrorWriteLine("Error {0} writing sector {1}, continuing...",
                                                          outputFormat.ErrorMessage, doneSectors);
                            else
                            {
                                DicConsole.ErrorWriteLine("Error {0} writing sector {1}, not continuing...",
                                                          outputFormat.ErrorMessage, doneSectors);
                                return;
                            }

                        doneSectors += sectorsToDo;
                    }

                    DicConsole.Write("\rConverting tag {2} for sectors {0} to {1} ({2:P2} done)",
                                     inputFormat.Info.Sectors, inputFormat.Info.Sectors, 1.0, tag);
                    DicConsole.WriteLine();
                }
            }
            else
            {
                foreach(Track track in tracks)
                {
                    doneSectors        = 0;
                    ulong trackSectors = track.TrackEndSector - track.TrackStartSector + 1;

                    while(doneSectors < trackSectors)
                    {
                        byte[] sector;

                        uint sectorsToDo;
                        if(trackSectors - doneSectors >= (ulong)options.Count) sectorsToDo = (uint)options.Count;
                        else
                            sectorsToDo =
                                (uint)(trackSectors - doneSectors);

                        DicConsole.Write("\rConverting sectors {0} to {1} in track {3} ({2:P2} done)",
                                         doneSectors  + track.TrackStartSector,
                                         doneSectors  + sectorsToDo + track.TrackStartSector,
                                         (doneSectors + track.TrackStartSector) / (double)inputFormat.Info.Sectors,
                                         track.TrackSequence);

                        bool result;
                        if(useLong)
                            if(sectorsToDo == 1)
                            {
                                sector = inputFormat.ReadSectorLong(doneSectors           + track.TrackStartSector);
                                result = outputFormat.WriteSectorLong(sector, doneSectors + track.TrackStartSector);
                            }
                            else
                            {
                                sector = inputFormat.ReadSectorsLong(doneSectors + track.TrackStartSector,
                                                                     sectorsToDo);
                                result = outputFormat.WriteSectorsLong(sector, doneSectors + track.TrackStartSector,
                                                                       sectorsToDo);
                            }
                        else
                        {
                            if(sectorsToDo == 1)
                            {
                                sector = inputFormat.ReadSector(doneSectors           + track.TrackStartSector);
                                result = outputFormat.WriteSector(sector, doneSectors + track.TrackStartSector);
                            }
                            else
                            {
                                sector = inputFormat.ReadSectors(doneSectors + track.TrackStartSector,
                                                                 sectorsToDo);
                                result = outputFormat.WriteSectors(sector, doneSectors + track.TrackStartSector,
                                                                   sectorsToDo);
                            }
                        }

                        if(!result)
                            if(options.Force)
                                DicConsole.ErrorWriteLine("Error {0} writing sector {1}, continuing...",
                                                          outputFormat.ErrorMessage, doneSectors);
                            else
                            {
                                DicConsole.ErrorWriteLine("Error {0} writing sector {1}, not continuing...",
                                                          outputFormat.ErrorMessage, doneSectors);
                                return;
                            }

                        doneSectors += sectorsToDo;
                    }
                }

                DicConsole.Write("\rConverting sectors {0} to {1} in track {3} ({2:P2} done)", inputFormat.Info.Sectors,
                                 inputFormat.Info.Sectors, 1.0, tracks.Count);
                DicConsole.WriteLine();

                foreach(SectorTagType tag in inputFormat.Info.ReadableSectorTags.OrderBy(t => t))
                {
                    if(!useLong) break;

                    switch(tag)
                    {
                        case SectorTagType.AppleSectorTag:
                        case SectorTagType.CdSectorSync:
                        case SectorTagType.CdSectorHeader:
                        case SectorTagType.CdSectorSubHeader:
                        case SectorTagType.CdSectorEdc:
                        case SectorTagType.CdSectorEccP:
                        case SectorTagType.CdSectorEccQ:
                        case SectorTagType.CdSectorEcc:
                            // This tags are inline in long sector
                            continue;
                    }

                    foreach(Track track in tracks)
                    {
                        doneSectors         = 0;
                        ulong  trackSectors = track.TrackEndSector - track.TrackStartSector + 1;
                        byte[] sector;
                        bool   result;

                        switch(tag)
                        {
                            case SectorTagType.CdTrackFlags:
                            case SectorTagType.CdTrackIsrc:
                                DicConsole.Write("\rConverting tag {0} in track {1} ({2:P2} done).", tag,
                                                 track.TrackSequence, track.TrackSequence / (double)tracks.Count);
                                sector = inputFormat.ReadSectorTag(track.TrackStartSector, tag);
                                result = outputFormat.WriteSectorTag(sector, track.TrackStartSector, tag);
                                continue;
                        }

                        while(doneSectors < trackSectors)
                        {
                            uint sectorsToDo;
                            if(trackSectors - doneSectors >= (ulong)options.Count) sectorsToDo = (uint)options.Count;
                            else
                                sectorsToDo =
                                    (uint)(trackSectors - doneSectors);

                            DicConsole.Write("\rConverting tag {4} for sectors {0} to {1} in track {3} ({2:P2} done)",
                                             doneSectors  + track.TrackStartSector,
                                             doneSectors  + sectorsToDo + track.TrackStartSector,
                                             (doneSectors + track.TrackStartSector) / (double)inputFormat.Info.Sectors,
                                             track.TrackSequence, tag);

                            if(sectorsToDo == 1)
                            {
                                sector = inputFormat.ReadSectorTag(doneSectors           + track.TrackStartSector, tag);
                                result = outputFormat.WriteSectorTag(sector, doneSectors + track.TrackStartSector, tag);
                            }
                            else
                            {
                                sector = inputFormat.ReadSectorsTag(doneSectors + track.TrackStartSector, sectorsToDo,
                                                                    tag);
                                result = outputFormat.WriteSectorsTag(sector, doneSectors + track.TrackStartSector,
                                                                      sectorsToDo, tag);
                            }

                            if(!result)
                                if(options.Force)
                                    DicConsole.ErrorWriteLine("Error {0} writing sector {1}, continuing...",
                                                              outputFormat.ErrorMessage, doneSectors);
                                else
                                {
                                    DicConsole.ErrorWriteLine("Error {0} writing sector {1}, not continuing...",
                                                              outputFormat.ErrorMessage, doneSectors);
                                    return;
                                }

                            doneSectors += sectorsToDo;
                        }
                    }

                    switch(tag)
                    {
                        case SectorTagType.CdTrackFlags:
                        case SectorTagType.CdTrackIsrc:
                            DicConsole.Write("\rConverting tag {0} in track {1} ({2:P2} done).", tag, tracks.Count,
                                             1.0);
                            break;
                        default:
                            DicConsole.Write("\rConverting tag {4} for sectors {0} to {1} in track {3} ({2:P2} done)",
                                             inputFormat.Info.Sectors, inputFormat.Info.Sectors, 1.0, tracks.Count,
                                             tag);
                            break;
                    }

                    DicConsole.WriteLine();
                }
            }

            if(tracks == null)
            {
                DicConsole.WriteLine("Setting geometry to {0} cylinders, {1} heads and {2} sectors per track",
                                     inputFormat.Info.Cylinders, inputFormat.Info.Heads,
                                     inputFormat.Info.SectorsPerTrack);
                if(!outputFormat.SetGeometry(inputFormat.Info.Cylinders, inputFormat.Info.Heads,
                                             inputFormat.Info.SectorsPerTrack))
                    DicConsole.ErrorWriteLine("Error {0} setting geometry, image may be incorrect, continuing...",
                                              outputFormat.ErrorMessage);
            }

            DicConsole.WriteLine("Closing output image.");

            if(!outputFormat.Close())
                DicConsole.ErrorWriteLine("Error {0} closing output image... Contents are not correct.",
                                          outputFormat.ErrorMessage);

            DicConsole.WriteLine();
            DicConsole.WriteLine("Conversion done.");

            Core.Statistics.AddCommand("convert-image");
        }
    }
}