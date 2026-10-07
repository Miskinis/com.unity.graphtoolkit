using System.Runtime.CompilerServices;

// Dev project
[assembly: InternalsVisibleTo("GraphToolkitTestProject")]

// UGTK
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Internal.Editor")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Editor")]

// UGTK Tests
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Editor.Tests.CommandStateObserver")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Testing.Editor")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Internal.Editor.Tests")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Editor.Tests.Performance")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Editor.Tests.UI")]

// Samples (Todo: Should remove these when the public samples use the public api)
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.VisualNovelDirector.Editor")]

// Unity users
[assembly: InternalsVisibleTo("Unity.Motion.Editor")]
[assembly: InternalsVisibleTo("Unity.Motion.Editor.Tests")]

// Ancient Privateers consumer (Shard-Struck fork). The consumer's undo path dispatches the fork's
// undoable SetInspectedGraphModelFieldCommand through RootView.Dispatch(ICommand); this grant makes
// the command-state-observer internals that path flows through an explicit friend surface. (The
// consumer's asmdef references Unity.CommandStateObserver directly so the dispatch signature
// resolves.)
[assembly: InternalsVisibleTo("Oddlock.Behavior.Editor")]

// Test Samples
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.BlackboardSample")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.ContextSample")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.ImportedGraphEditor")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.ItemLibrary")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.RecipesEditor")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.SimpleMathBook")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.StateMachine")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.TestSample")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.VerticalFlow")]
[assembly: InternalsVisibleTo("Unity.GraphToolkit.Samples.SampleSupport")]
