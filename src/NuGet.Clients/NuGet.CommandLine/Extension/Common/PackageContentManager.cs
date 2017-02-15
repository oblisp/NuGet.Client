using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.Extension.Common
{
    public class PackageContentManager
    {
        public static Versioning.NuGetVersion NULL_VERSION = new Versioning.NuGetVersion("0.0.0");

        private static PackageContent PKG_INTEROP;

        private static PackageContent PKG_GUANJINKE;

        private static PackageContent PKG_BAR_CODES;

        private static PackageContent PKG_CEFGLUE;

        private static PackageContent PKG_MEDICARE;

        private static PackageContent PKG_VS;

        private static PackageContent PKG_SQLITE;

        private static PackageContent PKG_ZXING;

        private static PackageContent PKG_AFORGE;

        private static PackageContent PKG_ZIP_LIB;

        private static PackageContent PKG_WEBSOCKET;

        private Dictionary<string, PackageContent> PKG_MAPPINGS = new Dictionary<string, PackageContent>();

        private Dictionary<string, IEnumerable<PackageIdentity>> PKG_LIST_MAPPINGS = new Dictionary<string, IEnumerable<PackageIdentity>>();

        private String DefaultPackageVersion { get; set; }

        static PackageContentManager()
        {
            PKG_INTEROP = new PackageContent(new PackageIdentity("IIH.Interop", new Versioning.NuGetVersion("1.0.1")));
            PKG_INTEROP.AddItem(new PackageContentItem("ChnCharInfo", NULL_VERSION));
            PKG_INTEROP.AddItem(new PackageContentItem("Interop.IWshRuntimeLibrary", NULL_VERSION));
            PKG_INTEROP.AddItem(new PackageContentItem("Interop.MedicareComLib", NULL_VERSION));
            PKG_INTEROP.AddItem(new PackageContentItem("Microsoft.Office.Interop.Excel", NULL_VERSION));

            PKG_GUANJINKE = new PackageContent(new PackageIdentity("IIH.Guanjinke", new Versioning.NuGetVersion("1.0.0")));
            PKG_GUANJINKE.AddItem(new PackageContentItem("Guanjinke.Form", NULL_VERSION));

            PKG_BAR_CODES = new PackageContent(new PackageIdentity("IIH.BarCodes", new Versioning.NuGetVersion("1.0.0")));
            PKG_BAR_CODES.AddItem(new PackageContentItem("BarCodes1D", NULL_VERSION));

            PKG_CEFGLUE = new PackageContent(new PackageIdentity("IIH.CefGlue", new Versioning.NuGetVersion("1.0.1")));
            PKG_CEFGLUE.AddItem(new PackageContentItem("Xilium.CefGlue", NULL_VERSION));
            PKG_CEFGLUE.AddItem(new PackageContentItem("Xilium.CefGlue.WindowsForms", NULL_VERSION));

            PKG_MEDICARE = new PackageContent(new PackageIdentity("IIH.Medicare", new Versioning.NuGetVersion("1.0.0")));
            PKG_MEDICARE.AddItem(new PackageContentItem("MedicareCom", NULL_VERSION));

            PKG_VS = new PackageContent(new PackageIdentity("IIH.VisualStudio", new Versioning.NuGetVersion("1.0.0")));
            PKG_VS.AddItem(new PackageContentItem("Microsoft.VisualStudio.TextTemplating.10.0", NULL_VERSION));
            PKG_VS.AddItem(new PackageContentItem("Microsoft.VisualStudio.TextTemplating.Interfaces.10.0", NULL_VERSION));

            PKG_SQLITE = new PackageContent(new PackageIdentity("System.Data.SQLite", new Versioning.NuGetVersion("1.0.103")));
            PKG_SQLITE.AddItem(new PackageContentItem("System.Data.SQLite.Core", new Versioning.NuGetVersion("1.0.103"), "System.Data.SQLite"));
            PKG_SQLITE.AddItem(new PackageContentItem("System.Data.SQLite.EF6", new Versioning.NuGetVersion("1.0.103")));
            PKG_SQLITE.AddItem(new PackageContentItem("System.Data.SQLite.Linq", new Versioning.NuGetVersion("1.0.103")));

            PKG_ZXING = new PackageContent(new PackageIdentity("ZXing.Net", new Versioning.NuGetVersion("0.14.0.1")));
            PKG_ZXING.AddItem(new PackageContentItem("zxing", NULL_VERSION));
            PKG_ZXING.AddItem(new PackageContentItem("zxing.presentation", NULL_VERSION));


            PKG_AFORGE = new PackageContent(new PackageIdentity("AForge", new Versioning.NuGetVersion("2.2.5")), PackageContent.TARGET_FRAMEWORK_ALL);
            PKG_AFORGE.AddItem(new PackageContentItem("AForge", new Versioning.NuGetVersion("2.2.5")));
            PKG_AFORGE.AddItem(new PackageContentItem("AForge.Imaging", new Versioning.NuGetVersion("2.2.5")));
            PKG_AFORGE.AddItem(new PackageContentItem("AForge.Math", new Versioning.NuGetVersion("2.2.5")));

            PKG_ZIP_LIB = new PackageContent(new PackageIdentity("SharpZipLib", new Versioning.NuGetVersion("0.86.0")), "20");
            PKG_ZIP_LIB.AddItem(new PackageContentItem("SharpZipLib", new Versioning.NuGetVersion("0.86.0"), "ICSharpCode.SharpZipLib"));

            PKG_WEBSOCKET = new PackageContent(new PackageIdentity("WebSocketSharp", new Versioning.NuGetVersion("1.0.3-rc11")), PackageContent.TARGET_FRAMEWORK_ALL);
            PKG_WEBSOCKET.AddItem(new PackageContentItem("WebSocketSharp", new Versioning.NuGetVersion("1.0.3-rc11"), "websocket-sharp"));
        }

        public PackageContentManager(String defaultPackageVersion)
        {
            DefaultPackageVersion = defaultPackageVersion;
            if (DefaultPackageVersion == null)
            {
                DefaultPackageVersion = "0.2.5-b1";
            }
            PKG_MAPPINGS.Add("ChnCharInfo", PKG_INTEROP);
            PKG_MAPPINGS.Add("Interop.IWshRuntimeLibrary", PKG_INTEROP);
            PKG_MAPPINGS.Add("Interop.MedicareComLib", PKG_INTEROP);
            PKG_MAPPINGS.Add("Microsoft.Office.Interop.Excel", PKG_INTEROP);
            PKG_MAPPINGS.Add("Guanjinke.Form", PKG_GUANJINKE);
            PKG_MAPPINGS.Add("Xilium.CefGlue", PKG_CEFGLUE);
            PKG_MAPPINGS.Add("Xilium.CefGlue.WindowsForms", PKG_CEFGLUE);
            PKG_MAPPINGS.Add("MedicareCom", PKG_MEDICARE);

            PKG_MAPPINGS.Add("Microsoft.VisualStudio.TextTemplating.10.0", PKG_VS);
            PKG_MAPPINGS.Add("Microsoft.VisualStudio.TextTemplating.Interfaces.10.0", PKG_VS);

            PKG_MAPPINGS.Add("System.Data.SQLite", PKG_SQLITE);

            // PKG_MAPPINGS.Add("Newtonsoft.Json", new PackageContent(new PackageIdentity("Newtonsoft.Json", new Versioning.NuGetVersion("9.0.1"))));
            PKG_MAPPINGS.Add("Newtonsoft.Json", new PackageContent(new PackageIdentity("Newtonsoft.Json", new Versioning.NuGetVersion("6.0.8"))));
            PKG_MAPPINGS.Add("EntityFramework", new PackageContent(new PackageIdentity("EntityFramework", new Versioning.NuGetVersion("6.0.0"))));

            PKG_MAPPINGS.Add("Apache.NMS", new PackageContent(new PackageIdentity("Apache.NMS", new Versioning.NuGetVersion("1.7.1"))));
            PKG_MAPPINGS.Add("Apache.NMS.ActiveMQ", new PackageContent(new PackageIdentity("Apache.NMS.ActiveMQ", new Versioning.NuGetVersion("1.7.1"))));

            PKG_MAPPINGS.Add("BarCodes1D", PKG_BAR_CODES);

            PKG_MAPPINGS.Add("itextsharp", new PackageContent(new PackageIdentity("itextsharp", new Versioning.NuGetVersion("5.5.9")), PackageContent.TARGET_FRAMEWORK_ALL));
            PKG_MAPPINGS.Add("itextsharp.pdfa", new PackageContent(new PackageIdentity("itextsharp.pdfa", new Versioning.NuGetVersion("5.5.9")), PackageContent.TARGET_FRAMEWORK_ALL));

            PKG_MAPPINGS.Add("websocket-sharp", PKG_WEBSOCKET);

            PKG_MAPPINGS.Add("DCSoft.Writer", new PackageContent(new PackageIdentity("DCSoft.Writer", new Versioning.NuGetVersion("1.2015.12.23"))));

            PKG_MAPPINGS.Add("zxing", PKG_ZXING);

            PKG_MAPPINGS.Add("AForge.Imaging", PKG_AFORGE);

            PKG_MAPPINGS.Add("ICSharpCode.SharpZipLib", PKG_ZIP_LIB);

            // TODO
            PKG_MAPPINGS.Add("Castle.Core", new PackageContent(new PackageIdentity("Castle.Core", new Versioning.NuGetVersion("3.3.0")), "net40-client"));
            PKG_MAPPINGS.Add("Castle.Windsor", new PackageContent(new PackageIdentity("Castle.Windsor", new Versioning.NuGetVersion("3.3.0"))));
        }

        public PackageContent findPackageContent(String key)
        {
            if (key.StartsWith("xap.") || key.StartsWith("iih."))
            {
                // TODO
                PackageIdentity Id = new PackageIdentity(key, new Versioning.NuGetVersion(DefaultPackageVersion));
                return new PackageContent(Id);
            }
            else if (PKG_MAPPINGS.ContainsKey(key))
            {
                return PKG_MAPPINGS[key];
            }
            return null;
        }
    }
}
