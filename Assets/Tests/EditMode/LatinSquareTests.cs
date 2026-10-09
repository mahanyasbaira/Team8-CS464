using System.Collections.Generic;
using NUnit.Framework;
using Team8.Core;

namespace Team8.Tests
{
    public class LatinSquareTests
    {
        [Test]
        public void RowsMatchTheProposal()
        {
            CollectionAssert.AreEqual(
                new[] { Condition.Joystick, Condition.Teleport, Condition.Trail }, Conditions(1));
            CollectionAssert.AreEqual(
                new[] { Condition.Teleport, Condition.Trail, Condition.Joystick }, Conditions(2));
            CollectionAssert.AreEqual(
                new[] { Condition.Trail, Condition.Joystick, Condition.Teleport }, Conditions(3));
        }

        [Test]
        public void ParticipantsCycleThroughRows()
        {
            Assert.AreEqual(1, LatinSquare.RowFor(1));
            Assert.AreEqual(2, LatinSquare.RowFor(2));
            Assert.AreEqual(3, LatinSquare.RowFor(3));
            Assert.AreEqual(1, LatinSquare.RowFor(4));
            Assert.AreEqual(3, LatinSquare.RowFor(12));
        }

        [Test]
        public void LayoutsFollowPosition()
        {
            var order = LatinSquare.OrderFor(5);
            Assert.AreEqual('A', order[0].Layout);
            Assert.AreEqual('B', order[1].Layout);
            Assert.AreEqual('C', order[2].Layout);
            Assert.AreEqual(1, order[0].TrialNumber);
            Assert.AreEqual(3, order[2].TrialNumber);
        }

        [Test]
        public void EachConditionOncePerPositionAndPerLayout()
        {
            for (int pos = 0; pos < 3; pos++)
            {
                var seen = new HashSet<Condition>();
                for (int p = 1; p <= 3; p++) seen.Add(LatinSquare.OrderFor(p)[pos].Condition);
                Assert.AreEqual(3, seen.Count, "position " + (pos + 1));
            }
            for (int p = 1; p <= 3; p++)
            {
                var seen = new HashSet<Condition>(Conditions(p));
                Assert.AreEqual(3, seen.Count, "row " + p);
            }
        }

        [Test]
        public void BadParticipantNumberThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LatinSquare.RowFor(0));
        }

        [Test]
        public void IdFormatting()
        {
            Assert.AreEqual("P05", LatinSquare.FormatId(5));
            Assert.AreEqual("P12", LatinSquare.FormatId(12));
            Assert.AreEqual(5, LatinSquare.ParseId("P05"));
            Assert.AreEqual(12, LatinSquare.ParseId("p12"));
            Assert.AreEqual(7, LatinSquare.ParseId(" 7 "));
            Assert.AreEqual(-1, LatinSquare.ParseId("abc"));
            Assert.AreEqual(-1, LatinSquare.ParseId("P0"));
        }

        static Condition[] Conditions(int participant)
        {
            var order = LatinSquare.OrderFor(participant);
            var result = new Condition[order.Length];
            for (int i = 0; i < order.Length; i++) result[i] = order[i].Condition;
            return result;
        }
    }
}
