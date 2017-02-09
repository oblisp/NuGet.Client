using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.CommandLine;

namespace NuGet.Extension.Common
{
    public class NuModulePackageBuilder
    {
        private static string FOLDER_CONTENT = "content";

        private static string FOLDER_LIBRARY = "lib";

        private static string FOLDER_SOURCE = "src";

        private static string FOLDER_RUNTIME = "runtime";

        private static string FOLDER_MANIFEST = "manifest";

        private static string PATH_SEPERATOR = "\\";

        private Dictionary<string, ICollection<NuGet.Packaging.IPackageFile>> _files = new Dictionary<string, ICollection<Packaging.IPackageFile>>();

        public Dictionary<string, ICollection<NuGet.Packaging.IPackageFile>> Files
        {
            get { return _files; }
        }

        public string OutputDirectory { get; set; }

        public bool Symbols { get; set; }

        public NuModuleManifest Manifest { get; private set; }

        public NuGet.CommandLine.IConsole Console { get; set; }

        private Dictionary<string, NuModuleManifestEntry> Libraries { get; set; }

        private Manifest NuSpec { get; set; }

        public NuModulePackageBuilder(NuModuleManifest manifest, NuGet.CommandLine.IConsole console)
        {
            Manifest = manifest;
            Libraries = new Dictionary<string, NuModuleManifestEntry>();
            foreach (var library in Manifest.Libraries)
            {
                Libraries[library.Id.Id] = library;
            }
            Console = console;
        }

        public void AddFiles(string library, ICollection<NuGet.Packaging.IPackageFile> files)
        {
            if (files == null)
                return;
            Files.Add(library, files);
        }

        public void Build()
        {
            if(String.IsNullOrEmpty(OutputDirectory))
            {
                throw new Exception("OutputDirectory must not be empty");
            }
            _cleanUp();
            _createNuspec();
            foreach (var l in Files.Keys)
            {
                foreach (var file in Files[l])
                {
                    if (file.Path.StartsWith(FOLDER_LIBRARY + PATH_SEPERATOR))
                    {
                        _copyLibraryFile(l, file);
                    }
                    else if (file.Path.StartsWith(FOLDER_SOURCE + PATH_SEPERATOR))
                    {
                        _copySourceFile(file);
                    }
                    else if (file.Path.StartsWith(FOLDER_CONTENT + PATH_SEPERATOR))
                    {
                        _copyContentFile(file);
                    }
                }
            }
            // copy content, lib, runtime, manifest
            // generate nuspec
            // packageBuilder build from nuspec
            _createNupkg();
        }

        private void _createNuspec()
        {
            NuSpec = new Manifest();
            NuSpec.Metadata.Id = Manifest.Name;
            NuSpec.Metadata.Version = Manifest.Version;
            NuSpec.Metadata.Description = Manifest.Name;
            NuSpec.Metadata.Authors = "PKU-HIT";
            NuSpec.Metadata.Tags = "IIH";
            NuSpec.Metadata.Copyright = "Copyright " + DateTime.Now.Year;
            // add dependencies
            if (Manifest.Dependencies != null)
            {
                List<ManifestDependency> dependecyList = new List<ManifestDependency>();
                foreach (var dep in Manifest.Dependencies)
                {
                    ManifestDependency dependency = new ManifestDependency();
                    dependency.Id = dep.Id.Id;
                    dependency.Version = dep.Id.Version.ToFullString();
                    dependecyList.Add(dependency);
                }
                ManifestDependencySet depSet = new ManifestDependencySet();
                depSet.Dependencies = dependecyList;
                // TODO targetFramework
                // depSet.TargetFramework 
                List<ManifestDependencySet> depSets = new List<ManifestDependencySet>();
                depSets.Add(depSet);
                NuSpec.Metadata.DependencySets = depSets;
            }

            NuSpec.Files = new List<ManifestFile>();
            _addNuSpecFile(FOLDER_MANIFEST);
        }

        private void _createFolderIfNeed(string name)
        {
            var folderPath = Path.Combine(OutputDirectory, name);
            if(!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _addNuSpecFile(name);
            }
        }

        private void _addNuSpecFile(string name)
        {
            ManifestFile file = new ManifestFile();
            file.Source = name + "\\**";
            file.Target = name;
            NuSpec.Files.Add(file);
        }

        private void _cleanUp()
        {
            var folders = new string[] { FOLDER_CONTENT, FOLDER_LIBRARY, FOLDER_RUNTIME, FOLDER_SOURCE};
            foreach(var folder in folders)
            {
                var folderPath = Path.Combine(OutputDirectory, folder);
                if(Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
            }
        }

        private void _copyLibraryFile(string libName, NuGet.Packaging.IPackageFile file)
        {
            if (!Libraries.ContainsKey(libName))
                return;
            var libEntry = Libraries[libName];
            if(NuModuleConstants.SCOPE_COMPILE.Equals(libEntry.Scope))
            {
                _createFolderIfNeed(FOLDER_LIBRARY);
                _copyFile(file, FOLDER_LIBRARY);
            }
            else if (NuModuleConstants.SCOPE_RUNTIME.Equals(libEntry.Scope))
            {
                _createFolderIfNeed(FOLDER_RUNTIME);
                _copyFile(file, FOLDER_RUNTIME);
            }
        }

        private void _copySourceFile(NuGet.Packaging.IPackageFile file)
        {
            _createFolderIfNeed(FOLDER_SOURCE);
            _copyFile(file, FOLDER_SOURCE);
        }

        private void _copyContentFile(NuGet.Packaging.IPackageFile file)
        {
            // TODO 常量化
            if(file.Path.StartsWith(FOLDER_CONTENT + "\\modules\\"))
            {
                _createFolderIfNeed(FOLDER_RUNTIME);
                _copyFile(file, Path.Combine(FOLDER_RUNTIME, "all"));
            }
        }

        private void _copyFile(NuGet.Packaging.IPackageFile file, string folder)
        {
            var start = file.Path.IndexOf(PATH_SEPERATOR);
            if(start < 0 || start + 1 >= file.Path.Length)
            {
                return;
            }
            var filePath = Path.Combine(folder, file.Path.Substring(start + 1));
            var dest = Path.Combine(OutputDirectory, filePath);
            var destDir = Path.GetDirectoryName(dest);
            if(!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            Console.WriteLine(String.Format("Copying file {0}", dest));
            using (Stream stream = file.GetStream())
            using(FileStream output = new FileStream(dest, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(output, 1024 * 32);
            }
        }

        private void _createNupkg()
        {
            string nuspecFile = Path.Combine(OutputDirectory, Manifest.Name + Constants.ManifestExtension);
            using (FileStream fs = new FileStream(nuspecFile, FileMode.Create))
            {
                NuSpec.Save(fs, false);
            }

            PackArgs packArgs = new PackArgs();
            packArgs.Logger = Console;
            packArgs.OutputDirectory = OutputDirectory;
            packArgs.BasePath = null;
            packArgs.MsBuildDirectory = MsBuildUtility.GetMsbuildDirectoryFromMsbuildPath(null, null, Console);
            packArgs.Build = false;
            packArgs.Exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            packArgs.ExcludeEmptyDirectories = false;
            packArgs.IncludeReferencedProjects = false;
            packArgs.LogLevel = NuGet.Common.LogLevel.Minimal;
            packArgs.MinClientVersion = null;
            packArgs.NoDefaultExcludes = false;
            packArgs.NoPackageAnalysis = false;
            packArgs.Suffix = null;
            packArgs.Symbols = Symbols;
            packArgs.Tool = false;
            // TODO
            packArgs.Version = Manifest.Version;

            packArgs.Arguments = new string[] { nuspecFile };
            packArgs.Path = PackCommandRunner.GetInputFile(packArgs);
            PackCommandRunner.SetupCurrentDirectory(packArgs);

            PackCommandRunner packCommandRunner = new PackCommandRunner(packArgs, ProjectFactory.ProjectCreator);
            packCommandRunner.BuildPackage();
        }
    }
}
