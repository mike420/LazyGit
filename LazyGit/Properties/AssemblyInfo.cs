using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using LazyGit;

[assembly: AssemblyTitle(LazyGitMod.PluginName)]
[assembly: AssemblyDescription(LazyGitMod.PluginLink)]
[assembly: AssemblyCompany(LazyGitMod.PluginAuthor)]
[assembly: AssemblyProduct(LazyGitMod.PluginName)]
[assembly: AssemblyCopyright(LazyGitMod.PluginCopy)]
[assembly: ComVisible(false)]
[assembly: Guid("c91f3a58-2d7e-4b06-a4f1-8e5b0d6a7c24")]
[assembly: AssemblyVersion(LazyGitMod.PluginVersion)]
[assembly: AssemblyFileVersion(LazyGitMod.PluginVersion)]

// The game runs this mod, not the copy of the game it was built against: members the game keeps protected or private
// (m_hiddenLeftItem, GetRightItem...) can only be used if the assembly asks for verification to be skipped. The
// original mods that use such members carry exactly these (and IgnoresAccessChecksTo, see IgnoresAccessChecks.cs).
#pragma warning disable CS0618 // obsolete, but this is what makes the runtime skip the access checks
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]
