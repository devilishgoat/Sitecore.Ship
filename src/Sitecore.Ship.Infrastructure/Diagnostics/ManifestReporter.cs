using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Ship.Core.Contracts;

namespace Sitecore.Ship.Infrastructure.Diagnostics
{
    public class ManifestReporter
    {
        private readonly log4net.ILog logger;

        public ManifestReporter(log4net.ILog logger)
        {
            this.logger = logger;
        }

        public void ReportPackage(string packageFilePath)
        {
            // get the manifest file path (extracting it from update or zip)
            string manifestPath = ExtractManifestFile(packageFilePath);
            if (manifestPath == null) return;

            var manifestFile = LoadManifestFile(manifestPath);
            if (manifestFile == null) return;

            try
            { // clean
                System.IO.File.Delete(manifestPath);
            }
            catch
            {
            }

            logger.Info("***************** Begin manifest ****************");

            var rootNode = manifestFile.ChildNodes.Cast<XmlNode>().FirstOrDefault(item => item.Name == "DeployedItems");

            var canDeleteItems = UpdateCanDeleteItems(rootNode);
            if (canDeleteItems) logger.Info("Update can delete items");
            else logger.Info("Update cannot delete items");

            // Pull out the manifest items
            var allManifestItems = rootNode.ChildNodes.Cast<XmlNode>().Where(item => item.Name == "DeployedItem");

            logger.Info("----- CORE -----");
            var itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "core");
            ReportManifestList(itemsList, canDeleteItems, Sitecore.Data.Database.GetDatabase("core"));

            logger.Info("----- MASTER -----");
            itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "master");
            ReportManifestList(itemsList, canDeleteItems, Sitecore.Data.Database.GetDatabase("master"));

            logger.Info("----- WEB -----");
            itemsList = allManifestItems.Where(item => item.Attributes["Database"].Value == "web");
            ReportManifestList(itemsList, canDeleteItems, Sitecore.Data.Database.GetDatabase("web"));

            logger.Info("***************** End manifest ****************");
        }

        

        private void ReportManifestList(IEnumerable<XmlNode> manifestItems, bool canDeleteItems, Sitecore.Data.Database scDataBase)
        {
            var parsingList = manifestItems.ToList(); // clone the list in case we need to parse it recursivly.

            manifestItems.ForEach(manifestItem =>
            {
                string name, idString, updateMode;
                bool parseChildren = false;

                var canDeleteChildren = canDeleteItems && ItemCanDeleteChildren(manifestItem);
                idString = manifestItem.Attributes["Id"].Value;

                // check to see if we are adding or updating
                var scItem = scDataBase.GetItem(new ID(idString));
                if (scItem == null)
                {
                    name = manifestItem.Attributes["Name"].Value;
                    if (name.EndsWith(".item")) name = name.Substring(0, name.Length - 5);

                    scItem = scDataBase.GetItem(new ID(manifestItem.Attributes["Parent"].Value));
                    if (scItem != null) name = scItem.Paths.Path + "/" + name;
                   
                    updateMode = "ADD";
                    scItem = null;
                }
                else
                {
                    name = scItem.Paths.FullPath;
                    updateMode = "UPDATE";
                    if (canDeleteChildren) parseChildren = true;
                }

                string reportText = "[" + updateMode +"] " + name + " " + idString;
                logger.Info(reportText);

                if (parseChildren && scItem!=null) ReportChildTreeDeletions(scItem, parsingList, scDataBase);
            });
        }

        private void ReportChildTreeDeletions(Data.Items.Item parentItem, List<XmlNode> manifestItems, Sitecore.Data.Database scDataBase)
        {
            var items = parentItem.GetChildren();
            items.ForEach(item =>
            {
                if (!manifestItems.Any(manifestItem => manifestItem.Attributes["Id"].Value == item.ID.Guid.ToString()))
                {
                    logger.Info("[DELETE] " + item.Paths.FullPath + " " + item.ID.Guid.ToString());
                    // report all decendants as deleted, as we know these are not kept.
                    ReportChildTreeDeletionsAsDeleted(item, manifestItems, scDataBase);
                }
                else
                {
                    // check any child nodes recursivly.
                    ReportChildTreeDeletions(item, manifestItems, scDataBase);
                }
            });
        }

        private void ReportChildTreeDeletionsAsDeleted(Data.Items.Item parentItem, List<XmlNode> manifestItems, Sitecore.Data.Database scDataBase)
        {
            var items = parentItem.GetChildren();
            items.ForEach(item =>
            {
                logger.Info("[DELETE] " + item.Name + " " + item.ID.Guid.ToString());
                ReportChildTreeDeletionsAsDeleted(item, manifestItems, scDataBase);
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

        private bool UnzipTargetFile(string zipSource, string fileToUnzip, string pathToUnzipTo)
        {
            using (var reader = new Zip.ZipReader(zipSource))
            {
                var targetFile = reader.Entries.FirstOrDefault(entry => entry.Name == fileToUnzip);
                if (targetFile == null)
                {
                    logger.Warn("Could not report on deployment as ship cannot find file (" + fileToUnzip + ") in package at " + zipSource);
                    return false;
                }

                var buffer = new byte[1024];
                using (var readStream = targetFile.GetStream())
                {
                    using (var writeStream = new System.IO.FileStream(pathToUnzipTo, System.IO.FileMode.Create, System.IO.FileAccess.Write))
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

            return true;
        }

        private string ExtractManifestFile(string packageFilePath)
        {
            if (!System.IO.File.Exists(packageFilePath))
            {
                logger.Warn("Could not report on deployment as ship cannot find pacakge file at " + packageFilePath);
                return null;
            }

            string extractedTempPackagePath=null;
            var guid = Guid.NewGuid();

            if (packageFilePath.Trim().ToLower().EndsWith(".update"))
            {
                extractedTempPackagePath = System.IO.Path.GetTempPath() + "\\package." + guid + ".zip";
                if (!UnzipTargetFile(packageFilePath, "package.zip", extractedTempPackagePath))
                {
                    return null;
                }
                packageFilePath = extractedTempPackagePath;
            }

            var targetPath = System.IO.Path.GetTempPath() + "\\DeployedItems." + guid + ".xml";
            if (!UnzipTargetFile(packageFilePath, "addedfiles/_DEV/DeployedItems.xml", targetPath))
            {
                return null;
            }

            if (extractedTempPackagePath != null)
            {
                try
                {
                    System.IO.File.Delete(extractedTempPackagePath);
                }
                catch
                {
                }
            }


            return targetPath;
        }

        private XmlDocument LoadManifestFile(string manifestFilePath)
        {
            if (!System.IO.File.Exists(manifestFilePath))
            {
                logger.Warn("Could not report on deployment as ship cannot find manifest file at " + manifestFilePath);
                return null;
            }

            XmlDocument manifestFile = new XmlDocument();
            manifestFile.Load(manifestFilePath);

            if (manifestFile.ChildNodes.Count == 0)
            {
                logger.Warn("Ship's manifest file appears to be blank at " + manifestFilePath);
                return null;
            }

            return manifestFile;
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