using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Security;
using System.Xml;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Install.Framework;
using Sitecore.Install.Security;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Security.Accounts;
using Sitecore.SecurityModel;

namespace Sitecore.Ship.Infrastructure.Update
{
    public class AccountInstaller : Sitecore.Install.Security.AccountInstaller
    {
        private static readonly MethodInfo GetAccountNameMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("GetAccountName", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ContextUserHasEnoughRightsMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("ContextUserHasEnoughRights", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly PropertyInfo UIEventsPI = typeof(Sitecore.Install.Security.AccountInstaller).GetProperty("UIEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo TryingToInstallAdminMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("TryingToInstallAdmin", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ReadXmlMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("ReadXml", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo SetUserProfileMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetUserPropertiesMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserProperties", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetUserRolesMI = typeof(Sitecore.Install.Security.AccountInstaller).GetMethod("SetUserRoles", BindingFlags.Instance | BindingFlags.NonPublic);
        private bool _skipAll;

        private IAccountInstallerEvents UIEvents
        {
            get
            {
                return (IAccountInstallerEvents)AccountInstaller.UIEventsPI.GetGetMethod(true).Invoke((object)this, (object[])null);
            }
        }

        public override void Put(PackageEntry entry)
        {
            this._skipAll = (bool)typeof(Sitecore.Install.Security.AccountInstaller).GetField("_skipAll", BindingFlags.Instance | BindingFlags.NonPublic).GetValue((object)this);
            if (this._skipAll)
                return;
            string[] strArray = entry.Key.Split('/');
            if (strArray[0] != "security")
                return;
            if (!AccountInstaller.ContextUserHasEnoughRights())
            {
                JobContext.Alert(Translate.Text("You do not have enough permissions to install security accounts"));
                this._skipAll = true;
            }
            else if (strArray.Length < 3)
            {
                Log.Error(string.Format("Bad entry key '{0}'", (object)entry.Key), (object)this);
            }
            else
            {
                if (strArray.Length > 3)
                {
                    string domainName = strArray[2];
                    if (!DomainManager.DomainExists(domainName))
                    {
                        this.UIEvents.ShowWarning(string.Format(Translate.Text("Unable to create the user '{0}' because domain '{1}' doesn't exist."), (object)AccountInstaller.GetAccountName(entry.Key), (object)domainName), "domain doesn't exist" + domainName);
                        return;
                    }
                }
                string str = strArray[1];
                try
                {
                    if (str == "users")
                        this.InstallUser(entry);
                    else if (str == "roles")
                        this.InstallRole(entry);
                    else
                        Log.Error(string.Format("Unexpected account type '{0}'", (object)entry.Key), (object)this);
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error installing entry '{0}'", (object)entry.Key), ex, (object)this);
                }
            }
        }

        private static string GetAccountName(string key)
        {
            return (string)AccountInstaller.GetAccountNameMI.Invoke((object)null, new object[1]
      {
        (object) key
      });
        }

        private static bool ContextUserHasEnoughRights()
        {
            return (bool)AccountInstaller.ContextUserHasEnoughRightsMI.Invoke((object)null, (object[])null);
        }

        private bool TryingToInstallAdmin(XmlDocument xml, string userName)
        {
            return (bool)AccountInstaller.TryingToInstallAdminMI.Invoke((object)this, new object[2]
      {
        (object) xml,
        (object) userName
      });
        }

        private static XmlDocument ReadXml(PackageEntry entry)
        {
            return (XmlDocument)AccountInstaller.ReadXmlMI.Invoke((object)null, new object[1]
      {
        (object) entry
      });
        }

        private void SetUserProfile(User user, XmlDocument xml)
        {
            AccountInstaller.SetUserProfileMI.Invoke((object)this, new object[2]
      {
        (object) user,
        (object) xml
      });
        }

        private void SetUserProperties(User user, XmlDocument xml)
        {
            AccountInstaller.SetUserPropertiesMI.Invoke((object)this, new object[2]
      {
        (object) user,
        (object) xml
      });
        }

        private void SetUserRoles(User user, XmlDocument xml)
        {
            AccountInstaller.SetUserRolesMI.Invoke((object)this, new object[2]
      {
        (object) user,
        (object) xml
      });
        }

        protected new void InstallUser(PackageEntry entry)
        {
            string accountName = AccountInstaller.GetAccountName(entry.Key);
            if (User.Exists(accountName))
            {
                Log.Info(string.Format("Installing of entry '{0}' was skipped. User already exists.", (object)accountName), (object)this);
                this.UIEvents.ShowWarning(string.Format(Translate.Text("User '{0}' will not be installed since the user already exists."), (object)accountName), "user already exists");
            }
            else
            {
                XmlDocument xml = AccountInstaller.ReadXml(entry);
                if (!Context.User.IsAdministrator && this.TryingToInstallAdmin(xml, accountName))
                    return;
                string password = AccountInstaller.GetPassword(xml);
                User user = (User)null;
                try
                {
                    user = User.Create(accountName, password);
                    this.SetUserProfile(user, xml);
                    this.SetUserProperties(user, xml);
                    this.SetUserRoles(user, xml);
                }
                catch (Exception ex)
                {
                    try
                    {
                        if ((Account)user != (Account)null)
                            user.Delete();
                    }
                    catch
                    {
                    }
                    Log.Error(string.Format("Failed to install the user '{0}'", (object)accountName), ex, (object)this);
                    throw;
                }
                Log.Info(string.Format("User '{0}' has been installed successfully", (object)user.Name), (object)this);
            }
        }

        private static string GetPassword(XmlDocument xml)
        {
            XmlNodeList xmlNodeList = xml.SelectNodes("/user/password");
            if (xmlNodeList != null)
            {
                if (xmlNodeList.Count == 1)
                    return Encoding.Unicode.GetString(System.Convert.FromBase64String(xmlNodeList[0].InnerText));
            }

            var sqlMembershipProvider = Membership.Providers["sql"] as SqlMembershipProvider;
            if (sqlMembershipProvider == null)
            {
                throw new ArgumentException("Could not get sql membership provider");
            }

            return sqlMembershipProvider.GeneratePassword();
        }
    }
}
