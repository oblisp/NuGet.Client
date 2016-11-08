using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.Extension.Common
{
    public class PackageContent
    {
        public static string TARGET_FRAMEWORK_ALL = "all";

        public PackageIdentity Id { get; private set; }

        public ISet<PackageContentItem> Content { get; set; }

        public String TargetFramework { get; private set; }

        public PackageContent(PackageIdentity id)
        {
            Id = id;
        }

        public PackageContent(PackageIdentity id, String targetFramework)
        {
            Id = id;
            TargetFramework = targetFramework;
        }

        public void AddItem(PackageContentItem item)
        {
            if (item == null)
                return;
            if(Content == null)
            {
                Content = new HashSet<PackageContentItem>();
            }
            Content.Add(item);
        }
    }
}
