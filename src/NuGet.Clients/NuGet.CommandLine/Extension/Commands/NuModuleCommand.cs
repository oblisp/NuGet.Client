using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.Commands;
using NuGet.CommandLine;
using NuGet.ProjectManagement;
using NuGet.Extension.Common;
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

        [Option("NuModuleCommandSymbolsDescription")]
        public bool Symbols { get; set; }

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
            var moduleDependencies = resolveModuleDependencies(manifest, pathResolver);
            foreach(var module in moduleDependencies)
            {
                var modulePath = pathResolver.GetInstalledPath(module);
                if(String.IsNullOrEmpty(modulePath))
                {
                    throw new Exception(String.Format("Can not find module {0} in {1}", module.Id, installPath));
                }
                var moduleManifestPath = Path.Combine(modulePath, "manifest");
                NuModuleManifest moduleManifest = new NuModuleManifest(module.Id, module.Version.ToNormalizedString());
                moduleManifest.read(moduleManifestPath);
                globalDependencies.AddRange(moduleManifest.Libraries.Where(o => NuModuleConstants.SCOPE_COMPILE.Equals(o.Scope)).Select(o => o.Id));
            }

            Lazy<string> msbuildDirectory = MsBuildUtility.GetMsbuildDirectoryFromMsbuildPath(null, null, Console);
            var projectInfos = MsBuildUtility.GetAllProjectInfos(Solution, msbuildDirectory.Value);
            foreach (var projectInfo in projectInfos)
            {
                string projectPath = projectInfo[MsBuildUtility.PROJECT_PROPERTY_PATH];
                var packagesConfig = Path.Combine(Path.GetDirectoryName(projectPath), "packages.config");
                if(File.Exists(packagesConfig))
                {
                    using (FileStream fs = new FileStream(packagesConfig, FileMode.Open, FileAccess.Read))
                    {
                        PackagesConfigReader reader = new PackagesConfigReader(fs);
                        foreach (var pkg in reader.GetPackages())
                        {
                            var installedPath = pathResolver.GetInstalledPath(pkg.PackageIdentity);
                            if (globalDependencies.Contains(pkg.PackageIdentity))
                            {
                                manifest.removeLibrary(pkg.PackageIdentity);
                            }
                            if (!globalDependencies.Contains(pkg.PackageIdentity) && !String.IsNullOrEmpty(installedPath))
                            {
                                var message = String.Format("Adding libraray {0} from repository, Are you sure?", pkg.PackageIdentity.ToString());
                                Console.WriteWarning(message);
                                manifest.addLibrary(pkg.PackageIdentity);
                            }
                            else if (!globalDependencies.Contains(pkg.PackageIdentity) && String.IsNullOrEmpty(installedPath))
                            {
                                var message = String.Format("Can not find libraray {0}", pkg.PackageIdentity.ToString());
                                Console.WriteWarning(message);
                            }
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

            Lazy<string> msbuildDirectory = MsBuildUtility.GetMsbuildDirectoryFromMsbuildPath(null, null, Console);
            INuGetProjectContext projectCtx = new ConsoleProjectContext(Console);
            var projectInfos = MsBuildUtility.GetAllProjectInfos(Solution, msbuildDirectory.Value);
            Dictionary<string, MSBuildProjectSystem> projects = new Dictionary<string, MSBuildProjectSystem>();
            foreach (var projectInfo in projectInfos)
            {
                var projectPath = projectInfo[MsBuildUtility.PROJECT_PROPERTY_PATH];
                var ps = new MSBuildProjectSystem(msbuildDirectory.Value, projectPath, projectCtx);
                projects[Path.GetFileNameWithoutExtension(ps.ProjectName)] = ps;
            }

            var builder = createNuModulePackageBuilder(manifest, projects);
            builder.Build();
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

        private IEnumerable<PackageIdentity> resolveModuleDependencies(NuModuleManifest manifest, PackagePathResolver resolver)
        {
            Dictionary<string, NuModuleManifest> nuModules = new Dictionary<string, NuModuleManifest>();
            collectNuModulesByManifest(manifest, nuModules, resolver);
            IList<PackageIdentity> result = new List<PackageIdentity>();
            IList<ResolverPackage> toSort = new List<ResolverPackage>();
            foreach (var nuModule in nuModules.Values)
            {
                List<NuGet.Packaging.Core.PackageDependency> nuModuleDeps = new List<NuGet.Packaging.Core.PackageDependency>();
                foreach(var dep in nuModule.Dependencies)
                {
                    NuGet.Packaging.Core.PackageDependency nuModuleDep = new NuGet.Packaging.Core.PackageDependency(dep.Id.Id);
                    nuModuleDeps.Add(nuModuleDep);
                }
                toSort.Add(new ResolverPackage(nuModule.Name,new NuGetVersion(nuModule.Version), nuModuleDeps, true, false));
            }
            var sorted = ResolverUtility.TopologicalSort(toSort);
            foreach (var s in sorted)
            {
                result.Add(new PackageIdentity(s.Id, s.Version));
            }
            return result;
        }

        private void collectNuModulesByManifest(NuModuleManifest manifest, Dictionary<string, NuModuleManifest> nuModules, PackagePathResolver resolver)
        {
            foreach (var dep in manifest.Dependencies)
            {
                collectNuModulesByManifestEntry(dep, nuModules, resolver);
            }
        }

        private void collectNuModulesByManifestEntry(NuModuleManifestEntry entry, Dictionary<string, NuModuleManifest> nuModules, PackagePathResolver resolver)
        {
            var modulePath = resolver.GetInstalledPath(entry.Id);
            var moduleManifestPath = Path.Combine(modulePath, "manifest");
            if(!Directory.Exists(moduleManifestPath))
            {
                Console.WriteWarning(String.Format("Can not find module {0}", entry.Id.ToString()));
                return;
            }
            NuModuleManifest manifest = new NuModuleManifest(entry.Id.Id, entry.Id.Version.ToFullString());
            manifest.read(moduleManifestPath);
            if(nuModules.ContainsKey(manifest.Name))
            {
                return;
            }
            nuModules[manifest.Name] = manifest;
            collectNuModulesByManifest(manifest, nuModules, resolver);
        }

        private NuModulePackageBuilder createNuModulePackageBuilder(NuModuleManifest manifest, Dictionary<string, MSBuildProjectSystem> projects)
        {
            List<string> libraries = new List<string>();

            // collect files from solution
            PackArgs packArgs = new PackArgs();
            packArgs.Logger = Console;
            // packArgs.OutputDirectory = OutputDirectory;
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
            packArgs.Version = Version;

            NuModulePackageBuilder builder = new NuModulePackageBuilder(manifest, Console);
            foreach (var project in projects.Values)
            {
                // TODO
                packArgs.Arguments = new string[] { project.ProjectFileFullPath };
                packArgs.Path = PackCommandRunner.GetInputFile(packArgs);
                PackCommandRunner.SetupCurrentDirectory(packArgs);
                var factory = ProjectFactory.ProjectCreator(packArgs, Path.GetFullPath(Path.Combine(packArgs.CurrentDirectory, packArgs.Path)));
                factory.SetIncludeSymbols(packArgs.Symbols);
                var nupkgbuilder = factory.CreateBuilder(packArgs.BasePath, new NuGetVersion(Version), packArgs.Suffix, true, null);

                var libName = Path.GetFileNameWithoutExtension(project.ProjectName);
                builder.AddFiles(libName, nupkgbuilder.Files);
                libraries.Add(libName);
            }

            // collect files from repository
            // TODO install package if not exists
            var installPath = resolveInstallPath();
            PackagePathResolver pathResolver = new PackagePathResolver(installPath);
            foreach (var l in manifest.Libraries)
            {
                var libName = l.Id.Id;
                if (libraries.Contains(libName))
                {
                    // 已经从Solution中找到
                    continue;
                }

                var pkgPath = pathResolver.GetInstalledPath(l.Id);
                if(!Directory.Exists(pkgPath))
                {
                    var msg = String.Format("Can not find Library {0}", l.Id.ToString());
                    throw new Exception(msg);
                }

                var files = _findPackageFiles(pkgPath);
                builder.AddFiles(libName, files);
            }

            builder.OutputDirectory = Path.Combine(OutputDirectory, manifest.Name);
            builder.Symbols = Symbols;
            return builder;
        }

        private ICollection<NuGet.Packaging.IPackageFile> _findPackageFiles(string root)
        {
            List<NuGet.Packaging.IPackageFile> result = new List<Packaging.IPackageFile>();
            result.AddRange(this._findPackageFiles(root, "lib"));
            result.AddRange(this._findPackageFiles(root, "build"));
            result.AddRange(this._findPackageFiles(root, "content"));
            return result;
        }

        private ICollection<NuGet.Packaging.IPackageFile> _findPackageFiles(string root, string folder)
        {
            List<NuGet.Packaging.IPackageFile> result = new List<Packaging.IPackageFile>();
            var searchDir = Path.Combine(root, folder);
            if(Directory.Exists(searchDir))
            {
                List<string> subFiles = new List<string>();
                _collectPackageFiles(Path.Combine(root, folder), subFiles);
                foreach (var subFile in subFiles)
                {
                    var targetPath = subFile.Substring(root.Length);
                    if(targetPath.StartsWith("\\"))
                    {
                        if(targetPath.Length > 1)
                        {
                            targetPath = targetPath.Substring(1);
                        }
                        else
                        {
                            targetPath = null;
                        }
                    }
                    if (String.IsNullOrEmpty(targetPath))
                        continue;
                    var file = new NuGet.Packaging.PhysicalPackageFile()
                    {
                        SourcePath = subFile,
                        TargetPath = targetPath
                    };
                    result.Add(file);
                }
            }
            return result;
        }

        private void _collectPackageFiles(string root, ICollection<string> files)
        {
            foreach(var subFile in Directory.GetFiles(root))
            {
                files.Add(subFile);
            }
            foreach(var subDir in Directory.GetDirectories(root))
            {
                _collectPackageFiles(subDir, files);
            }
        }
    }
}
