using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sitecore.ContentSearch.Utilities;

namespace Sitecore.Ship.Infrastructure.Helpers
{
    public static class Utilities
    {
        public static string AssemblyRunningDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public static string SitecoreTempDirectory
        {
            get
            {
                var temp = Utilities.AssemblyRunningDirectory.Split(new char[] { '\\', '/' }).ToList();
                temp.RemoveAt(temp.Count - 1);// down one dir
                var tempDirectory = string.Join("\\", temp) + "\\temp\\";
                if (!System.IO.Directory.Exists(tempDirectory)) System.IO.Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
        }

        public static bool UnzipTargetFile(string zipSource, string fileToUnzip, string pathToUnzipTo)
        {
            using (var reader = new Zip.ZipReader(zipSource))
            {
                if (!UnzipTargetFile(reader, fileToUnzip, pathToUnzipTo)) return false;
            }

            return true;
        }

        public static bool UnzipTargetFile(Zip.ZipReader reader, string fileToUnzip, string filePathToUnzipTo)
        {
            var targetFile = reader.Entries.FirstOrDefault(entry => entry.Name == fileToUnzip);
            if (targetFile == null) return false;

            string pathToUnzipTo = Path.GetDirectoryName(filePathToUnzipTo);
            string fileName = Path.GetFileName(filePathToUnzipTo);

            targetFile.ExtractItem(pathToUnzipTo, false, fileName);
            return true;
        }

        
        public static void ExtractAll(string zipFile, string pathTo, bool includeTargetPathing = true)
        {
            using (var reader = new Zip.ZipReader(zipFile))
            {
                reader.Entries.ForEach(entry =>
                {
                    entry.ExtractItem(pathTo, includeTargetPathing);
                });
            }
        }

        public static void ExtractItem(this Zip.ZipEntry entry, string pathTo, bool includeTargetPathing=true, string targetFileName = null)
        {
            if (entry.IsDirectory)
            {
                if (includeTargetPathing && !Directory.Exists(pathTo + entry.Name)) Directory.CreateDirectory(pathTo + entry.Name);
            }
            else
            {
                string fullFilePath = pathTo + "\\";
                

                if (includeTargetPathing)
                {
                    fullFilePath += Path.GetDirectoryName(entry.Name);
                    if (!fullFilePath.EndsWith("\\")) fullFilePath += "\\";
                }
                
                if (targetFileName == null)
                {
                    fullFilePath += Path.GetFileName(entry.Name);
                }
                else
                {
                    fullFilePath += targetFileName;
                }

                string fullDirPath = Path.GetDirectoryName(fullFilePath);
                if (!Directory.Exists(fullDirPath))
                {
                    Directory.CreateDirectory(fullDirPath);
                }

                var buffer = new byte[1024];
                using (var readStream = entry.GetStream())
                {
                    using (var writeStream = new System.IO.FileStream(fullFilePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    {
                        while (readStream.CanRead)
                        {
                            var bytesRead = readStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break;
                            writeStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }

        public static void ZipAll(string pathFrom, string zipTo)
        {
            using (var writer = new Alienlab.Zip.ZipFile(zipTo, Encoding.UTF8))
            {
                var allFiles = Directory.GetFiles(pathFrom, "*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    string entryName = file.Substring(pathFrom.Length);
                    writer.AddItem(file, Path.GetDirectoryName(entryName));
                }
                writer.Save(zipTo);
            }
        }

        public static void ZipFile(string fileFrom, string zipTo)
        {

            using (var writer = new Alienlab.Zip.ZipFile(zipTo, Encoding.UTF8))
            {
                string entryName = Path.GetFileName(fileFrom);
              
                writer.AddItem(fileFrom, Path.GetDirectoryName(entryName));
                writer.Save(zipTo);
            }
        }
    }
}
