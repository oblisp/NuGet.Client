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
        public PackageIdentity Id { get; private set; }

        public ISet<PackageContentItem> Content { get; set; }

        public PackageContent(PackageIdentity id)
        {
            Id = id;
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
