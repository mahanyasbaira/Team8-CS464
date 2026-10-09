using NUnit.Framework;
using Team8.Core;

namespace Team8.Tests
{
    // Same paths are used in analysis/test_analysis.py so both versions agree.
    public class GridBacktrackCounterTests
    {
        static GridBacktrackCounter Run(double[,] path, double size = 1.0, double hyst = 0.15)
        {
            var c = new GridBacktrackCounter(size, hyst);
            for (int i = 0; i < path.GetLength(0); i++) c.AddSample(path[i, 0], path[i, 1]);
            return c;
        }

        [Test]
        public void StayingInOneCell_NeverCounts()
        {
            var c = Run(new double[,] { { 0.5, 0.5 }, { 0.6, 0.5 }, { 0.4, 0.7 }, { 0.5, 0.5 } });
            Assert.AreEqual(0, c.Backtracks);
            Assert.AreEqual(1, c.CellsEntered);
            Assert.AreEqual(1, c.UniqueCells);
        }

        [Test]
        public void WalkingStraight_NoBacktracks()
        {
            var c = Run(new double[,] { { 0.5, 0.5 }, { 1.5, 0.5 }, { 2.5, 0.5 }, { 3.5, 0.5 } });
            Assert.AreEqual(0, c.Backtracks);
            Assert.AreEqual(4, c.CellsEntered);
        }

        [Test]
        public void GoingBack_CountsEachReEntry()
        {
            // A -> B -> C -> B -> A : B and A are re-entered
            var c = Run(new double[,] { { 0.5, 0.5 }, { 1.5, 0.5 }, { 2.5, 0.5 }, { 1.5, 0.5 }, { 0.5, 0.5 } });
            Assert.AreEqual(2, c.Backtracks);
            Assert.AreEqual(5, c.CellsEntered);
            Assert.AreEqual(3, c.UniqueCells);
        }

        [Test]
        public void ManySamplesInReEnteredCell_CountOnce()
        {
            var c = Run(new double[,] { { 0.5, 0.5 }, { 1.5, 0.5 }, { 0.5, 0.5 }, { 0.4, 0.5 }, { 0.3, 0.6 }, { 0.5, 0.5 } });
            Assert.AreEqual(1, c.Backtracks);
        }

        [Test]
        public void SwayingOnABoundary_IsIgnored()
        {
            // standing near x = 1.0, wobbling 10 cm either side
            var c = Run(new double[,] { { 0.95, 0.5 }, { 1.05, 0.5 }, { 0.92, 0.5 }, { 1.08, 0.5 }, { 0.97, 0.5 } });
            Assert.AreEqual(0, c.Backtracks);
            Assert.AreEqual(1, c.CellsEntered);
        }

        [Test]
        public void CrossingPastHysteresis_Switches()
        {
            var c = Run(new double[,] { { 0.95, 0.5 }, { 1.20, 0.5 }, { 0.80, 0.5 } });
            Assert.AreEqual(3, c.CellsEntered);
            Assert.AreEqual(1, c.Backtracks);
        }

        [Test]
        public void TeleportJump_OnlyLandingCellCounts()
        {
            // jump from (0,0) to (5,0) and back: the cells in between are never entered
            var c = Run(new double[,] { { 0.5, 0.5 }, { 5.5, 0.5 }, { 0.5, 0.5 } });
            Assert.AreEqual(3, c.CellsEntered);
            Assert.AreEqual(2, c.UniqueCells);
            Assert.AreEqual(1, c.Backtracks);
        }

        [Test]
        public void NegativeCoordinates_UseFloor()
        {
            Assert.AreEqual(-1, GridBacktrackCounter.CellOf(-0.2, 1.0));
            Assert.AreEqual(0, GridBacktrackCounter.CellOf(0.0, 1.0));
            Assert.AreEqual(2, GridBacktrackCounter.CellOf(1.0, 0.5));
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var c = Run(new double[,] { { 0.5, 0.5 }, { 1.5, 0.5 }, { 0.5, 0.5 } });
            c.Reset();
            Assert.AreEqual(0, c.Backtracks);
            Assert.AreEqual(0, c.CellsEntered);
            Assert.AreEqual(0, c.UniqueCells);
            Assert.IsFalse(c.HasCell);
        }
    }
}
