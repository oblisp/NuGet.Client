using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Frameworks;

namespace NuGet.Extension.Common
{
    public class NuModuleManifestEntry
    {
        public PackageIdentity Id { get; set; }

        public string Scope { get; set; }

        public override bool Equals(object obj)
        {
            var entry = obj as NuModuleManifestEntry;

            if (entry == null)
            {
                return false;
            }

            return Id.Equals(entry.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
