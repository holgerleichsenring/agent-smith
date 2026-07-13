using AgentSmith.SkillsPackaging;

// p0325: build-time entry point. Invoked by the EmbedSkillsCatalog MSBuild
// step with the pinned skills tarball; a non-zero exit fails the build with
// the offending master + reason on stderr (surfaced as an MSBuild error).
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: AgentSmith.SkillsPackaging <skills-release.tar.gz>");
    return 2;
}

using var tarball = File.OpenRead(args[0]);
var violations = new MasterDescriptionValidator().Validate(tarball);
if (violations.Count == 0)
    return 0;

foreach (var violation in violations)
    Console.Error.WriteLine($"skills catalog validation failed: master '{violation.Master}': {violation.Reason}");
return 1;
