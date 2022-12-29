﻿using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Xml;

namespace UpdateSotMEngineNuGetPackages
{
    internal class Program
    {
        private const int SotMSteamAppID = 337150;
        private const string EngineCommon = "EngineCommon";
        private const string SentinelsEngine = "SentinelsEngine";
        private const string SteamCmd = "SteamCMD";
        private const string SteamCmdDownloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
        private const string SotMInstallEngineSubdirectory = $"{SteamCmd}\\steamapps\\common\\Sentinels of the Multiverse\\Sentinels_Data\\Managed";

        private static string SteamUsername;
        private static string SteamPassword;
        private static string AzureFeed;
        private static string DownloadsDirectory;
        private static string ArtifactsDirectory;
        private static string ArtifactStagingDirectory;

        static void Main(string[] args)
        {
            // Get args
            if (args.Length < 5)
            {
                throw new ArgumentException("Not enough arguments");
            }

            SteamUsername = args[0];
            Console.WriteLine($"{nameof(SteamUsername)}: {SteamUsername}");

            SteamPassword = args[1];
            Console.WriteLine($"{nameof(SteamPassword)}: {SteamPassword}");

            AzureFeed = args[2];
            Console.WriteLine($"{nameof(AzureFeed)}: {AzureFeed}");

            ArtifactsDirectory = args[3];
            Console.WriteLine($"{nameof(ArtifactsDirectory)}: {ArtifactsDirectory}");

            ArtifactStagingDirectory = args[4];
            Console.WriteLine($"{nameof(ArtifactStagingDirectory)}: {ArtifactStagingDirectory}");

            DownloadsDirectory = SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0);
            Console.WriteLine($"{nameof(DownloadsDirectory)}: {DownloadsDirectory}\n");

            // Install Sentinels of the Multiverse locally
            InstallSentinelsOfTheMultiverse();

            // Update both engine nuget packages
            bool ecUpdated = UpdateSotMEngineNugetPackage(EngineCommon);
            bool seUpdated = UpdateSotMEngineNugetPackage(SentinelsEngine);

            // If either package was updated, set the variable to true
            if (ecUpdated || seUpdated)
            {

            }
            else
            {

            }
        }

        private static void InstallSentinelsOfTheMultiverse()
        {
            // Download and install SteamCMD
            Console.WriteLine("Downloading SteamCMD.zip...");
            string SteamCmdPath = Path.Combine(DownloadsDirectory, SteamCmd);
            string SteamCmdZip = $"{SteamCmdPath}.zip";
            DownloadFile(SteamCmdDownloadUrl, SteamCmdZip);

            Console.WriteLine("Extracting SteamCMD.zip...");
            ZipFile.ExtractToDirectory(SteamCmdZip, SteamCmdPath, true);

            // Use SteamCMD to install Sentinels of the Multiverse
            Console.WriteLine("Installing Sentinels of the Multiverse...\n");
            string SteamCmdExe = Path.Combine(SteamCmdPath, $"{SteamCmd}.exe");
            ExecuteCommand($"{SteamCmdExe} +login {SteamUsername} {SteamPassword} +app_update {SotMSteamAppID} -validate +quit");
        }

        private static bool UpdateSotMEngineNugetPackage(string engineName)
        {
            // Get the version info of the newest engine dll
            FileVersionInfo newestEngineFvi = FileVersionInfo.GetVersionInfo(Path.Combine(DownloadsDirectory, SotMInstallEngineSubdirectory, $"{engineName}.dll"));
            Console.WriteLine($"\n{engineName} version from install: {newestEngineFvi.FileVersion}");

            // Get the version info of the current engine dll
            string nugetEngineDll = Directory.GetFiles(ArtifactsDirectory, $"{engineName}.dll", SearchOption.AllDirectories).SingleOrDefault();
            FileVersionInfo currentEngineFvi = String.IsNullOrWhiteSpace(nugetEngineDll) ? null : FileVersionInfo.GetVersionInfo(nugetEngineDll);
            Console.WriteLine($"\n{engineName} version from nuget: {newestEngineFvi?.FileVersion ?? "NOT FOUND"}");

            // If the newest engine version is greater than the current engine version...
            if (currentEngineFvi == null
                || newestEngineFvi.FileMajorPart > currentEngineFvi.FileMajorPart
                || newestEngineFvi.FileMinorPart > currentEngineFvi.FileMinorPart
                || newestEngineFvi.FileBuildPart > currentEngineFvi.FileBuildPart)
            {
                // Update the NuGet package
                CreateNuGetPackage(engineName);
                return true;
            }
            else
            {
                // The NuGet package does not need to be updated
                Console.WriteLine("No NuGet update required.");
                return false;
            }
        }

        private static void CreateNuGetPackage(string engineName)
        {
            // Create nuspec file for this engine package
            string nuspecFile = CreateEngineNuspecFile(engineName);

            // Use the nuspec file to create the nuget package
            ExecuteCommand($"nuget pack {nuspecFile} -OutputDirectory {ArtifactStagingDirectory} -OutputFileNamesWithoutVersion");
        }

        private static string CreateEngineNuspecFile(string engineName)
        {
            Console.WriteLine("Creating NuSpec file...");
            string EngineDll = Path.Combine(DownloadsDirectory, SotMInstallEngineSubdirectory, $"{engineName}.dll");
            FileVersionInfo EngineFvi = FileVersionInfo.GetVersionInfo(EngineDll);
            string nuspecFile = Path.Combine(ArtifactStagingDirectory, $"{engineName}.nuspec");

            XmlWriterSettings xws = new XmlWriterSettings();
            xws.Async = false;
            xws.Indent = true;
            xws.IndentChars = "\t";

            using (XmlWriter xmlWriter = XmlWriter.Create(nuspecFile, xws))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("package");
                xmlWriter.WriteStartElement("metadata");
                xmlWriter.WriteElementString("id", engineName);
                xmlWriter.WriteElementString("version", $"{EngineFvi.FileMajorPart}.{EngineFvi.FileMinorPart}.{EngineFvi.FileBuildPart}");
                xmlWriter.WriteElementString("description", "SotM Engine DLL that is required to write mods for the game. I don't own this dll in any way, shape, or form. It belongs to Handelabra.");
                xmlWriter.WriteElementString("authors", "DiffidentDeckard, Handelabra");
                xmlWriter.WriteElementString("icon", $"images\\{engineName}.png");
                xmlWriter.WriteElementString("tags", "SentinelsOfTheMultiverse Sentinels Multiverse SotM Engine Mod Steam Workshop DLL");
                xmlWriter.WriteStartElement("dependencies");
                xmlWriter.WriteStartElement("group");
                xmlWriter.WriteAttributeString("targetFramework", "net35");
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("files");
                xmlWriter.WriteStartElement("file");
                xmlWriter.WriteAttributeString("src", EngineDll);
                xmlWriter.WriteAttributeString("target", "lib\\net35");
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("file");
                xmlWriter.WriteAttributeString("src", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", $"{engineName}.png"));
                xmlWriter.WriteAttributeString("target", "images\\");
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
            }
            Console.WriteLine("NuSpec file created.");
            return nuspecFile;
        }

        private static void DownloadFile(string address, string fileName)
        {
            using (var client = new WebClient())
            {
                client.DownloadFile(address, fileName);
            }
        }

        private static void ExecuteCommand(string command)
        {
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = $"/c {command}";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                Console.WriteLine(p.StandardOutput.ReadLine());
            }
        }

        [DllImport("shell32", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);

    }
}