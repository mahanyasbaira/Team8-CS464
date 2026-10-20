using NUnit.Framework;
using Team8.Core;

namespace Team8.Tests
{
    public class GrabLogTests
    {
        [Test]
        public void CountsUniquePropsButAllGrabs()
        {
            var log = new GrabLog();
            Assert.IsTrue(log.Add("mug", 3.0));
            Assert.IsFalse(log.Add("mug", 9.0));
            Assert.IsTrue(log.Add("book", 12.5));
            Assert.AreEqual(2, log.Count);
            Assert.AreEqual(3, log.TotalGrabs);
            Assert.AreEqual(3.0, log.FirstTime("mug"));
            Assert.IsNull(log.FirstTime("crate"));
        }

        [Test]
        public void EmptyNamesAreIgnored()
        {
            var log = new GrabLog();
            Assert.IsFalse(log.Add("", 1));
            Assert.IsFalse(log.Add(null, 1));
            Assert.AreEqual(0, log.TotalGrabs);
        }

        [Test]
        public void SummaryShortensLongLists()
        {
            var log = new GrabLog();
            Assert.AreEqual("none", log.Summary());
            foreach (var p in new[] { "a", "b", "c" }) log.Add(p, 0);
            Assert.AreEqual("a, b, c", log.Summary());
            Assert.AreEqual("a, b (+1)", log.Summary(2));
        }

        [Test]
        public void ClearResets()
        {
            var log = new GrabLog();
            log.Add("mug", 1);
            log.Clear();
            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(0, log.TotalGrabs);
            Assert.IsTrue(log.Add("mug", 2));
        }
    }
}
