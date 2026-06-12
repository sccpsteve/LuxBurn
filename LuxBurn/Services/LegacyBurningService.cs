// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace LuxBurn.Services
{
    internal sealed class BurnProgress
    {
        public int ProgressPercent { get; set; }
        public int BufferPercent { get; set; }
        public int DeviceBufferPercent { get; set; }
        public string Status { get; set; }
    }

    internal sealed class BurnProcessException : InvalidOperationException
    {
        public BurnProcessException(string message, bool writeStarted, string processOutput)
            : base(message)
        {
            WriteStarted = writeStarted;
            ProcessOutput = processOutput ?? string.Empty;
        }

        public bool WriteStarted { get; private set; }
        public string ProcessOutput { get; private set; }
    }

    internal sealed class ManualMediaLoadRequiredException : InvalidOperationException
    {
        public ManualMediaLoadRequiredException(string message, string processOutput, Exception innerException)
            : base(message, innerException)
        {
            ProcessOutput = processOutput ?? string.Empty;
        }

        public string ProcessOutput { get; private set; }
    }

    internal sealed class DiscRecorderInfo
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string VendorId { get; set; }
        public string ProductId { get; set; }
        public string DriveLetter { get; set; }
        public string RegistryDeviceKey { get; set; }
        public string RegistryInstanceKey { get; set; }
        public int RegistryBusNumber { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;
        }
    }

    internal sealed class LegacyBurningService
    {
        private const uint STGM_READ = 0x00000000;
        private const int BytesPerSector = 2048;
        private const int MediaReadyTimeoutSeconds = 120;
        private const int MediaReadyPollMilliseconds = 2000;
        private const string ClientName = "LuxBurn";

        public bool IsImapi2Available
        {
            get
            {
                return Type.GetTypeFromProgID("IMAPI2.MsftDiscMaster2") != null &&
                       Type.GetTypeFromProgID("IMAPI2.MsftDiscRecorder2") != null &&
                       Type.GetTypeFromProgID("IMAPI2.MsftDiscFormat2Data") != null;
            }
        }

        public bool IsImapi2FileSystemAvailable
        {
            get { return Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage") != null; }
        }

        public bool IsReadcdAvailable
        {
            get { return !string.IsNullOrEmpty(FindReadcdPath()); }
        }

        public IList<DiscRecorderInfo> GetRecorders()
        {
            List<DiscRecorderInfo> recorders = new List<DiscRecorderInfo>();
            IList<string> cdRomDriveLetters = GetCdRomDriveLetters();
            int driveIndex = 0;

            Type masterType = Type.GetTypeFromProgID("IMAPI2.MsftDiscMaster2");
            Type recorderType = Type.GetTypeFromProgID("IMAPI2.MsftDiscRecorder2");
            if (masterType == null || recorderType == null)
                return recorders;

            object master = Activator.CreateInstance(masterType);
            if (!ReadBoolProperty(master, "IsSupportedEnvironment", false))
                return recorders;

            foreach (string recorderId in EnumerateRecorderIds(master))
            {
                object recorder = Activator.CreateInstance(recorderType);
                InvokeMethod(recorder, "InitializeDiscRecorder", recorderId);

                string vendor = ReadStringProperty(recorder, "VendorId");
                string product = ReadStringProperty(recorder, "ProductId");
                string registryDeviceKey = ExtractRegistryDeviceKey(recorderId);
                string registryInstanceKey = ExtractRegistryInstanceKey(recorderId);
                ScsiRegistryDevice registryLocation = GetRegistryDeviceLocation(registryDeviceKey, registryInstanceKey);
                if (string.IsNullOrEmpty(vendor))
                    vendor = ParseDeviceFieldFromRecorderId(recorderId, "ven_");
                if (string.IsNullOrEmpty(product))
                    product = ParseDeviceFieldFromRecorderId(recorderId, "prod_").Replace("_", " ");

                string volume = ReadStringProperty(recorder, "VolumeName");
                string driveLetter = ReadDriveLetter(recorder);
                if (string.IsNullOrEmpty(driveLetter) && driveIndex < cdRomDriveLetters.Count)
                    driveLetter = cdRomDriveLetters[driveIndex];
                driveIndex++;

                string name = (vendor + " " + product).Trim();
                if (!string.IsNullOrEmpty(driveLetter))
                    name = string.IsNullOrEmpty(name) ? driveLetter : name + " (" + driveLetter + ")";
                else if (!string.IsNullOrEmpty(volume))
                    name = string.IsNullOrEmpty(name) ? volume : name + " - " + volume;
                if (registryLocation != null)
                    name = string.IsNullOrEmpty(name) ? "Bus " + registryLocation.BusNumber : name + " - Bus " + registryLocation.BusNumber;

                recorders.Add(new DiscRecorderInfo
                {
                    Id = recorderId,
                    VendorId = vendor,
                    ProductId = product,
                    DriveLetter = driveLetter,
                    RegistryDeviceKey = registryDeviceKey,
                    RegistryInstanceKey = registryInstanceKey,
                    RegistryBusNumber = registryLocation == null ? -1 : registryLocation.BusNumber,
                    DisplayName = string.IsNullOrEmpty(name) ? recorderId : name
                });
            }

            recorders.Sort(delegate(DiscRecorderInfo left, DiscRecorderInfo right)
            {
                int leftBus = left.RegistryBusNumber < 0 ? int.MaxValue : left.RegistryBusNumber;
                int rightBus = right.RegistryBusNumber < 0 ? int.MaxValue : right.RegistryBusNumber;
                int comparison = leftBus.CompareTo(rightBus);
                if (comparison != 0)
                    return comparison;

                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            return recorders;
        }

        public void BuildIso(string sourceFolder, string outputPath, string volumeName)
        {
            if (string.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException("The source folder does not exist.");

            Type imageType = Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage");
            if (imageType == null)
                throw new InvalidOperationException("IMAPI2FS is not installed. Install Microsoft Image Mastering API v2 support to build ISO images.");

            object fileSystemImage = Activator.CreateInstance(imageType);
            SetProperty(fileSystemImage, "FileSystemsToCreate", 7); // ISO9660, Joliet, and UDF.
            SetProperty(fileSystemImage, "VolumeName", MakeVolumeName(volumeName));
            object root = GetProperty(fileSystemImage, "Root");
            InvokeMethod(root, "AddTree", sourceFolder, false);

            object result = InvokeMethod(fileSystemImage, "CreateResultImage");
            IStream imageStream = (IStream)GetProperty(result, "ImageStream");
            CopyComStreamToFile(imageStream, outputPath);
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone, string method, Action<string> log)
        {
            BurnImage(imagePath, recorderId, ejectWhenDone, method, log, null, CancellationToken.None);
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone, string method, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken)
        {
            BurnImage(imagePath, recorderId, ejectWhenDone, method, null, log, progress, cancellationToken);
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone, string method, string writeSpeed, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken)
        {
            BurnImage(imagePath, recorderId, ejectWhenDone, method, writeSpeed, log, progress, cancellationToken, null);
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone, string method, string writeSpeed, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken, Func<bool> confirmManualMediaLoad)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("The image file does not exist.", imagePath);

            if (ShouldUseCdrecord(method))
            {
                LaunchCdrecordBurner(imagePath, recorderId, ejectWhenDone, writeSpeed, log, progress, cancellationToken, confirmManualMediaLoad);
                return;
            }

            if (!IsAutomaticWriteSpeed(writeSpeed))
                Log(log, "Selected write speed is only applied by the cdrecord backend.");

            if (ShouldUseWindowsDiscImageBurner(method))
            {
                LaunchWindowsDiscImageBurner(imagePath, recorderId, log);
                return;
            }

            if (string.IsNullOrEmpty(method) || string.Equals(method, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "No supported burn backend is available. LuxBurn needs the bundled cdrecord backend, or Windows Disc Image Burner as a fallback.");
            }

            if (!IsImapi2Available)
                throw new InvalidOperationException("IMAPI2 is not installed. Install Microsoft Image Mastering API v2 support or use the bundled cdrtools backend.");

            string id = recorderId;
            id = ResolveRecorderId(id);

            Type recorderType = Type.GetTypeFromProgID("IMAPI2.MsftDiscRecorder2");
            Type formatType = Type.GetTypeFromProgID("IMAPI2.MsftDiscFormat2Data");
            object recorder = Activator.CreateInstance(recorderType);
            InvokeMethod(recorder, "InitializeDiscRecorder", id);

            object format = Activator.CreateInstance(formatType);
            bool exclusive = false;

            try
            {
                SetProperty(format, "Recorder", recorder);
                SetProperty(format, "ClientName", ClientName);
                SetProperty(format, "ForceMediaToBeClosed", true);

                FileInfo imageFile = new FileInfo(imagePath);
                long imageSectors = (imageFile.Length + BytesPerSector - 1) / BytesPerSector;
                Log(log, string.Format("Image size: {0:N0} bytes ({1:N0} sectors).", imageFile.Length, imageSectors));
                LogRecorderInfo(log, recorder);

                PreflightBurn(format, recorder, imageSectors, log);

                IStream imageStream;
                SHCreateStreamOnFileW(imagePath, STGM_READ, out imageStream);

                try
                {
                    Log(log, "Acquiring exclusive access to the recorder.");
                    InvokeMethod(recorder, "AcquireExclusiveAccess", true, ClientName);
                    exclusive = true;
                    SetProperty(format, "Recorder", recorder);

                    Log(log, "Starting write operation.");
                    InvokeMethod(format, "Write", imageStream);
                    Log(log, "Drive reported write operation complete.");
                }
                catch (COMException ex)
                {
                    string mediaSnapshot = GetMediaSnapshot(format);
                    throw CreateBurnException("The drive rejected the write operation.", ex, mediaSnapshot);
                }

                if (ejectWhenDone)
                {
                    Log(log, "Ejecting media.");
                    InvokeMethod(recorder, "EjectMedia");
                }
            }
            finally
            {
                if (exclusive)
                {
                    try
                    {
                        InvokeMethod(recorder, "ReleaseExclusiveAccess");
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone)
        {
            BurnImage(imagePath, recorderId, ejectWhenDone, "Auto", null, null, CancellationToken.None);
        }

        public void BurnImage(string imagePath, string recorderId, bool ejectWhenDone, Action<string> log)
        {
            BurnImage(imagePath, recorderId, ejectWhenDone, "Auto", log, null, CancellationToken.None);
        }

        public bool IsWindowsDiscImageBurnerAvailable
        {
            get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "isoburn.exe")); }
        }

        public bool IsCdrecordAvailable
        {
            get { return !string.IsNullOrEmpty(FindCdrecordPath()); }
        }

        public bool WillUseWindowsDiscImageBurner(string method)
        {
            return ShouldUseWindowsDiscImageBurner(method);
        }

        private static bool ShouldUseCdrecord(string method)
        {
            if (string.IsNullOrEmpty(method))
                method = "Auto";

            if (string.Equals(method, "CDRTFE cdrecord", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "cdrecord", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(method, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            return !string.IsNullOrEmpty(FindCdrecordPath());
        }

        private static bool ShouldUseWindowsDiscImageBurner(string method)
        {
            if (string.IsNullOrEmpty(method))
                method = "Auto";

            if (string.Equals(method, "Windows Disc Image Burner", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(method, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            return string.IsNullOrEmpty(FindCdrecordPath()) &&
                   File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "isoburn.exe"));
        }

        private static bool IsAutomaticWriteSpeed(string writeSpeed)
        {
            if (string.IsNullOrEmpty(writeSpeed))
                return true;

            return string.Equals(writeSpeed, "Auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(writeSpeed, "Max", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(writeSpeed, "MAX", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(writeSpeed, "AWS", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatCdrecordSpeedArgument(string writeSpeed)
        {
            if (IsAutomaticWriteSpeed(writeSpeed))
                return string.Empty;

            string digits = Regex.Replace(writeSpeed, "[^0-9]", string.Empty);
            if (digits.Length == 0)
                return string.Empty;

            return "speed=" + digits;
        }

        private static string FormatCdrecordWriteModeArgument(CdrecordMediaInfo media)
        {
            string type = (media == null ? string.Empty : media.MediaType) ?? string.Empty;
            if (type.IndexOf("DVD", StringComparison.OrdinalIgnoreCase) >= 0)
                return "-sao";

            if (type.IndexOf("BD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("Blu", StringComparison.OrdinalIgnoreCase) >= 0)
                return string.Empty;

            return "-tao";
        }

        public void EraseDisc(string recorderId, bool fullErase, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken)
        {
            string cdrecordPath = FindCdrecordPath();
            if (string.IsNullOrEmpty(cdrecordPath))
                throw new InvalidOperationException("cdrecord.exe was not found. Put the cdrtools folder next to LuxBurn or choose another erase method.");

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string device = FindCdrecordDevice(cdrecordPath, recorder, log);
            if (string.IsNullOrEmpty(device))
                throw new InvalidOperationException("LuxBurn could not determine a cdrecord SPTI device address.");

            CdrecordMediaInfo media = ProbeCdrecordMedia(cdrecordPath, device, log);
            if (!media.IsErasable)
                throw new InvalidOperationException("The inserted disc is not erasable. CD-R and finalized write-once discs cannot be erased.");

            ReportProgress(progress, -1, -1, -1, "Erasing disc");
            string args = "gracetime=5 dev=" + device + " blank=" + (fullErase ? "all" : "fast") + " -v";
            Log(log, "Launching cdrecord erase: cdrecord.exe " + args);
            RunProcessAndLog(cdrecordPath, args, log, progress, cancellationToken);
            ReportProgress(progress, 100, -1, -1, "Erase complete");
            Log(log, "Erase completed.");
        }

        public void CopyDiscToImage(string recorderId, string outputPath, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new InvalidOperationException("Choose an output image path first.");

            string readcdPath = FindReadcdPath();
            if (string.IsNullOrEmpty(readcdPath))
                throw new InvalidOperationException("readcd.exe was not found. Put the cdrtools folder next to LuxBurn.");

            string cdrecordPath = FindCdrecordPath();
            if (string.IsNullOrEmpty(cdrecordPath))
                throw new InvalidOperationException("cdrecord.exe was not found. LuxBurn needs it to map the selected drive safely.");

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string device = FindCdrecordDevice(cdrecordPath, recorder, log);
            if (string.IsNullOrEmpty(device))
                throw new InvalidOperationException("LuxBurn could not determine a readcd SPTI device address.");

            CdrecordMediaInfo media = ProbeCdrecordMedia(cdrecordPath, device, log);
            if (media == null || !media.HasMedia)
                throw new InvalidOperationException("No readable disc was detected in the selected drive.");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            string args = "dev=" + device + " f=" + QuoteArgument(outputPath) + " retries=16 -v";
            Log(log, "Launching readcd backend: readcd.exe " + args);
            ReportProgress(progress, 0, -1, -1, "Starting copy");
            RunProcessAndLog(readcdPath, args, log, progress, cancellationToken, media.CapacitySectors);

            FileInfo image = new FileInfo(outputPath);
            if (!image.Exists || image.Length == 0)
                throw new InvalidOperationException("readcd completed, but no image data was written.");

            ReportProgress(progress, 100, -1, -1, "Copy complete");
            Log(log, string.Format("Copied image size: {0:N0} bytes.", image.Length));
        }

        public string CaptureDriveCommand(string recorderId, string command, Action<string> log)
        {
            string cdrecordPath = FindCdrecordPath();
            if (string.IsNullOrEmpty(cdrecordPath))
                throw new InvalidOperationException("cdrecord.exe was not found. Put the cdrtools folder next to LuxBurn.");

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string device = FindCdrecordDevice(cdrecordPath, recorder, log);
            if (string.IsNullOrEmpty(device))
                throw new InvalidOperationException("LuxBurn could not determine a cdrecord SPTI device address.");

            string args = "dev=" + device + " " + command;
            Log(log, "Running cdrecord command: cdrecord.exe " + args);
            return CaptureProcessOutput(cdrecordPath, args);
        }

        public void RunDriveCommand(string recorderId, string command, Action<string> log, CancellationToken cancellationToken)
        {
            string cdrecordPath = FindCdrecordPath();
            if (string.IsNullOrEmpty(cdrecordPath))
                throw new InvalidOperationException("cdrecord.exe was not found. Put the cdrtools folder next to LuxBurn.");

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string device = FindCdrecordDevice(cdrecordPath, recorder, log);
            if (string.IsNullOrEmpty(device))
                throw new InvalidOperationException("LuxBurn could not determine a cdrecord SPTI device address.");

            string args = "dev=" + device + " " + command;
            Log(log, "Running cdrecord command: cdrecord.exe " + args);
            RunProcessAndLog(cdrecordPath, args, log, null, cancellationToken);
        }

        private void LaunchCdrecordBurner(string imagePath, string recorderId, bool ejectWhenDone, string writeSpeed, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken, Func<bool> confirmManualMediaLoad)
        {
            string cdrecordPath = FindCdrecordPath();
            if (string.IsNullOrEmpty(cdrecordPath))
                throw new InvalidOperationException("cdrecord.exe was not found. Put the cdrtools folder next to LuxBurn or choose another burn method.");

            if (!string.Equals(Path.GetExtension(imagePath), ".iso", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The cdrecord backend currently writes ISO images. Use Windows Disc Image Burner or IMAPI for this file type.");

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string device = FindCdrecordDevice(cdrecordPath, recorder, log);
            if (string.IsNullOrEmpty(device))
                throw new InvalidOperationException("LuxBurn could not determine a cdrecord SPTI device address.");

            CdrecordMediaInfo media = ProbeCdrecordMediaWithManualLoad(cdrecordPath, device, log, confirmManualMediaLoad, cancellationToken);
            FileInfo imageFile = new FileInfo(imagePath);
            long imageSectors = (imageFile.Length + BytesPerSector - 1) / BytesPerSector;
            Log(log, string.Format("Image size: {0:N0} bytes ({1:N0} sectors).", imageFile.Length, imageSectors));
            ValidateCdrecordMediaForBurn(media, imageSectors);

            string writeModeArgument = FormatCdrecordWriteModeArgument(media);
            string args =
                "dev=" + device +
                " fs=16m -v -data";

            if (!string.IsNullOrEmpty(writeModeArgument))
                args += " " + writeModeArgument;

            string speedArgument = FormatCdrecordSpeedArgument(writeSpeed);
            if (!string.IsNullOrEmpty(speedArgument))
                args += " " + speedArgument;

            args += " " + QuoteArgument(imagePath);

            Log(log, "Launching cdrecord backend: cdrecord.exe " + args);
            if (!string.IsNullOrEmpty(writeModeArgument))
                Log(log, "cdrecord write mode: " + writeModeArgument + ".");
            ReportProgress(progress, 0, 0, 0, "Starting write");
            RunBurnProcessWithRetries(cdrecordPath, args, log, progress, cancellationToken, confirmManualMediaLoad);
            ReportProgress(progress, 100, 100, 100, "Burn complete");
            Log(log, "cdrecord reported write operation complete.");

            if (ejectWhenDone)
            {
                try
                {
                    Log(log, "Ejecting media.");
                    RunProcessAndLog(cdrecordPath, "dev=" + device + " -eject", log, null, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log(log, "Eject failed: " + ex.Message);
                }
            }
        }

        private sealed class CdrecordMediaInfo
        {
            public string MediaType;
            public string DiskStatus;
            public long CapacitySectors;
            public bool IsErasable;
            public bool HasMedia;
            public bool ManualLoadRequired;
        }

        private static CdrecordMediaInfo ProbeCdrecordMedia(string cdrecordPath, string device, Action<string> log)
        {
            string output = CaptureProcessOutput(cdrecordPath, "dev=" + device + " -minfo -v");
            CdrecordMediaInfo media = new CdrecordMediaInfo();
            media.MediaType = ReadCdrecordValue(output, "Mounted media type");
            if (string.IsNullOrEmpty(media.MediaType))
                media.MediaType = ReadCdrecordValue(output, "Current");
            media.DiskStatus = ReadCdrecordValue(output, "disk status");
            media.CapacitySectors = ReadCdrecordCapacitySectors(output);
            media.IsErasable = output.IndexOf("Disk Is erasable", StringComparison.OrdinalIgnoreCase) >= 0 &&
                               output.IndexOf("Disk Is not erasable", StringComparison.OrdinalIgnoreCase) < 0;
            media.ManualLoadRequired = IsManualMediaLoadRequired(output);
            media.HasMedia =
                output.IndexOf("No disk", StringComparison.OrdinalIgnoreCase) < 0 &&
                !media.ManualLoadRequired &&
                output.IndexOf("No disk / Wrong disk", StringComparison.OrdinalIgnoreCase) < 0 &&
                !string.IsNullOrEmpty(media.MediaType);

            Log(log, "Media type: " + (string.IsNullOrEmpty(media.MediaType) ? "unknown" : media.MediaType) + ".");
            Log(log, "Disc status: " + (string.IsNullOrEmpty(media.DiskStatus) ? "unknown" : media.DiskStatus) + ".");
            Log(log, "Disc capacity: " + (media.CapacitySectors > 0 ? media.CapacitySectors.ToString("N0") + " sectors" : "unknown") + ".");
            Log(log, "Erasable: " + YesNo(media.IsErasable) + ".");
            return media;
        }

        private static CdrecordMediaInfo ProbeCdrecordMediaWithManualLoad(string cdrecordPath, string device, Action<string> log, Func<bool> confirmManualMediaLoad, CancellationToken cancellationToken)
        {
            const int MaxAttempts = 10;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CdrecordMediaInfo media = ProbeCdrecordMedia(cdrecordPath, device, log);
                if (!media.ManualLoadRequired || confirmManualMediaLoad == null)
                    return media;

                Log(log, "The drive could not load media automatically. Waiting for manual tray closure.");
                if (!confirmManualMediaLoad())
                    throw new OperationCanceledException("Burn cancelled while waiting for the tray to be closed by hand.");

                Thread.Sleep(1500);
            }

            throw new InvalidOperationException("The selected drive still cannot see the disc after several manual load attempts.");
        }

        private static string ReadCdrecordValue(string output, string name)
        {
            Match match = Regex.Match(output, @"^\s*" + Regex.Escape(name) + @":\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static long ReadCdrecordCapacitySectors(string output)
        {
            Match atip = Regex.Match(output, @"ATIP start of lead out:\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (atip.Success)
            {
                long sectors = Convert.ToInt64(atip.Groups[1].Value);
                if (sectors > 0)
                    return sectors;
            }

            Match leadout = Regex.Match(output, @"last start of lead out:\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (leadout.Success)
            {
                long sectors = Convert.ToInt64(leadout.Groups[1].Value);
                if (sectors > 0)
                    return sectors;
            }

            return -1;
        }

        private static void ValidateCdrecordMediaForBurn(CdrecordMediaInfo media, long imageSectors)
        {
            if (media == null || !media.HasMedia)
                throw new InvalidOperationException("No writable disc was detected in the selected drive.");

            string status = (media.DiskStatus ?? string.Empty).Trim().ToLowerInvariant();
            if (status.Length == 0)
                throw new InvalidOperationException("LuxBurn detected a disc, but cdrecord did not report whether it is blank or appendable.");

            if (status == "empty")
            {
                if (media.CapacitySectors > 0 && imageSectors > media.CapacitySectors)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "The image is too large for the inserted disc. Image requires {0:N0} sectors; disc reports {1:N0} sectors.",
                            imageSectors,
                            media.CapacitySectors));
                }

                return;
            }

            if (media.IsErasable)
                throw new InvalidOperationException("The inserted rewritable disc already contains data. Erase it before burning.");

            throw new InvalidOperationException("The inserted disc already contains data or is finalized. It cannot be burned again.");
        }

        private static string FindCdrecordDevice(string cdrecordPath, DiscRecorderInfo recorder, Action<string> log)
        {
            string output = CaptureProcessOutput(cdrecordPath, "-scanbus");
            MatchCollection matches = Regex.Matches(
                output,
                @"^\s*(\d+,\d+,\d+)\s+\d+\)\s+'([^']*)'\s+'([^']*)'.*Removable CD-ROM",
                RegexOptions.Multiline);

            if (matches.Count == 0)
            {
                Log(log, "cdrecord did not report any removable CD-ROM devices.");
                return string.Empty;
            }

            Match fallback = matches[0];
            if (recorder != null)
            {
                string wantedVendor = NormalizeDeviceText(recorder.VendorId);
                string wantedProduct = NormalizeDeviceText(recorder.ProductId);
                List<Match> productMatches = new List<Match>();

                for (int i = 0; i < matches.Count; i++)
                {
                    string vendor = NormalizeDeviceText(matches[i].Groups[2].Value);
                    string product = NormalizeDeviceText(matches[i].Groups[3].Value);
                    if (vendor == wantedVendor && product == wantedProduct)
                        productMatches.Add(matches[i]);
                }

                if (productMatches.Count == 1)
                {
                    string matched = "SPTI:" + productMatches[0].Groups[1].Value;
                    Log(log, "Matched cdrecord device: " + matched + ".");
                    return matched;
                }

                if (productMatches.Count > 1)
                {
                    string mapped = MapIdenticalCdrecordDevice(recorder, productMatches, log);
                    if (!string.IsNullOrEmpty(mapped))
                        return mapped;

                    throw new InvalidOperationException(
                        "LuxBurn found more than one identical cdrecord drive and could not map the selected drive safely. " +
                        "No write was started.");
                }
            }

            string device = "SPTI:" + fallback.Groups[1].Value;
            Log(log, "Using first cdrecord optical device: " + device + ".");
            return device;
        }

        private static string MapIdenticalCdrecordDevice(DiscRecorderInfo recorder, IList<Match> productMatches, Action<string> log)
        {
            if (recorder == null ||
                string.IsNullOrEmpty(recorder.RegistryDeviceKey) ||
                string.IsNullOrEmpty(recorder.RegistryInstanceKey))
            {
                return string.Empty;
            }

            List<ScsiRegistryDevice> registryDevices = GetRegistryCdRomDevices(recorder.RegistryDeviceKey);
            int selectedIndex = -1;
            for (int i = 0; i < registryDevices.Count; i++)
            {
                if (string.Equals(registryDevices[i].InstanceKey, recorder.RegistryInstanceKey, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0 || selectedIndex >= productMatches.Count)
                return string.Empty;

            string mapped = "SPTI:" + productMatches[selectedIndex].Groups[1].Value;
            Log(
                log,
                "Mapped selected recorder " + recorder.RegistryInstanceKey +
                " to cdrecord device " + mapped + ".");
            return mapped;
        }

        private void LaunchWindowsDiscImageBurner(string imagePath, string recorderId, Action<string> log)
        {
            string isoburnPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "isoburn.exe");
            if (!File.Exists(isoburnPath))
                throw new InvalidOperationException("Windows Disc Image Burner is not available on this system. Restore the bundled cdrecord backend or install another burn backend.");

            DiscRecorderInfo recorder = FindRecorder(recorderId);
            string args = "\"" + imagePath + "\"";

            if (recorder != null && !string.IsNullOrEmpty(recorder.DriveLetter))
                Log(log, "Selected drive: " + recorder.DriveLetter + ". Windows Disc Image Burner will confirm the target before writing.");

            Log(log, "Launching Windows Disc Image Burner: isoburn.exe " + args);
            ProcessStartInfo startInfo = new ProcessStartInfo(isoburnPath, args);
            startInfo.UseShellExecute = false;
            Process process = Process.Start(startInfo);

            if (process == null)
                throw new InvalidOperationException("Could not start Windows Disc Image Burner.");

            Log(log, "Windows Disc Image Burner is now handling the burn. Follow its window for completion status.");
        }

        private static void RunBurnProcessWithRetries(string fileName, string arguments, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken, Func<bool> confirmManualMediaLoad)
        {
            const int MaxAttempts = 10;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (attempt > 1)
                    Log(log, "Retrying burn startup, attempt " + attempt + " of " + MaxAttempts + ".");

                try
                {
                    RunProcessAndLog(fileName, arguments, log, progress, cancellationToken);
                    return;
                }
                catch (BurnProcessException ex)
                {
                    if (!ex.WriteStarted && IsManualMediaLoadRequired(ex.ProcessOutput))
                    {
                        if (confirmManualMediaLoad != null)
                        {
                            Log(log, "The drive could not load media automatically. Waiting for manual tray closure.");
                            if (confirmManualMediaLoad())
                            {
                                Thread.Sleep(1500);
                                continue;
                            }

                            throw new OperationCanceledException("Burn cancelled while waiting for the tray to be closed by hand.");
                        }

                        throw new ManualMediaLoadRequiredException(
                            "The selected drive cannot load the tray automatically. Push the tray fully closed by hand, wait for the disc to settle, then retry.",
                            ex.ProcessOutput,
                            ex);
                    }

                    if (ex.WriteStarted || attempt == MaxAttempts)
                        throw;

                    Thread.Sleep(1000);
                }
            }
        }

        private static void RunProcessAndLog(string fileName, string arguments, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken)
        {
            RunProcessAndLog(fileName, arguments, log, progress, cancellationToken, -1);
        }

        private static void RunProcessAndLog(string fileName, string arguments, Action<string> log, Action<BurnProgress> progress, CancellationToken cancellationToken, long readTotalSectors)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = Path.GetDirectoryName(fileName);
            AddCdrtoolsRuntimePath(startInfo, fileName);

            using (Process process = new Process())
            {
                bool writeStarted = false;
                object outputLock = new object();
                StringBuilder processOutput = new StringBuilder();
                process.StartInfo = startInfo;
                ProcessProgressState progressState = new ProcessProgressState();
                progressState.ReadTotalSectors = readTotalSectors;
                Action<string> captureOutput = delegate(string line)
                {
                    lock (outputLock)
                    {
                        if (processOutput.Length < 12000)
                            processOutput.AppendLine(line);
                    }
                };

                if (!process.Start())
                    throw new InvalidOperationException("Could not start " + Path.GetFileName(fileName) + ".");

                Thread outputThread = new Thread(new ThreadStart(delegate
                {
                    PumpProcessOutput(process.StandardOutput, log, progress, progressState, delegate { writeStarted = true; }, captureOutput);
                }));
                Thread errorThread = new Thread(new ThreadStart(delegate
                {
                    PumpProcessOutput(process.StandardError, log, progress, progressState, delegate { writeStarted = true; }, captureOutput);
                }));
                outputThread.IsBackground = true;
                errorThread.IsBackground = true;
                outputThread.Start();
                errorThread.Start();

                while (!process.WaitForExit(250))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        throw new OperationCanceledException("Operation cancelled.");
                    }
                }

                outputThread.Join(2000);
                errorThread.Join(2000);

                if (process.ExitCode != 0)
                {
                    string output;
                    lock (outputLock)
                    {
                        output = processOutput.ToString().Trim();
                    }

                    throw new BurnProcessException(BuildProcessFailureMessage(Path.GetFileName(fileName), process.ExitCode, output), writeStarted, output);
                }
            }
        }

        private static string BuildProcessFailureMessage(string toolName, int exitCode, string processOutput)
        {
            string message = toolName + " failed with exit code " + exitCode + ".";
            if (string.IsNullOrEmpty(processOutput))
                return message;

            return message + Environment.NewLine + Environment.NewLine + "Last tool output:" + Environment.NewLine + TrimForMessage(processOutput, 1600);
        }

        private static bool IsManualMediaLoadRequired(string processOutput)
        {
            if (string.IsNullOrEmpty(processOutput))
                return false;

            return processOutput.IndexOf("Cannot load media", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   processOutput.IndexOf("Try to load media by hand", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string TrimForMessage(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return "..." + value.Substring(value.Length - maxLength);
        }

        private sealed class ProcessProgressState
        {
            public long ReadTotalSectors;
            public int LastLoggedProgressPercent = -1;
        }

        private static void PumpProcessOutput(StreamReader reader, Action<string> log, Action<BurnProgress> progress, ProcessProgressState state, Action writeStarted, Action<string> captureOutput)
        {
            StringBuilder segment = new StringBuilder();
            int value;
            while ((value = reader.Read()) >= 0)
            {
                char ch = (char)value;
                if (ch == '\r' || ch == '\n')
                    FlushProcessOutputSegment(segment, log, progress, state, writeStarted, captureOutput);
                else
                    segment.Append(ch);
            }

            FlushProcessOutputSegment(segment, log, progress, state, writeStarted, captureOutput);
        }

        private static void FlushProcessOutputSegment(StringBuilder segment, Action<string> log, Action<BurnProgress> progress, ProcessProgressState state, Action writeStarted, Action<string> captureOutput)
        {
            if (segment == null || segment.Length == 0)
                return;

            string line = segment.ToString().Trim();
            segment.Length = 0;
            if (line.Length == 0)
                return;

            if (captureOutput != null)
                captureOutput(line);

            if (IsCdrecordWriteStartedLine(line) && writeStarted != null)
                writeStarted();

            int percent = -1;
            bool progressLine = ReportProgressFromCdrecordLine(line, progress, out percent);
            progressLine = ReportProgressFromReadcdLine(line, progress, state, out percent) || progressLine;

            if (progressLine)
            {
                if (percent >= 0 && (state == null || Math.Abs(percent - state.LastLoggedProgressPercent) >= 10 || percent == 100))
                {
                    if (state != null)
                        state.LastLoggedProgressPercent = percent;
                    Log(log, "Progress: " + percent + "%");
                }
                return;
            }

            Log(log, line);
        }

        private static bool IsCdrecordWriteStartedLine(string line)
        {
            return line.IndexOf("Starting new track", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Track 01:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Writing  time", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Fixating", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ReportProgressFromCdrecordLine(string line, Action<BurnProgress> progress, out int percentValue)
        {
            percentValue = -1;
            if (progress == null || string.IsNullOrEmpty(line))
                return false;

            Match track = Regex.Match(
                line,
                @"Track\s+\d+:\s+(\d+)\s+of\s+(\d+)\s+MB.*?fifo\s+(\d+)%.*?\[buf\s+(\d+)%\]",
                RegexOptions.IgnoreCase);
            if (track.Success)
            {
                int current = Convert.ToInt32(track.Groups[1].Value);
                int total = Math.Max(1, Convert.ToInt32(track.Groups[2].Value));
                int percent = Math.Max(0, Math.Min(100, (int)Math.Round((current * 100.0) / total)));
                int fifo = Convert.ToInt32(track.Groups[3].Value);
                int device = Convert.ToInt32(track.Groups[4].Value);
                ReportProgress(progress, percent, fifo, device, "Writing");
                percentValue = percent;
                return true;
            }

            Match percentDone = Regex.Match(line, @"(\d+)%\s+done", RegexOptions.IgnoreCase);
            if (percentDone.Success)
            {
                percentValue = Convert.ToInt32(percentDone.Groups[1].Value);
                ReportProgress(progress, percentValue, -1, -1, "Writing");
                return true;
            }

            if (line.IndexOf("Fixating", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("lead-out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReportProgress(progress, -1, -1, -1, "Finalizing disc");
                return true;
            }

            if (line.IndexOf("blanking", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Blanking", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReportProgress(progress, -1, -1, -1, "Erasing disc");
                return true;
            }

            return false;
        }

        private static bool ReportProgressFromReadcdLine(string line, Action<BurnProgress> progress, ProcessProgressState state, out int percentValue)
        {
            percentValue = -1;
            if (progress == null || string.IsNullOrEmpty(line))
                return false;

            Match percent = Regex.Match(line, @"(\d{1,3})\s*%\s*(?:done|read|complete)?", RegexOptions.IgnoreCase);
            if (percent.Success)
            {
                percentValue = Math.Max(0, Math.Min(100, Convert.ToInt32(percent.Groups[1].Value)));
                ReportProgress(progress, percentValue, -1, -1, "Copying disc");
                return true;
            }

            Match sectors = Regex.Match(line, @"(\d+)\s*(?:of|/)\s*(\d+)\s*(?:sectors|blocks)?", RegexOptions.IgnoreCase);
            if (sectors.Success)
            {
                long current = Convert.ToInt64(sectors.Groups[1].Value);
                long total = Math.Max(1, Convert.ToInt64(sectors.Groups[2].Value));
                percentValue = Math.Max(0, Math.Min(100, (int)Math.Round((current * 100.0) / total)));
                ReportProgress(progress, percentValue, -1, -1, "Copying disc");
                return true;
            }

            Match capacity = Regex.Match(line, @"Capacity:\s*(\d+)\s+Blocks", RegexOptions.IgnoreCase);
            if (capacity.Success && state != null)
            {
                state.ReadTotalSectors = Convert.ToInt64(capacity.Groups[1].Value);
                ReportProgress(progress, -1, -1, -1, "Copying disc");
                return true;
            }

            Match address = Regex.Match(line, @"addr:\s*(\d+)", RegexOptions.IgnoreCase);
            if (address.Success && state != null && state.ReadTotalSectors > 0)
            {
                long current = Convert.ToInt64(address.Groups[1].Value);
                percentValue = Math.Max(0, Math.Min(100, (int)Math.Round((current * 100.0) / state.ReadTotalSectors)));
                ReportProgress(progress, percentValue, -1, -1, "Copying disc");
                return true;
            }

            if (line.IndexOf("capacity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("reading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("addr", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReportProgress(progress, -1, -1, -1, "Copying disc");
                return true;
            }

            return false;
        }

        private static void ReportProgress(Action<BurnProgress> progress, int overall, int buffer, int deviceBuffer, string status)
        {
            if (progress == null)
                return;

            progress(new BurnProgress
            {
                ProgressPercent = overall < 0 ? -1 : Math.Max(0, Math.Min(100, overall)),
                BufferPercent = buffer < 0 ? -1 : Math.Max(0, Math.Min(100, buffer)),
                DeviceBufferPercent = deviceBuffer < 0 ? -1 : Math.Max(0, Math.Min(100, deviceBuffer)),
                Status = status
            });
        }

        private static string CaptureProcessOutput(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = Path.GetDirectoryName(fileName);
            AddCdrtoolsRuntimePath(startInfo, fileName);

            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                DataReceivedEventHandler handler = delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };

                process.OutputDataReceived += handler;
                process.ErrorDataReceived += handler;

                if (!process.Start())
                    return string.Empty;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }

            return output.ToString();
        }

        private static string NormalizeDeviceText(string value)
        {
            string source = (value ?? string.Empty).Trim().ToUpperInvariant();
            StringBuilder normalized = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (char.IsLetterOrDigit(source[i]))
                    normalized.Append(source[i]);
            }

            return normalized.ToString();
        }

        private static void AddCdrtoolsRuntimePath(ProcessStartInfo startInfo, string toolPath)
        {
            try
            {
                DirectoryInfo toolDirectory = Directory.GetParent(Path.GetDirectoryName(toolPath));
                if (toolDirectory == null)
                    return;

                string cygwinDirectory = Path.Combine(toolDirectory.FullName, "cygwin");
                if (!Directory.Exists(cygwinDirectory))
                    return;

                string currentPath = startInfo.EnvironmentVariables["PATH"];
                startInfo.EnvironmentVariables["PATH"] = cygwinDirectory + Path.PathSeparator + currentPath;
            }
            catch
            {
            }
        }

        private string ResolveRecorderId(string recorderId)
        {
            if (!string.IsNullOrEmpty(recorderId))
                return recorderId;

            IList<DiscRecorderInfo> recorders = GetRecorders();
            if (recorders.Count == 0)
                throw new InvalidOperationException("No writable optical drive was found.");

            return recorders[0].Id;
        }

        private DiscRecorderInfo FindRecorder(string recorderId)
        {
            IList<DiscRecorderInfo> recorders = GetRecorders();
            if (recorders.Count == 0)
                return null;

            if (string.IsNullOrEmpty(recorderId))
                return recorders[0];

            for (int i = 0; i < recorders.Count; i++)
            {
                if (string.Equals(recorders[i].Id, recorderId, StringComparison.OrdinalIgnoreCase))
                    return recorders[i];
            }

            return recorders[0];
        }

        private static void PreflightBurn(object format, object recorder, long imageSectors, Action<string> log)
        {
            bool recorderSupported = InvokeBool(format, "IsRecorderSupported", recorder);
            Log(log, "Recorder supported by IMAPI2 data writer: " + YesNo(recorderSupported));
            if (!recorderSupported)
                throw new InvalidOperationException("This drive is not supported by the IMAPI2 data writer.");

            bool mediaSupported = WaitForSupportedMedia(format, recorder, log);
            if (!mediaSupported)
                throw new InvalidOperationException(
                    "The inserted media is not supported for this write operation." + Environment.NewLine +
                    GetMediaSnapshot(format));

            int mediaTypeCode = ReadIntProperty(format, "CurrentPhysicalMediaType", -1);
            string mediaType = MediaTypeName(mediaTypeCode);
            long freeSectors = ReadLongProperty(format, "FreeSectorsOnMedia", -1);
            long totalSectors = ReadLongProperty(format, "TotalSectorsOnMedia", -1);
            bool heuristicallyBlank = ReadBoolProperty(format, "MediaHeuristicallyBlank", false);
            bool physicallyBlank = ReadBoolProperty(format, "MediaPhysicallyBlank", false);

            Log(log, "Current media: " + mediaType);
            Log(log, string.Format("Media blank: heuristic={0}, physical={1}.", YesNo(heuristicallyBlank), YesNo(physicallyBlank)));
            Log(log, string.Format("Media capacity: free={0:N0} sectors, total={1:N0} sectors.", freeSectors, totalSectors));

            if ((mediaTypeCode == 2 || mediaTypeCode == 6 || mediaTypeCode == 8 || mediaTypeCode == 9 || mediaTypeCode == 11 || mediaTypeCode == 18) &&
                (freeSectors <= 0 || totalSectors <= 0))
            {
                Log(log, "Warning: write-once media capacity was not reported. Continuing because the IMAPI compatibility writer was explicitly selected.");
            }

            if (freeSectors > 0 && imageSectors > freeSectors)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "The image is too large for the inserted media. Image requires {0:N0} sectors; media reports {1:N0} free sectors.",
                        imageSectors,
                        freeSectors));
            }
        }

        private static bool WaitForSupportedMedia(object format, object recorder, Action<string> log)
        {
            DateTime deadline = DateTime.Now.AddSeconds(MediaReadyTimeoutSeconds);
            int attempt = 1;
            string lastSnapshot = string.Empty;

            while (DateTime.Now <= deadline)
            {
                bool supported = InvokeBool(format, "IsCurrentMediaSupported", recorder);
                lastSnapshot = GetMediaSnapshot(format);

                if (supported)
                {
                    Log(log, "Inserted media is supported for this operation.");
                    return true;
                }

                if (attempt == 1 || attempt % 5 == 0)
                    Log(log, "Waiting for supported media. " + lastSnapshot);

                attempt++;
                Thread.Sleep(MediaReadyPollMilliseconds);
            }

            Log(log, "Timed out waiting for supported media. " + lastSnapshot);
            return false;
        }

        private static Exception CreateBurnException(string message, COMException ex, string mediaSnapshot)
        {
            string detail =
                message + Environment.NewLine +
                "Original IMAPI error: " + ex.Message + Environment.NewLine +
                "HRESULT: 0x" + ex.ErrorCode.ToString("X8") + Environment.NewLine +
                mediaSnapshot;

            return new InvalidOperationException(detail, ex);
        }

        private static string GetMediaSnapshot(object format)
        {
            return string.Format(
                "Media={0}; Status={1}; FreeSectors={2:N0}; TotalSectors={3:N0}; Blank={4}/{5}",
                MediaTypeName(ReadIntProperty(format, "CurrentPhysicalMediaType", -1)),
                MediaStatusName(ReadIntProperty(format, "CurrentMediaStatus", -1)),
                ReadLongProperty(format, "FreeSectorsOnMedia", -1),
                ReadLongProperty(format, "TotalSectorsOnMedia", -1),
                YesNo(ReadBoolProperty(format, "MediaHeuristicallyBlank", false)),
                YesNo(ReadBoolProperty(format, "MediaPhysicallyBlank", false)));
        }

        private static void LogRecorderInfo(Action<string> log, object recorder)
        {
            Log(log, "Recorder vendor: " + ReadStringProperty(recorder, "VendorId"));
            Log(log, "Recorder product: " + ReadStringProperty(recorder, "ProductId"));
            Log(log, "Recorder revision: " + ReadStringProperty(recorder, "ProductRevision"));
        }

        private static object GetProperty(object instance, string propertyName)
        {
            return instance.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                instance,
                null);
        }

        private static void SetProperty(object instance, string propertyName, object value)
        {
            instance.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.SetProperty,
                null,
                instance,
                new object[] { value });
        }

        private static object InvokeMethod(object instance, string methodName, params object[] arguments)
        {
            return instance.GetType().InvokeMember(
                methodName,
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                instance,
                arguments);
        }

        private static object GetIndexedProperty(object instance, object index)
        {
            try
            {
                return instance.GetType().InvokeMember(
                    "Item",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    new object[] { index });
            }
            catch
            {
                return instance.GetType().InvokeMember(
                    string.Empty,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    new object[] { index });
            }
        }

        private static bool InvokeBool(object instance, string methodName, object argument)
        {
            try
            {
                object result = instance.GetType().InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    instance,
                    new[] { argument });

                return Convert.ToBoolean(result);
            }
            catch
            {
                return false;
            }
        }

        private static int ReadIntProperty(object instance, string propertyName, int fallback)
        {
            try
            {
                object value = instance.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    null);

                return value == null ? fallback : Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static long ReadLongProperty(object instance, string propertyName, long fallback)
        {
            try
            {
                object value = instance.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    null);

                return value == null ? fallback : Convert.ToInt64(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ReadBoolProperty(object instance, string propertyName, bool fallback)
        {
            try
            {
                object value = instance.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    null);

                return value == null ? fallback : Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static void Log(Action<string> log, string message)
        {
            if (log != null)
                log(message);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string MediaTypeName(int mediaType)
        {
            switch (mediaType)
            {
                case 0: return "unknown";
                case 1: return "CD-ROM";
                case 2: return "CD-R";
                case 3: return "CD-RW";
                case 4: return "DVD-ROM";
                case 5: return "DVD-RAM";
                case 6: return "DVD+R";
                case 7: return "DVD+RW";
                case 8: return "DVD+R DL";
                case 9: return "DVD-R";
                case 10: return "DVD-RW";
                case 11: return "DVD-R DL";
                case 12: return "disc";
                case 13: return "DVD+RW DL";
                case 14: return "HD DVD-ROM";
                case 15: return "HD DVD-R";
                case 16: return "HD DVD-RAM";
                case 17: return "BD-ROM";
                case 18: return "BD-R";
                case 19: return "BD-RE";
                default: return "type " + mediaType;
            }
        }

        private static string MediaStatusName(int status)
        {
            if (status < 0)
                return "unknown";

            List<string> parts = new List<string>();
            AddFlag(parts, status, 0x00000001, "unknown");
            AddFlag(parts, status, 0x00000002, "blank");
            AddFlag(parts, status, 0x00000004, "appendable");
            AddFlag(parts, status, 0x00000008, "final session");
            AddFlag(parts, status, 0x00000400, "damaged");
            AddFlag(parts, status, 0x00000800, "erase required");
            AddFlag(parts, status, 0x00001000, "non-empty session");
            AddFlag(parts, status, 0x00002000, "write protected");
            AddFlag(parts, status, 0x00004000, "finalized");
            AddFlag(parts, status, 0x00008000, "unsupported media");

            return parts.Count == 0 ? "0x" + status.ToString("X8") : string.Join(", ", parts.ToArray());
        }

        private static void AddFlag(ICollection<string> parts, int value, int flag, string name)
        {
            if ((value & flag) == flag)
                parts.Add(name);
        }

        private static IEnumerable<string> EnumerateRecorderIds(object master)
        {
            List<string> ids = new List<string>();

            try
            {
                System.Collections.IEnumerable enumerable = master as System.Collections.IEnumerable;
                if (enumerable == null)
                    throw new InvalidCastException();

                foreach (object id in enumerable)
                    ids.Add(Convert.ToString(id));
            }
            catch
            {
                int count = ReadIntProperty(master, "Count", 0);
                for (int i = 0; i < count; i++)
                    ids.Add(Convert.ToString(GetIndexedProperty(master, i)));
            }

            return ids;
        }

        private static string ReadStringProperty(object instance, string propertyName)
        {
            try
            {
                object value = instance.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    instance,
                    null);

                return value == null ? string.Empty : Convert.ToString(value);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadDriveLetter(object recorder)
        {
            try
            {
                object value = recorder.GetType().InvokeMember(
                    "VolumePathNames",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    recorder,
                    null);

                Array values = value as Array;
                if (values == null || values.Length == 0)
                    return string.Empty;

                for (int i = 0; i < values.Length; i++)
                {
                    string path = Convert.ToString(values.GetValue(i));
                    if (!string.IsNullOrEmpty(path) && path.Length >= 3 && path[1] == ':' && path[2] == '\\')
                        return path.Substring(0, 2);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private sealed class ScsiRegistryDevice
        {
            public string InstanceKey;
            public int BusNumber;
            public int TargetId;
            public int Lun;
        }

        private static List<ScsiRegistryDevice> GetRegistryCdRomDevices(string registryDeviceKey)
        {
            List<ScsiRegistryDevice> devices = new List<ScsiRegistryDevice>();
            if (string.IsNullOrEmpty(registryDeviceKey))
                return devices;

            string path = @"SYSTEM\CurrentControlSet\Enum\SCSI\" + registryDeviceKey;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key == null)
                        return devices;

                    string[] instanceNames = key.GetSubKeyNames();
                    for (int i = 0; i < instanceNames.Length; i++)
                    {
                        using (RegistryKey instanceKey = key.OpenSubKey(instanceNames[i]))
                        {
                            if (instanceKey == null)
                                continue;

                            string location = Convert.ToString(instanceKey.GetValue("LocationInformation"));
                            ScsiRegistryDevice device = ParseScsiRegistryLocation(location);
                            if (device == null)
                                continue;

                            device.InstanceKey = instanceNames[i];
                            devices.Add(device);
                        }
                    }
                }
            }
            catch
            {
                return devices;
            }

            devices.Sort(delegate(ScsiRegistryDevice left, ScsiRegistryDevice right)
            {
                int comparison = left.BusNumber.CompareTo(right.BusNumber);
                if (comparison != 0)
                    return comparison;

                comparison = left.TargetId.CompareTo(right.TargetId);
                if (comparison != 0)
                    return comparison;

                comparison = left.Lun.CompareTo(right.Lun);
                if (comparison != 0)
                    return comparison;

                return string.Compare(left.InstanceKey, right.InstanceKey, StringComparison.OrdinalIgnoreCase);
            });

            return devices;
        }

        private static ScsiRegistryDevice GetRegistryDeviceLocation(string registryDeviceKey, string registryInstanceKey)
        {
            if (string.IsNullOrEmpty(registryInstanceKey))
                return null;

            List<ScsiRegistryDevice> devices = GetRegistryCdRomDevices(registryDeviceKey);
            for (int i = 0; i < devices.Count; i++)
            {
                if (string.Equals(devices[i].InstanceKey, registryInstanceKey, StringComparison.OrdinalIgnoreCase))
                    return devices[i];
            }

            return null;
        }

        private static ScsiRegistryDevice ParseScsiRegistryLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
                return null;

            Match match = Regex.Match(
                location,
                @"Bus Number\s+(\d+),\s*Target Id\s+(\d+),\s*LUN\s+(\d+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            return new ScsiRegistryDevice
            {
                BusNumber = Convert.ToInt32(match.Groups[1].Value),
                TargetId = Convert.ToInt32(match.Groups[2].Value),
                Lun = Convert.ToInt32(match.Groups[3].Value)
            };
        }

        private static string ExtractRegistryDeviceKey(string recorderId)
        {
            string[] parts = SplitRecorderId(recorderId);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }

        private static string ExtractRegistryInstanceKey(string recorderId)
        {
            string[] parts = SplitRecorderId(recorderId);
            return parts.Length > 2 ? parts[2] : string.Empty;
        }

        private static string[] SplitRecorderId(string recorderId)
        {
            if (string.IsNullOrEmpty(recorderId))
                return new string[0];

            string value = recorderId;
            if (value.StartsWith(@"\\?\", StringComparison.Ordinal))
                value = value.Substring(4);

            int classMarker = value.IndexOf("#{", StringComparison.Ordinal);
            if (classMarker >= 0)
                value = value.Substring(0, classMarker);

            return value.Split('#');
        }

        private static string ParseDeviceFieldFromRecorderId(string recorderId, string marker)
        {
            string deviceKey = ExtractRegistryDeviceKey(recorderId);
            if (string.IsNullOrEmpty(deviceKey))
                return string.Empty;

            string lower = deviceKey.ToLowerInvariant();
            int start = lower.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += marker.Length;
            int end = lower.IndexOf('&', start);
            if (end < 0)
                end = deviceKey.Length;

            return deviceKey.Substring(start, end - start);
        }

        private static IList<string> GetCdRomDriveLetters()
        {
            List<string> drives = new List<string>();
            DriveInfo[] allDrives;

            try
            {
                allDrives = DriveInfo.GetDrives();
            }
            catch
            {
                return drives;
            }

            for (int i = 0; i < allDrives.Length; i++)
            {
                if (allDrives[i].DriveType == DriveType.CDRom)
                    drives.Add(allDrives[i].Name.Substring(0, 2));
            }

            return drives;
        }

        private static string FindFirstCdRomDriveLetter()
        {
            IList<string> drives = GetCdRomDriveLetters();
            return drives.Count == 0 ? string.Empty : drives[0];
        }

        private static string FindCdrecordPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = LegacyPaths.ProgramFilesX86();

            string[] candidates = new[]
            {
                LegacyPaths.Combine(baseDir, "Tools", "cdrtools", "cdrecord.exe"),
                LegacyPaths.Combine(baseDir, "..", "..", "Tools", "cdrtools", "cdrecord.exe"),
                LegacyPaths.Combine(programFilesX86, "cdrtfe", "tools", "cdrtools", "cdrecord.exe"),
                LegacyPaths.Combine(programFiles, "cdrtfe", "tools", "cdrtools", "cdrecord.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = Path.GetFullPath(candidates[i]);
                if (File.Exists(candidate))
                    return candidate;
            }

            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string[] directories = path.Split(Path.PathSeparator);
            for (int i = 0; i < directories.Length; i++)
            {
                if (string.IsNullOrEmpty(directories[i]))
                    continue;

                try
                {
                    string candidate = Path.Combine(directories[i], "cdrecord.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static string FindReadcdPath()
        {
            string cdrecordPath = FindCdrecordPath();
            if (!string.IsNullOrEmpty(cdrecordPath))
            {
                string sibling = Path.Combine(Path.GetDirectoryName(cdrecordPath), "readcd.exe");
                if (File.Exists(sibling))
                    return sibling;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = LegacyPaths.ProgramFilesX86();

            string[] candidates = new[]
            {
                LegacyPaths.Combine(baseDir, "Tools", "cdrtools", "readcd.exe"),
                LegacyPaths.Combine(baseDir, "..", "..", "Tools", "cdrtools", "readcd.exe"),
                LegacyPaths.Combine(programFilesX86, "cdrtfe", "tools", "cdrtools", "readcd.exe"),
                LegacyPaths.Combine(programFiles, "cdrtfe", "tools", "cdrtools", "readcd.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = Path.GetFullPath(candidates[i]);
                if (File.Exists(candidate))
                    return candidate;
            }

            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string[] directories = path.Split(Path.PathSeparator);
            for (int i = 0; i < directories.Length; i++)
            {
                if (string.IsNullOrEmpty(directories[i]))
                    continue;

                try
                {
                    string candidate = Path.Combine(directories[i], "readcd.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string MakeVolumeName(string volumeName)
        {
            string value = string.IsNullOrEmpty(volumeName) ? "LUXBURN_DISC" : volumeName.Trim();
            if (value.Length > 32)
                value = value.Substring(0, 32);

            return value;
        }

        private static void CopyComStreamToFile(IStream stream, string outputPath)
        {
            const int BufferSize = 1024 * 1024;
            byte[] buffer = new byte[BufferSize];
            IntPtr bytesReadPtr = Marshal.AllocHGlobal(sizeof(int));

            try
            {
                using (FileStream output = File.Create(outputPath))
                {
                    while (true)
                    {
                        Marshal.WriteInt32(bytesReadPtr, 0);
                        stream.Read(buffer, buffer.Length, bytesReadPtr);
                        int bytesRead = Marshal.ReadInt32(bytesReadPtr);
                        if (bytesRead <= 0)
                            break;

                        output.Write(buffer, 0, bytesRead);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bytesReadPtr);
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateStreamOnFileW(string fileName, uint mode, out IStream stream);
    }
}

