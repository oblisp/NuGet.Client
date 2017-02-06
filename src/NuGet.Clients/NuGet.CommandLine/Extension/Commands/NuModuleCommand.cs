using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.CommandLine;
using NuGet.ProjectManagement;
using NuGet.Extension.Common;
using NuGet.ProjectModel;
using NuGet.LibraryModel;
using NuGet.Versioning;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using NuGet.Resolver;
using NuGet.Configuration;


namespace NuGet.Extension.Commands
{
    [Export]
    [Command(
        "numodule",
        "Manage NuGet Based Modules",
        MinArgs = 0,
        MaxArgs = 1)]
    public class NuModleCommand : Command
    {
        [Option("NuModuleCommandIdDescription")]
        public String Id { get; set; }

        [Option("NuModuleCommandVersionDescription")]
        public String Version { get; set; }

        [Option("NuModuleCommandSolutionDescription")]
        public String Solution { get; set; }

        [Option("NuModuleCommandConfigurationDescription")]
        public String Configuration { get; set; }

        [Option("NuModuleCommandOutputDirectoryDescription")]
        public String OutputDirectory { get; set; }

        [Option("NuModuleCommandRepositoryPathDescription")]
        public String RepositoryPath { get; set; }

        public override void ExecuteCommand()
        {
            if(Solution == null || Solution.Length <= 0)
                throw new CommandLineException("Solution must not be empty");
            if (Id == null || Id.Length <= 0)
            {
                Id = Path.GetFileNameWithoutExtension(Solution);
            }
            if (Version == null || Version.Length <= 0)
            {
                Version = "0.2.5-b1";
            }
            if (Arguments.IsEmpty())
            {
                string message = "No sub command found";
                Console.WriteError(message);
                throw new CommandLineException(message);
            }
            string subCmd = Arguments.Last<string>();
            if("spec".Equals(subCmd))
            {
                handleSpecCmd();
            }
            else if ("pack".Equals(subCmd))
            {
                handlePackCmd();
            }
            else
            {
                var message = String.Format("Unknown sub command {0}", subCmd);
                throw new CommandLineException(message);
            }
        }

        private void handleSpecCmd()
        {
            NuModuleManifest manifest = new NuModuleManifest(Id, Version);
            var path = Path.Combine(OutputDirectory, Id, "manifest");
            manifest.read(path);

            var installPath = resolveInstallPath();
            PackagePathResolver pathResolver = new PackagePathResolver(installPath);

            ISet<PackageIdentity> globalDependencies = new HashSet<PackageIdentity>();
            var moduleDependencies = resolveModuleDependencies(manifest);
            foreach(var module in moduleDependencies)
            {
                var modulePath = pathResolver.GetInstalledPath(module);
                var moduleManifestPath = Path.Combine(modulePath, "manifest");
                NuModuleManifest moduleManifest = new NuModuleManifest(module.Id, module.Version.ToNormalizedString());
                moduleManifest.read(moduleManifestPath);
                globalDependencies.AddRange(moduleManifest.Libraries.Select((o)=> { return o.Id; }));
            }

            Lazy<string> msbuildDirectory = MsBuildUtility.GetMsbuildDirectoryFromMsbuildPath(null, null, Console);
            var projectInfos = MsBuildUtility.GetAllProjectInfos(Solution, msbuildDirectory.Value);
            foreach (var projectInfo in projectInfos)
            {
                string projectPath = projectInfo[MsBuildUtility.PROJECT_PROPERTY_PATH];
                var packagesConfig = Path.Combine(Path.GetDirectoryName(projectPath), "packages.config");
                if(!File.Exists(packagesConfig))
                {
                    continue;
                }
                using(FileStream fs = new FileStream(packagesConfig, FileMode.Open, FileAccess.Read))
                {
                    PackagesConfigReader reader = new PackagesConfigReader(fs);
                    foreach(var pkg in reader.GetPackages())
                    {
                        if(!globalDependencies.Contains(pkg.PackageIdentity))
                        {
                            var message = String.Format("Can nof reference package {0}", pkg.PackageIdentity.ToString());
                            Console.WriteWarning(message);
                        }
                        else
                        {
                            // manifest.addLibrary(pkg.PackageIdentity);
                        }
                    }
                }
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                manifest.addLibrary(new PackageIdentity(projectName, new NuGetVersion(Version)));
            }
            manifest.write(path);
        }

        private void handlePackCmd()
        {
            NuModuleManifest manifest = new NuModuleManifest(Id, Version);
            var modulePath = Path.Combine(OutputDirectory, Id);
            var path = Path.Combine(modulePath, "manifest");
            manifest.read(path);

            Manifest nuspec = new Manifest();
            string pkgName = manifest.Name;
            nuspec.Metadata.Id = pkgName;
            nuspec.Metadata.Version = Version;
            nuspec.Metadata.Description = pkgName;
            nuspec.Metadata.Authors = "PKU-HIT";
            nuspec.Metadata.Tags = "IIH";
            nuspec.Metadata.Copyright = "Copyright " + DateTime.Now.Year;
            // add dependencies
            if(manifest.Dependencies != null)
            {
                List<ManifestDependency> dependecyList = new List<ManifestDependency>();
                foreach (var dep in manifest.Dependencies)
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
                nuspec.Metadata.DependencySets = depSets;
            }

            ManifestFile lib = new ManifestFile();
            lib.Source = "lib\\**";
            lib.Target = "lib";
            ManifestFile runtime = new ManifestFile();
            runtime.Source = "runtime\\**";
            runtime.Target = "runtime";
            ManifestFile moduleManifest = new ManifestFile();
            moduleManifest.Source = "manifest\\**";
            moduleManifest.Target = "manifest";
            List<ManifestFile> files = new List<ManifestFile>();
            files.Add(lib);
            files.Add(runtime);
            files.Add(moduleManifest);
            nuspec.Files = files;
            string nuspecFile = Path.Combine(OutputDirectory, pkgName, pkgName + Constants.ManifestExtension);
            using (FileStream fs = new FileStream(nuspecFile, FileMode.Create))
            {
                nuspec.Save(fs, false);
            }

            var installPath = resolveInstallPath();
            PackagePathResolver pathResolver = new PackagePathResolver(installPath);
            var runtimePath = Path.Combine(modulePath, "runtime");
            if(Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, true);
            }
            if (!Directory.Exists(runtimePath))
            {
                Directory.CreateDirectory(runtimePath);
            }
            var libPath = Path.Combine(modulePath, "lib");
            if (Directory.Exists(libPath))
            {
                Directory.Delete(libPath, true);
            }
            if (!Directory.Exists(libPath))
            {
                Directory.CreateDirectory(libPath);
            }
            foreach(var l in manifest.Libraries)
            {
                var pkgDirName = pathResolver.GetPackageDirectoryName(l.Id);
                var pkgLibDir = Path.Combine(installPath, pkgDirName, "lib");
                if (String.IsNullOrEmpty(l.Scope) || NuModuleConstants.SCOPE_COMPILE.Equals(l.Scope))
                {
                    copyDirectory(pkgLibDir, libPath);
                }
                else if (NuModuleConstants.SCOPE_RUNTIME.Equals(l.Scope))
                {
                    copyDirectory(pkgLibDir, runtimePath);
                }
            }
        }

        private string resolveInstallPath()
        {
            string installPath = RepositoryPath;
            if (!String.IsNullOrEmpty(installPath))
            {
                return installPath;
            }

            installPath = SettingsUtility.GetRepositoryPath(Settings);
            if (!String.IsNullOrEmpty(installPath))
            {
                return installPath;
            }

            var solutionDir = Path.GetDirectoryName(Solution);
            if (!String.IsNullOrEmpty(solutionDir))
            {
                return Path.Combine(solutionDir, CommandLineConstants.PackagesDirectoryName);
            }

            return null;
        }

        private IEnumerable<PackageIdentity> resolveModuleDependencies(NuModuleManifest manifest)
        {
            IList<PackageIdentity> result = new List<PackageIdentity>();
            var dependencies = manifest.Dependencies;
            if(dependencies != null)
            {
                IList<ResolverPackage> toSort = new List<ResolverPackage>();
                foreach (var dependency in dependencies)
                {
                    toSort.Add(new ResolverPackage(dependency.Id.Id, dependency.Id.Version));
                }
                var sorted = ResolverUtility.TopologicalSort(toSort);
                foreach(var s in sorted)
                {
                    result.Add(new PackageIdentity(s.Id, s.Version));
                }
            }
            return result;
        }

        private void copyDirectory(string sPath, string dPath, bool force)
        {
            if (!Directory.Exists(sPath))
            {
                Console.WriteWarning(String.Format("Can not find src directory {0}.\n", sPath));
                return;
            }
            if(force)
            {
                Directory.Delete(dPath, true);
            }
            Console.WriteLine(String.Format("Copying directory {0} to {1}", sPath, dPath));
            string[] directories = System.IO.Directory.GetDirectories(sPath);
            if (!System.IO.Directory.Exists(dPath))
                System.IO.Directory.CreateDirectory(dPath);
            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(sPath);
            System.IO.DirectoryInfo[] dirs = dir.GetDirectories();
            FileInfo[] files = dir.GetFiles();
            foreach (System.IO.DirectoryInfo subDirectoryInfo in dirs)
            {
                string sourceDirectoryFullName = subDirectoryInfo.FullName;
                string destDirectoryFullName = sourceDirectoryFullName.Replace(sPath, dPath);
                copyDirectory(sourceDirectoryFullName, destDirectoryFullName);
            }
            foreach (FileInfo file in files)
            {
                string sourceFileFullName = file.FullName;
                string destFileFullName = sourceFileFullName.Replace(sPath, dPath);
                Console.WriteLine(String.Format("Copying file {0} to {1}", Path.GetFileName(sourceFileFullName), destFileFullName));
                file.CopyTo(destFileFullName, true);
            }
        }

        private void copyDirectory(string sPath, string dPath)
        {
            copyDirectory(sPath, dPath, false);
        }

        private void CopyFile(System.IO.DirectoryInfo path, string desPath)
        {
            string sourcePath = path.FullName;
            System.IO.FileInfo[] files = path.GetFiles();
            foreach (System.IO.FileInfo file in files)
            {
                string sourceFileFullName = file.FullName;
                string destFileFullName = sourceFileFullName.Replace(sourcePath, desPath);
                file.CopyTo(destFileFullName, true);
            }
        }
    }
}
