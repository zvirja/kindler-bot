using System;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
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
        .Triggers(CleanDockerBuilder)
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

    Target CleanDockerBuilder => _ => _
        .After(BuildDocker, PushDocker)
        .Executes(() =>
        {
            var builderExists = Docker("buildx ls").Any(x => x.Text.StartsWith("kindler"));
            if (builderExists)
            {
                Docker("buildx rm kindler");
            }
        });

    Target CompleteBuild => _ => _
        .DependsOn(BuildDocker);

    // ==============================================
    // ===================== CI =====================
    // ==============================================

    Target CI_DescribeState => _ => _
      .Before(Prepare)
      .Executes(() =>
      {
        var env = GitHubActions.Instance ?? throw new InvalidOperationException("Is not GitHub Actions CI");
        var trigger = ResolveCITrigger();
        Log.Information($"Build type: {env.RefType}, Ref name: '{env.RefName}', Is PR: {env.IsPullRequest}, trigger: {trigger}");
      });

    Target CI_Pipeline => _ => _
        .DependsOn(ResolveCITarget(this), CI_DescribeState);

    static Target ResolveCITarget(Build build)
    {
        var trigger = ResolveCITrigger();
        return trigger switch
        {
            CITrigger.SemVerTag        => build.PushDocker,
            CITrigger.MainBranch       => build.PushDocker,
            CITrigger.PR               => build.BuildDocker,
            _                          => build.BuildDocker
        };
    }

    enum CITrigger
    {
        Invalid,
        SemVerTag,
        PR,
        MainBranch,
        UnknownBranchOrTag
    }

    static CITrigger ResolveCITrigger()
    {
        var env = GitHubActions.Instance;
        if (env == null)
        {
            return CITrigger.Invalid;
        }

        var tag = env.RefType == "tag" ? env.RefName : null;
        var isPr = env.IsPullRequest;
        var branchName = env.RefName;

        return (tag, isPr, branchName) switch
        {
            (tag: { } t, _, _) when Regex.IsMatch(t, "^v\\d.*") => CITrigger.SemVerTag,
            (_, isPr: true, _)                                  => CITrigger.PR,
            (_, _, branchName: "main")                          => CITrigger.MainBranch,
            _                                                   => CITrigger.UnknownBranchOrTag
        };
    }
}
