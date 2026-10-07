using System.Runtime.CompilerServices;


// Tests
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Editor.Tests")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Internal.Editor.Tests")]

// Shard-Struck consumer (Oddlock fork)
[assembly: InternalsVisibleTo("Oddlock.Behavior.Editor")]
