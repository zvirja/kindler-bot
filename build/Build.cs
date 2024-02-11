using System;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Serilog.Log;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.CompleteBuild);

    [Solution] readonly Solution Solution;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter(Name = "BuildVersion")]
    readonly string BuildVersionParam = "git";

    [Parameter(Name = "BuildNumber")]
    readonly int BuildNumberParam = 0;

    readonly string DockerImageName = "docker.zvirja.com/kindler-bot";

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath ArtifactsDir => RootDirectory / "artifacts";

    AbsolutePath Dockerfile => RootDirectory / "build" / "docker" / "Dockerfile";

    BuildVersionInfo CurrentBuildVersion;

    Target CalculateVersion => _ => _
        .Executes(() =>
        {
            Information($"Build version: {BuildVersionParam}");

            CurrentBuildVersion = BuildVersionParam switch
            {
                "git" => GitBasedVersion.CalculateVersionFromGit(BuildNumberParam),

                var ver => new BuildVersionInfo {AssemblyVersion = ver, FileVersion = ver, InfoVersion = ver, NuGetVersion = ver}
            };

            Information($"Calculated version: {CurrentBuildVersion}");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            ArtifactsDir.CreateOrCleanDirectory();
        });

    Target Prepare => _ => _
        .DependsOn(CalculateVersion, Clean)
        .Executes(() => { });

    Target Compile => _ => _
        .DependsOn(Prepare)
        .Executes(() =>
        {
            DotNetBuild(c => c
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetProperty("AssemblyVersion", CurrentBuildVersion.AssemblyVersion)
                .SetProperty("FileVersion", CurrentBuildVersion.FileVersion)
                .SetProperty("InformationalVersion", CurrentBuildVersion.InfoVersion)
                .SetProperty("GitSha", CurrentBuildVersion.GitSha)
                .SetVerbosity(DotNetVerbosity.minimal)
            );
        });

    Target Publish => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (var (rid, path) in new[] { ("linux-arm64", "linux/arm64"), ("linux-x64", "linux/amd64") })
            {
                DotNetPublish(c => c
                    .SetProject(Solution)
                    .SetConfiguration(Configuration)
                    .SetVersion(CurrentBuildVersion.NuGetVersion)
                    .SetProperty("PublishDir", ArtifactsDir / path)
                    .SetPublishReadyToRun(true)
                    .SetRuntime(rid)
                    .SetProperty("AssemblyVersion", CurrentBuildVersion.AssemblyVersion)
                    .SetProperty("FileVersion", CurrentBuildVersion.FileVersion)
                    .SetProperty("InformationalVersion", CurrentBuildVersion.InfoVersion)
                    .SetProperty("GitSha", CurrentBuildVersion.GitSha)
                    .SetVerbosity(DotNetVerbosity.minimal)
                );
            }
        });

    Target BuildDocker => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            // From https://docs.docker.com/build/building/multi-platform/#qemu
            DockerRun(c => c
                .SetImage("tonistiigi/binfmt")
                .SetArgs("--install", "all")
                .EnablePrivileged()
                .EnableRm());

            var builderExists = Docker("buildx ls").Any(x => x.Text.StartsWith("kindler"));
            if (builderExists)
            {
                Docker("buildx rm kindler");
            }

            Docker("buildx create --name kindler");

            DockerBuildxBuild(c => c
                .SetBuilder("kindler")
                .SetFile(Dockerfile)
                .SetTag(DockerImageName)
                .SetPath(ArtifactsDir)
                .SetPlatform("linux/amd64,linux/arm64")
            );
        });

    Target PushDocker => _ => _
        .DependsOn(BuildDocker)
        .Executes(() =>
        {
            // Workaround for https://github.com/docker/buildx/issues/59
            // Also: https://uninterrupted.tech/blog/creating-docker-images-that-can-run-on-different-platforms-including-raspberry-pi/
            DockerBuildxBuild(c => c
                .SetBuilder("kindler")
                .SetFile(Dockerfile)
                .SetTag(DockerImageName)
                .SetPath(ArtifactsDir)
                .SetPlatform("linux/amd64,linux/arm64")
                .EnablePush()
            );
        });

    Target CompleteBuild => _ => _
        .DependsOn(BuildDocker)
        .Executes(() =>
        {
            var builderExists = Docker("buildx ls").Any(x => x.Text.StartsWith("kindler"));
            if (builderExists)
            {
                Docker("buildx rm kindler");
            }
        });

    // ==============================================
    // ================== AppVeyor ==================
    // ==============================================

    static AppVeyor AppVeyorEnv => AppVeyor.Instance ?? throw new InvalidOperationException("Is not AppVeyor CI");

    Target AppVeyor_DescribeState => _ => _
        .Before(Prepare)
        .Executes(() =>
        {
            var env = AppVeyorEnv;
            var trigger = ResolveAppVeyorTrigger();
            Information($"Is tag: {env.RepositoryTag}, tag name: '{env.RepositoryTagName}', PR number: {env.PullRequestNumber?.ToString() ?? "<null>"}, branch name: '{env.RepositoryBranch}', trigger: {trigger}");
        });

    Target AppVeyor_Pipeline => _ => _
        .DependsOn(ResolveAppVeyorTarget(this), AppVeyor_DescribeState)
        .Executes(() =>
        {
            var trigger = ResolveAppVeyorTrigger();
            if (trigger != AppVeyorTrigger.PR)
            {
                AppVeyorEnv.UpdateBuildVersion(CurrentBuildVersion.FileVersion);
                Information($"Updated build version to: '{CurrentBuildVersion.FileVersion}'");
            }
        });

    static Target ResolveAppVeyorTarget(Build build)
    {
        var trigger = ResolveAppVeyorTrigger();
        return trigger switch
        {
            AppVeyorTrigger.SemVerTag  => build.PushDocker,
            AppVeyorTrigger.MainBranch => build.PushDocker,
            AppVeyorTrigger.PR         => build.BuildDocker,
            _                          => build.BuildDocker
        };
    }

    enum AppVeyorTrigger
    {
        Invalid,
        SemVerTag,
        PR,
        MainBranch,
        UnknownBranchOrTag
    }

    static AppVeyorTrigger ResolveAppVeyorTrigger()
    {
        var env = AppVeyor.Instance;
        if (env == null)
        {
            return AppVeyorTrigger.Invalid;
        }

        var tag = env.RepositoryTag ? env.RepositoryTagName : null;
        var isPr = env.PullRequestNumber != null;
        var branchName = env.RepositoryBranch;

        return (tag, isPr, branchName) switch
        {
            ({ } t, _, _) when Regex.IsMatch(t, "^v\\d.*") => AppVeyorTrigger.SemVerTag,
            (_, true, _)                                   => AppVeyorTrigger.PR,
            (_, _, "main")                                 => AppVeyorTrigger.MainBranch,
            _                                              => AppVeyorTrigger.UnknownBranchOrTag
        };
    }
}
