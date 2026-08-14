using System;

namespace Carbon.Plugins;

// Supplies the attribute normally provided by Carbon's plugin compiler for local dotnet builds.
[AttributeUsage(AttributeTargets.Class)]
internal sealed class AutoPatchAttribute : Attribute
{
}

