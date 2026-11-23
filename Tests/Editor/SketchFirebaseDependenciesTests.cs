using System.Threading.Tasks;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    // OR-824: Firebase throws "Don't call other Firebase functions while CheckDependencies is running" when two modules
    // start CheckAndFixDependenciesAsync at the same time. Every module must share ONE check.
    public sealed class SketchFirebaseDependenciesTests
    {
        [SetUp] public void Reset() => SketchFirebaseDependencies.ResetForTests();

        [Test]
        public void Once_StartsTheCheckOnlyOnce_AndSharesTheTask()
        {
            int starts = 0;
            var source = new TaskCompletionSource<int>();
            Task<int> a = SketchFirebaseDependencies.Once(() => { starts++; return source.Task; });
            Task<int> b = SketchFirebaseDependencies.Once(() => { starts++; return source.Task; });
            Assert.AreEqual(1, starts);
            Assert.AreSame(a, b);
        }
    }
}
