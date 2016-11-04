using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Extension.Common
{
    public class PackageContentItem
    {
        public String Name { get; private set; }

        public String Alias { get; private set; }

        public Versioning.NuGetVersion Version { get; private set; }

        public bool isNugetPackage { get; private set; }

        public PackageContentItem(String name, Versioning.NuGetVersion version)
        {
            Name = name;
            Version = version;
            if(!PackageContentManager.NULL_VERSION.Equals(Version))
            {
                isNugetPackage = true;
            }
        }

        public PackageContentItem(String name, Versioning.NuGetVersion version, String alias) : this(name, version)
        {
            Alias = alias;
        }
    }
}
