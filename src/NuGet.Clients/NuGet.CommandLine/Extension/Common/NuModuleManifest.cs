using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using NuGet.Frameworks;

namespace NuGet.Extension.Common
{
    public class NuModuleManifest
    {
        public string Name { get; private set; }

        public string Version { get; private set; }

        public IList<NuModuleManifestEntry> Libraries { get; private set; }

        public IList<NuModuleManifestEntry> Dependencies { get; private set; }

        public NuModuleManifest(string name, string version)
        {
            Name = name;
            Version = version;
            Libraries = new List<NuModuleManifestEntry>();
            Dependencies = new List<NuModuleManifestEntry>();
        }

        public void addLibrary(PackageIdentity id)
        {
            addLibrary(id, NuModuleConstants.SCOPE_RUNTIME);
        }

        public void addLibrary(PackageIdentity id, string scope)
        {
            NuModuleManifestEntry entry = new NuModuleManifestEntry();
            entry.Id = id;
            if (Libraries.Contains(entry))
            {
                return;
            }
            entry.Scope = scope;
            Libraries.Add(entry);
        }

        public void addDependency(string id, string version)
        {
            PackageIdentity pkgId = new PackageIdentity(id, new Versioning.NuGetVersion(version));
            NuModuleManifestEntry entry = new NuModuleManifestEntry();
            entry.Id = pkgId;
            if (Dependencies.Contains(entry))
            {
                return;
            }
            Dependencies.Add(entry);
        }

        public void read(string path)
        {
            var xml = System.IO.Path.Combine(path, Name + ".xml");
            if(!File.Exists(xml))
            {
                return;
            }
            XmlDocument xmlDoc = new XmlDocument();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            XmlReader reader = XmlReader.Create(xml, settings);
            xmlDoc.Load(reader);

            var libraryNodes = xmlDoc.SelectNodes("/module/libraries/library");
            var libraryEnumerator = libraryNodes.GetEnumerator();
            while(libraryEnumerator.MoveNext())
            {
                var element = libraryEnumerator.Current as XmlElement;
                var idNode = element.SelectSingleNode("id");
                if (idNode == null)
                    continue;
                var id = idNode.InnerText.Trim();
                var versionNode = element.SelectSingleNode("version");
                if (versionNode == null)
                    continue;
                var version = versionNode.InnerText.Trim();
                PackageIdentity pkgId = new PackageIdentity(id, new Versioning.NuGetVersion(version));
                var scopeNode = element.SelectSingleNode("scope");
                var scope = NuModuleConstants.SCOPE_RUNTIME;
                if(scopeNode != null)
                {
                    scope = scopeNode.InnerText.Trim();
                }
                addLibrary(pkgId, scope);
            }

            var dependencyNodes = xmlDoc.SelectNodes("/module/dependencies/dependency");
            var dependencyEnumerator = dependencyNodes.GetEnumerator();
            while (dependencyEnumerator.MoveNext())
            {
                var element = dependencyEnumerator.Current as XmlElement;
                var idNode = element.SelectSingleNode("id");
                if (idNode == null)
                    continue;
                var id = idNode.InnerText.Trim();
                var versionNode = element.SelectSingleNode("version");
                if (versionNode == null)
                    continue;
                var version = versionNode.InnerText.Trim();
                addDependency(id, version);
            }
        }

        public void write(string path)
        {
            var xml = System.IO.Path.Combine(path, Name + ".xml");
            var xmlDir = Path.GetDirectoryName(xml);
            if(!Directory.Exists(xmlDir))
            {
                Directory.CreateDirectory(xmlDir);
            }
            using (FileStream fs = new FileStream(xml, FileMode.Create, FileAccess.Write))
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(fs, settings))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    var moduleNode = xmlDoc.CreateElement("module");
                    var idNode = xmlDoc.CreateElement("id");
                    idNode.InnerText = Name;
                    moduleNode.AppendChild(idNode);
                    var versionNode = xmlDoc.CreateElement("version");
                    versionNode.InnerText = Version;
                    moduleNode.AppendChild(versionNode);
                    var librariesNode = xmlDoc.CreateElement("libraries");
                    foreach (var library in Libraries)
                    {
                        var libNode = xmlDoc.CreateElement("library");

                        var libIdNode = xmlDoc.CreateElement("id");
                        libIdNode.InnerText = library.Id.Id;
                        libNode.AppendChild(libIdNode);

                        var libVersionNode = xmlDoc.CreateElement("version");
                        libVersionNode.InnerText = library.Id.Version.ToNormalizedString();
                        libNode.AppendChild(libVersionNode);

                        var libScopeNode = xmlDoc.CreateElement("scope");
                        libScopeNode.InnerText = library.Scope;
                        libNode.AppendChild(libScopeNode);

                        librariesNode.AppendChild(libNode);
                    }
                    moduleNode.AppendChild(librariesNode);
                    var dependenciesNode = xmlDoc.CreateElement("dependencies");
                    foreach (var dependency in Dependencies)
                    {
                        var depNode = xmlDoc.CreateElement("dependency");

                        var depIdNode = xmlDoc.CreateElement("id");
                        depIdNode.InnerText = dependency.Id.Id;
                        depNode.AppendChild(depIdNode);

                        var depVersionNode = xmlDoc.CreateElement("version");
                        depVersionNode.InnerText = dependency.Id.Version.ToNormalizedString();
                        depNode.AppendChild(depVersionNode);

                        dependenciesNode.AppendChild(depNode);
                    }
                    moduleNode.AppendChild(dependenciesNode);

                    xmlDoc.AppendChild(moduleNode);

                    xmlDoc.WriteTo(writer);
                }
            }
        }
    }
}
