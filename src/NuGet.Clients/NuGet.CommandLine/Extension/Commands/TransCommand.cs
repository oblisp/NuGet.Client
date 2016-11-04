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


namespace NuGet.Extension.Commands
{
    [Export]
    [Command(
        "transform", 
        "transform solution's projects",
        MinArgs = 0, 
        MaxArgs = 1)]
    public class TransCommand : Command
    {
        [Option("TransCommandSolutionDescription")]
        public String Solution { get; set; }

        [Option("TransCommandBaseVersionDescription")]
        public String DefaultPackageVersion { get; set; }

        private string BuildDir { get; set; }

        private PackageContentManager packageContentManager;

        public override void ExecuteCommand()
        {
            Console.WriteLine("executing transform...");
            packageContentManager = new PackageContentManager(DefaultPackageVersion);
            BuildDir = Directory.GetParent(Solution).Parent.FullName;
            Lazy<string> msbuildDirectory = MsBuildUtility.GetMsbuildDirectoryFromMsbuildPath(null, null, Console);
            var projectInfos = MsBuildUtility.GetAllProjectInfos(Solution, msbuildDirectory.Value);
            Dictionary<string, Dictionary<string, string>> projects = new Dictionary<string, Dictionary<string, string>>();
            foreach(var projectInfo in projectInfos)
            {
                string path = projectInfo[MsBuildUtility.PROJECT_PROPERTY_PATH];
                path = Path.GetFileNameWithoutExtension(path);
                projects[path] = projectInfo;
            }
            INuGetProjectContext ctx = new ConsoleProjectContext(Console);
            foreach (var name in projects.Keys)
            {
                var project = projects[name];
                var path = project[MsBuildUtility.PROJECT_PROPERTY_PATH];
                var ps = new MSBuildProjectSystem(msbuildDirectory.Value, path, ctx);
                Console.WriteLine(ps.ProjectFileFullPath);
                ps.setProperty("Debug", "AnyCPU", "OutputPath", "bin\\Debug\\");
                ps.setProperty("Release", "AnyCPU", "OutputPath", "bin\\Release\\");
                ps.setProperty("Debug", "x86", "OutputPath", "bin\\Debug\\");
                ps.setProperty("Release", "x86", "OutputPath", "bin\\Release\\");
                ps.AddImport(Path.Combine(BuildDir, "CommonBuild.targets"), false, ImportLocation.Bottom);
                // ps.RemoveImport("E:\\workspace\\nuget\\CommonBuild.targets");
                string outputType = ps.GetPropertyValue("OutputType");
                if (!"Library".Equals(outputType))
                {
                    continue;
                }
                updateProjectReferences(ps, projects);
                // createPackagesConfig(name, ps, projects);
                // createProjectJson(ps, projectNames);
                createNuspec(ps);
            }
        }

        private void createNuspec(MSBuildProjectSystem ps)
        {
            Manifest manifest = new Manifest();
            string fileName = Path.GetFileNameWithoutExtension(ps.ProjectFileFullPath);
            string pkgName = transformPackageName(fileName);
            manifest.Metadata.Id = pkgName;
            manifest.Metadata.Version = "$version$";
            manifest.Metadata.Description = pkgName;
            manifest.Metadata.Authors = "PKU-HIT";

            manifest.Metadata.Tags = "IIH";
            manifest.Metadata.Copyright = "Copyright " + DateTime.Now.Year;
            string nuspecFile = Path.Combine(ps.ProjectFullPath, fileName + Constants.ManifestExtension);
            using (FileStream fs = new FileStream(nuspecFile, FileMode.Create))
            {
                manifest.Save(fs, false);
            }
        }

        private void updateProjectReferences(MSBuildProjectSystem project, Dictionary<string, Dictionary<string, string>> projectInfos)
        {
            // 工程引用
            List<dynamic> projectReferences = new List<dynamic>();
            // NuGet引用
            List<dynamic> packagesReferences = new List<dynamic>();

            var projectName = getProjectName(project);
            foreach (var reference in project.getReferences())
            {
                if(!isXAPClientDirectoryReference(project, reference))
                {
                    // 不是xaptools里的引用，直接跳过
                    continue;
                }
                string referenceName = getReferenceName(project, reference);
                if (projectInfos.Keys.Contains(referenceName))
                {
                    // 工程引用
                    projectReferences.Add(projectInfos[referenceName]);
                }
                else
                {
                    // 包引用
                    packagesReferences.Add(reference);
                }
            }
            createProjectReferences(project, projectReferences);
            createPackagesReferences(project, packagesReferences);
            project.Save();
        }

        private string getProjectName(MSBuildProjectSystem project)
        {
            return Path.GetFileNameWithoutExtension(project.ProjectName);
        }

        private string getReferenceName(MSBuildProjectSystem project, dynamic reference)
        {
            return project.getReferenceName(reference);
        }

        // 旧的xapclient目录里的引用
        private bool isXAPClientDirectoryReference(MSBuildProjectSystem project, dynamic reference)
        {
            string hintPath = null;
            foreach (var m in reference.DirectMetadata)
            {
                if ("HintPath".Equals(m.Name))
                {
                    hintPath = m.EvaluatedValue;
                }
            }
            string refName = project.getReferenceName(reference);
            return hintPath != null && hintPath.EndsWith("..\\..\\xapclient\\" + refName + ".dll");
        }

        private void createProjectReferences(MSBuildProjectSystem project, List<dynamic> projectReferences)
        {
            foreach(dynamic r in projectReferences)
            {
                createProjectReference(project, r);
            }
        }

        private void createProjectReference(MSBuildProjectSystem project, dynamic projectReference)
        {
            var path = projectReference[MsBuildUtility.PROJECT_PROPERTY_PATH];
            project.RemoveReference(Path.GetFileNameWithoutExtension(path) + ".dll");
            project.addProjectReference(path, projectReference[MsBuildUtility.PROJECT_PROPERTY_GUID]);
        }

        private void createPackagesReferences(MSBuildProjectSystem project, List<dynamic> packagesReferences)
        {
            HashSet<PackageIdentity> packageIdsToAdd = new HashSet<PackageIdentity>();
            HashSet<PackageContent> packagesToAdd = new HashSet<PackageContent>();
            foreach (dynamic reference in packagesReferences)
            {
                var refName = getReferenceName(project, reference);
                PackageContent pkg = packageContentManager.findPackageContent(refName);
                if(pkg == null)
                {
                    Console.WriteWarning("can not find package " + refName);
                    continue;
                }
                // 删除当前引用
                project.RemoveReference(refName + ".dll");
                // 映射到已知的库上去
                if(packageIdsToAdd.Contains(pkg.Id))
                {
                    continue;
                }
                packageIdsToAdd.Add(pkg.Id);
                packagesToAdd.Add(pkg);
            }
            createPackagesReferences(project, packagesToAdd);
        }

        private void createPackagesReferences(MSBuildProjectSystem project, HashSet<PackageContent> packages)
        {
            if (packages.Count > 0)
            {
                using (PackagesConfigWriter writer = new PackagesConfigWriter(Path.Combine(project.ProjectFullPath, "packages.config"), true))
                {
                    foreach (PackageContent package in packages)
                    {
                        createPackagesReference(project, package);
                        writer.AddPackageEntry(package.Id, project.TargetFramework);
                        if (package.Content == null)
                            continue;
                        foreach(PackageContentItem item in package.Content)
                        {
                            if(!item.isNugetPackage)
                            {
                                continue;
                            }
                            PackageIdentity pkgId = new PackageIdentity(item.Name, item.Version);
                            writer.AddPackageEntry(pkgId, project.TargetFramework);
                        }
                    }
                }
            }
        }

        private void createPackagesReference(MSBuildProjectSystem project, PackageContent package)
        {
            // basePath\[packageId].[packageVersion]\[framework]\lib\xxx.dll
            string solutionDir = Directory.GetParent(Solution).FullName;
            if(package.Content == null || package.Content.Count <= 0)
            {
                string referencePath = Path.Combine(
                    Directory.GetParent(solutionDir).FullName,
                    "packages",
                    package.Id.ToString(),
                    "lib",
                    project.TargetFramework.GetShortFolderName(),
                    package.Id.Id + ".dll");
                project.AddReference(referencePath);
            }
            else
            {
                foreach (PackageContentItem item in package.Content)
                {
                    string pkgName = item.isNugetPackage ? item.Name + "." + item.Version.ToString() : package.Id.ToString();
                    string dllName = item.Alias != null ? item.Alias : item.Name;
                    string referencePath = Path.Combine(
                        Directory.GetParent(solutionDir).FullName,
                        "packages",
                        pkgName,
                        "lib",
                        project.TargetFramework.GetShortFolderName(),
                        dllName + ".dll");
                    project.AddReference(referencePath);
                }
            }            
        }

        private void createProjectJson(MSBuildProjectSystem ps, HashSet<string> projectNames)
        {
            TargetFrameworkInformation framework = new TargetFrameworkInformation
            {
                FrameworkName = ps.TargetFramework,
                Dependencies = new List<LibraryDependency>(),
            };
            PackageSpec spec = new PackageSpec(new List<TargetFrameworkInformation>() { framework });
            spec.Name = this.transformPackageName(Path.GetFileNameWithoutExtension(ps.ProjectName));
            foreach (var r in ps.getReferences())
            {
                // Console.Write("\t");
                // Console.WriteLine(ps.getReferenceName(r));
                string hintPath = null;
                foreach (var m in r.DirectMetadata)
                {
                    if ("HintPath".Equals(m.Name))
                    {
                        hintPath = m.EvaluatedValue;
                    }
                }
                string refName = ps.getReferenceName(r);
                if (hintPath == null || !hintPath.EndsWith("..\\..\\xapclient\\" + refName + ".dll"))
                {
                    // Console.Write("\t");
                    // Console.WriteLine(refName);
                    continue;
                }
                // ps.RemoveReference(refName);
                String transformedRefName = transformPackageName(refName);
                Version refVersion = ps.getReferenceVersion(r);

                LibraryDependency dep = new LibraryDependency();
                LibraryRange libRange = null;
                if (projectNames.Contains(refName))
                {
                    libRange = new LibraryRange(transformedRefName, LibraryDependencyTarget.Project);
                }
                else if(refName.StartsWith("xap.") || refName.StartsWith("iih."))
                {
                    // TODO
                    NuGetVersion ngv = new NuGetVersion("0.2.0");
                    VersionRange versionRange = new VersionRange(ngv);
                    libRange = new LibraryRange(transformedRefName, versionRange, LibraryDependencyTarget.Package);
                }
                else if(refVersion != null)
                {
                    NuGetVersion ngv = new NuGetVersion(refVersion);
                    VersionRange versionRange = new VersionRange(ngv);
                    libRange = new LibraryRange(refName, versionRange, LibraryDependencyTarget.Package);
                }
                else
                {
                    NuGetVersion ngv = new NuGetVersion("0.0.0");
                    VersionRange versionRange = new VersionRange(ngv);
                    libRange = new LibraryRange(refName, versionRange, LibraryDependencyTarget.Package);
                }
                dep.LibraryRange = libRange;

                spec.Dependencies.Add(dep);
            }
            JsonPackageSpecWriter.WritePackageSpec(spec, Path.Combine(ps.ProjectFullPath, "project.json"));
        }

        private string transformPackageName(string name)
        {
            // name = name.ToUpper();
            /*
            if(name.StartsWith("XAP.") && name.Length > 4)
            {
                name = "IIH." + name.Substring(4);
            }*/
            return name;
        }
    }
}
