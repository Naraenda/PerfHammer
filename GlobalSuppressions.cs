// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "Unity is not on latest C# version.", Scope = "module")]
[assembly: SuppressMessage("Style", "IDE0054:Use compound assignment", Justification = "Unity is not on latest C# version.", Scope = "module")]

[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity Event.", Scope = "member", Target = "~M:PerfHammerWindow.OnGUI")]

