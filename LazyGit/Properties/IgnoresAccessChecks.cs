#pragma warning disable CS0436 // MonoMod has a type of the same name; the one here is used
using System;

[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("assembly_valheim")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("assembly_utils")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
        }
    }
}
