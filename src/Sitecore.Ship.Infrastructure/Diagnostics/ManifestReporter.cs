using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Ship.Core.Contracts;
using Sitecore.Ship.Core.Reporting;
using Sitecore.Ship.Infrastructure.Helpers;

namespace Sitecore.Ship.Infrastructure.Diagnostics
{
    

    public class ManifestReporter : IDisposable
    {
        private readonly log4net.ILog logger;
        private readonly string manifestFilePath = "addedfiles/_DEV/DeployedItems.xml";
        private readonly string tempDirectory;

        public string ExtractedTempPackagePath
        {
            get; private set;
        }

        public string SessionTempDirectory
        {
            get; private set;
        }

        

        public ManifestReporter(log4net.ILog logger)
        {
            this.logger = logger;
            tempDirectory = Utilities.SitecoreTempDirectory;
            ExtractedTempPackagePath = null;
            SessionTempDirectory = null;
        }

        public ManifestReport ReportPackage(string packageFilePath)
        {
            var manifestReport = new ManifestReport()
            {
                Databases = new List<ManifestReportDataBase>()
            };

            // get the all the files (extracting it from update or zip)
            string extractionPath = ExtractPackageFiles(packageFilePath, manifestReport);
            if (extractionPath == null) return manifestReport;

            var manifestFile = LoadManifestFile(extractionPath, manifestReport);
            if (manifestFile == null) return manifestReport;

            logger.Info("***************** Begin manifest ****************");

            var rootNode = manifestFile.ChildNodes.Cast<XmlNode>().FirstOrDefault(item => item.Name == "DeployedItems");

            manifestReport.CanDeleteItems = UpdateCanDeleteItems(rootNode);
            if (manifestReport.CanDeleteItems) logger.Info("Update can delete items");
            else logger.Info("Update cannot delete items");

            // Pull out the manifest items
            var allManifestItems = rootNode.ChildNodes.Cast<XmlNode>().Where(item => item.Name == "DeployedItem");

            
            logger.Info("----- CORE -----");
            var addedItemsPath = extractionPath + "\\core";
            var itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "core");
            var reportDb = ReportManifestList(itemsList, addedItemsPath + "\\", manifestReport.CanDeleteItems, Sitecore.Data.Database.GetDatabase("core"));
            manifestReport.Databases.Add(reportDb);
            
            logger.Info("----- MASTER -----");
            addedItemsPath = extractionPath + "\\master";
            itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "master");
            reportDb = ReportManifestList(itemsList, addedItemsPath + "\\", manifestReport.CanDeleteItems, Sitecore.Data.Database.GetDatabase("master"));
            manifestReport.Databases.Add(reportDb);

            logger.Info("----- WEB -----");
            addedItemsPath = extractionPath + "\\web";
            itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "web");
            reportDb = ReportManifestList(itemsList, addedItemsPath + "\\", manifestReport.CanDeleteItems, Sitecore.Data.Database.GetDatabase("web"));
            manifestReport.Databases.Add(reportDb);

            logger.Info("***************** End manifest ****************");

            return manifestReport;
        }

        

        private ManifestReportDataBase ReportManifestList(IEnumerable<XmlNode> manifestItems, string itemsExtractionPath, bool canDeleteItems, Sitecore.Data.Database scDataBase)
        {
            var manifestReportDatabase = new ManifestReportDataBase()
            {
                Database = scDataBase.Name,
                Items = new List<ManifestReportItem>()
            };
            var parsingList = manifestItems.ToList(); // clone the list in case we need to parse it recursivly.

            manifestItems.ForEach(manifestItem =>
            {
                var manifestReportItem = new ManifestReportItem();
                bool parseChildren = false;

                var canDeleteChildren = canDeleteItems && ItemCanDeleteChildren(manifestItem);
                manifestReportItem.Id = NormalizeGuid(manifestItem.Attributes["Id"].Value);

                // check to see if we are adding or updating
                var scItem = scDataBase.GetItem(new ID(manifestReportItem.Id));
                if (scItem == null)
                {

                    manifestReportItem.FullPath = manifestItem.Attributes["Name"].Value;
                    if (manifestReportItem.FullPath.EndsWith(".item")) manifestReportItem.FullPath = manifestReportItem.FullPath.Substring(0, manifestReportItem.FullPath.Length - 5);

                    scItem = scDataBase.GetItem(new ID(manifestItem.Attributes["Parent"].Value));
                    if (scItem != null) manifestReportItem.FullPath = scItem.Paths.Path + "/" + manifestReportItem.FullPath;

                    manifestReportItem.UpdateType = "ADD";
                    scItem = null;
                }
                else
                {
                    if (IsItemDeployOnce(manifestItem, itemsExtractionPath))
                    {
                        manifestReportItem.FullPath = scItem.Paths.FullPath;
                        manifestReportItem.UpdateType = "IGNORE";
                        parseChildren = false;
                    }
                    else
                    {
                        manifestReportItem.FullPath = scItem.Paths.FullPath;
                        manifestReportItem.UpdateType = "UPD";
                        if (canDeleteChildren) parseChildren = true;
                    }

                    
                }
            
                logger.Info(manifestReportItem.ToString());
                manifestReportDatabase.Items.Add(manifestReportItem);

                if (parseChildren && scItem!=null) ReportChildTreeDeletions(scItem, parsingList, scDataBase, manifestReportDatabase);
            });

            manifestReportDatabase.Items = manifestReportDatabase.Items.OrderBy(i => i.FullPath).ToList();
            return manifestReportDatabase;
        }

        private bool IsItemDeployOnce(XmlNode manifestItem, string itemsExtractionPath)
        {
            string fileName = manifestItem.Attributes["Name"].Value;
            fileName = fileName.Substring(0, fileName.Length - ".item".Length);
            fileName += "_" + manifestItem.Attributes["Id"].Value;
            fileName = fileName.ToLower();

            var itemFile = new XmlDocument();
            itemFile.Load(itemsExtractionPath + fileName);

            var node = itemFile.SelectSingleNode("/addItemCommand/collisionbehavior/overwriteExisting");
            return node.InnerText == "false";
        }

        private string NormalizeGuid(string guid)
        {
            guid =  guid.ToLower();
            if (guid.StartsWith("{")) return guid.Substring(1, guid.Length - 2);
            else return guid;
        }

        private string NormalizeGuid(Guid guid)
        {
            return guid.ToString().ToLower();
        }

        private void ReportChildTreeDeletions(Data.Items.Item parentItem, List<XmlNode> manifestItems, Sitecore.Data.Database scDataBase, ManifestReportDataBase manifestReportDatabase)
        {
            var items = parentItem.GetChildren();
            items.ForEach(item =>
            {
                if (!manifestItems.Any(manifestItem => NormalizeGuid(manifestItem.Attributes["Id"].Value) == NormalizeGuid(item.ID.Guid)))
                {
                    manifestReportDatabase.Items.Add(new ManifestReportItem()
                    {
                        FullPath = item.Paths.FullPath,
                        Id = NormalizeGuid(item.ID.Guid),
                        UpdateType = "DEL"
                    });

                    logger.Info("[DELETE] " + item.Paths.FullPath + " " + NormalizeGuid(item.ID.Guid));
                    // report all decendants as deleted, as we know these are not kept.
                    ReportChildTreeDeletionsAsDeleted(item, manifestItems, scDataBase, manifestReportDatabase);
                }
                else
                {
                    // check any child nodes recursivly.
                    ReportChildTreeDeletions(item, manifestItems, scDataBase, manifestReportDatabase);
                }
            });
        }

        private void ReportChildTreeDeletionsAsDeleted(Data.Items.Item parentItem, List<XmlNode> manifestItems, Sitecore.Data.Database scDataBase, ManifestReportDataBase manifestReportDatabase)
        {
            var items = parentItem.GetChildren();
            items.ForEach(item =>
            {
                manifestReportDatabase.Items.Add(new ManifestReportItem()
                {
                    FullPath = item.Paths.FullPath,
                    Id = NormalizeGuid(item.ID.Guid),
                    UpdateType = "DEL"
                });

                logger.Info("[DELETE] " + item.Name + " " + NormalizeGuid(item.ID.Guid));
                ReportChildTreeDeletionsAsDeleted(item, manifestItems, scDataBase, manifestReportDatabase);
            });
        }

        /// <summary>
        /// Checks for the specific attribute value to establish if the update is allowed to delete items from sitecore
        /// </summary>
        private bool UpdateCanDeleteItems(XmlNode rootNode)
        {
            return IsAttributeValue(rootNode, "RecursiveDeployAction", "Delete");
        }

        private bool ItemCanDeleteChildren(XmlNode manifestItem)
        {
            return IsAttributeValue(manifestItem, "KeepChildrenInSync", "true");
        }

        private bool IsAttributeValue(XmlNode node, string attributeName, string value)
        {
            var attribute = node.Attributes[attributeName];
            if (attribute != null)
            {
                return (attribute.Value == value);
            }
            return false;
        }
        
        private void UnzipPackageFiles(string zipSource, string pathToUnzipTo)
        {
            using (var reader = new Zip.ZipReader(zipSource))
            {
                // unzip manifest
                Utilities.UnzipTargetFile(reader, this.manifestFilePath, pathToUnzipTo + "DeployedItems.xml");

                //unzip everything in the addeditems folder into a flat list as filenames are unique, but in there db folders
                UnzipPackageFilesForDB(reader, pathToUnzipTo, "core");
                UnzipPackageFilesForDB(reader, pathToUnzipTo, "master");
                UnzipPackageFilesForDB(reader, pathToUnzipTo, "web");
            }
        }

        private void UnzipPackageFilesForDB(Zip.ZipReader reader, string pathToUnzipTo, string dbName)
        {
            var entries = reader.Entries.Where(entry => entry.Name.StartsWith("addeditems/"+ dbName));
            if (!entries.Any()) return;
            
            pathToUnzipTo += dbName;
            System.IO.Directory.CreateDirectory(pathToUnzipTo);
            pathToUnzipTo += "\\";

            entries.ForEach(entry =>
            {
                if (!entry.IsDirectory)
                {
                    entry.ExtractItem(pathToUnzipTo,false);
                }
            });
        }

        private void CleanSessionTempDirectory()
        {
            if (SessionTempDirectory != null)
            {
                try
                {
                    System.IO.Directory.Delete(SessionTempDirectory,true);
                }
                catch
                {
                }
                SessionTempDirectory = null;
            }
        }

        private string ExtractPackageFiles(string packageFilePath, ManifestReport manifestReport)
        {
            if (!System.IO.File.Exists(packageFilePath))
            {
                logger.Warn(manifestReport.SetError("Could not report on deployment as ship cannot find pacakge file at " + packageFilePath).Error);
                return null;
            }

            if (SessionTempDirectory != null) CleanSessionTempDirectory();
            var guid = Guid.NewGuid();
            SessionTempDirectory = tempDirectory + guid + "\\";
            System.IO.Directory.CreateDirectory(SessionTempDirectory);

            if (packageFilePath.Trim().ToLower().EndsWith(".update"))
            {
                ExtractedTempPackagePath = SessionTempDirectory + "package." + guid + ".zip";
                if (!Utilities.UnzipTargetFile(packageFilePath, "package.zip", ExtractedTempPackagePath))
                {
                    logger.Warn(manifestReport.SetError("Could not report on deployment as ship cannot find package zip in the update file at " + packageFilePath).Error);
                    return null;
                }
                packageFilePath = ExtractedTempPackagePath;
            }

            try
            {
                UnzipPackageFiles(packageFilePath, SessionTempDirectory);
            }
            catch (Exception ex)
            {
                logger.Error(manifestReport.SetError("Could not report on deployment as ship cannot unzip all files in " + packageFilePath + ". " + ex.Message).Error);
                return null;
            }
            

            return SessionTempDirectory;
        }

        private XmlDocument LoadManifestFile(string manifestFilePath, ManifestReport manifestReport)
        {
            manifestFilePath += this.manifestFilePath.Split(new char[] {'/','\\'}).Last();

            if (!System.IO.File.Exists(manifestFilePath))
            {
                logger.Warn(manifestReport.SetError("Could not report on deployment as ship cannot find manifest file at " + manifestFilePath).Error);
                return null;
            }

            var manifestFile = new XmlDocument();
            manifestFile.Load(manifestFilePath);

            if (manifestFile.ChildNodes.Count == 0)
            {
                logger.Warn(manifestReport.SetError("Ship's manifest file appears to be blank at " + manifestFilePath).Error);
                return null;
            }

            return manifestFile;
        }

        public void Dispose()
        {
            CleanSessionTempDirectory();
        }


        /*
        public void ReportHistory(string historyFolderPath)
        {
            if (!(historyFolderPath.EndsWith("\\") || historyFolderPath.EndsWith("/"))) historyFolderPath += "\\";

            var filePath = historyFolderPath + "transactions\\_DEV\\DeployedItems.xml";

            var historyDoc = LoadManifestFile(filePath);
            if (historyDoc == null) return;


            var canDeleteItems = UpdateCanDeleteItems(historyDoc);
            if (canDeleteItems) logger.Write("[Update can delete items]");
            else logger.Write("[Update cannot delete items]");

            var deployedIitems = historyDoc.ParentNode.ChildNodes.Cast<XmlNode>().Where(item => item.Name == "DeployedItem");

            deployedIitems.ForEach(deployedItem =>
            {
                
            });

            //KeepChildrenInSync="true"


        }
        */

    }
}