using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team8.Core;

namespace Team8.Tests
{
    // Uses the real lever spots and obstacles from GreyboxBuilder.
    public class RoomPlannerTests
    {
        static readonly (double x, double z)[][] Spots =
        {
            new[] { (6.5, 6.5), (1.5, 1.5), (6.0, 1.5) },
            new[] { (1.5, 6.5), (6.5, 6.0), (1.5, 1.5) },
            new[] { (6.5, 1.5), (1.5, 6.5), (6.5, 6.5) },
        };

        static IList<Box2> Center(int r) => RoomPlanner.CenterPieces(Spots[r], RoomPlanner.Kits(Spots[r]));

        [Test]
        public void SameKitShapeForEachLetterInEveryRoom()
        {
            for (int letter = 0; letter < 3; letter++)
            {
                var kits = Enumerable.Range(0, 3).Select(r => RoomPlanner.Kits(Spots[r])[letter]).ToList();
                Assert.That(kits.Select(k => k.Kind).Distinct().Count(), Is.EqualTo(1));
                Assert.That(kits.Select(k => (k.Cover.W, k.Cover.D, k.CoverHeight, k.LeverY)).Distinct().Count(), Is.EqualTo(1));
            }
        }

        [Test]
        public void KitsStayInsideTheRoomAndCoverTheLever()
        {
            for (int r = 0; r < 3; r++)
            foreach (var kit in RoomPlanner.Kits(Spots[r]))
            {
                var b = kit.Cover.Bounds();
                Assert.That(b.minX, Is.GreaterThan(0.1)); Assert.That(b.maxX, Is.LessThan(7.9));
                Assert.That(b.minZ, Is.GreaterThan(0.1)); Assert.That(b.maxZ, Is.LessThan(7.9));
                if (kit.Kind != KitKind.Behind)
                    Assert.IsTrue(kit.Cover.Contains(kit.LeverX, kit.LeverZ), "lever sits under/inside its cover");
                else
                    Assert.IsFalse(kit.Cover.Contains(kit.LeverX, kit.LeverZ), "lever sits behind, not inside, the cover");
            }
        }

        [Test]
        public void BehindCoverIsBetweenLeverAndRoomCentre()
        {
            var kit = RoomPlanner.Kit(1.5, 1.5, KitKind.Behind);
            Assert.That(RoomPlanner.Dist(kit.Cover.X, kit.Cover.Z, 4, 4), Is.LessThan(RoomPlanner.Dist(1.5, 1.5, 4, 4)));
        }

        [Test]
        public void WallFurnitureAvoidsLeversDoorsAndEachOther()
        {
            for (int r = 0; r < 3; r++)
            {
                var kits = RoomPlanner.Kits(Spots[r]);
                var slots = RoomPlanner.PerimeterSlots(r, Spots[r], kits, Center(r));
                Assert.That(slots.Count, Is.GreaterThanOrEqualTo(4), "room " + (r + 1));
                foreach (var s in slots)
                {
                    foreach (var p in Spots[r]) Assert.That(RoomPlanner.Dist(p.x, p.z, s.X, s.Z), Is.GreaterThanOrEqualTo(1.4));
                    if (s.Wall == 'E' || s.Wall == 'W') Assert.That(Math.Abs(s.Z - 4), Is.GreaterThanOrEqualTo(1.3));
                    if (s.Wall == 'S') Assert.That(Math.Abs(s.X - 4), Is.GreaterThanOrEqualTo(1.0));
                }
                for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                {
                    var a = new Box2(slots[i].X, slots[i].Z, 1.0, 0.5, slots[i].Yaw);
                    var b = new Box2(slots[j].X, slots[j].Z, 1.0, 0.5, slots[j].Yaw);
                    Assert.IsFalse(a.Overlaps(b), "slots " + i + " and " + j + " overlap in room " + (r + 1));
                }
            }
        }

        [Test]
        public void CenterPieceFitsEveryRoomAndClearsTheKits()
        {
            for (int r = 0; r < 3; r++)
            {
                var kits = RoomPlanner.Kits(Spots[r]);
                var center = RoomPlanner.CenterPieces(Spots[r], kits);
                Assert.That(center.Count, Is.EqualTo(1), "room " + (r + 1));
                foreach (var k in kits) Assert.IsFalse(center[0].Overlaps(k.Cover, 0.5));
            }
        }

        [Test]
        public void SpreadKeepsTheRequestedCount()
        {
            var kits = RoomPlanner.Kits(Spots[0]);
            var slots = RoomPlanner.PerimeterSlots(0, Spots[0], kits, Center(0));
            Assert.That(RoomPlanner.Spread(slots, 3).Count, Is.EqualTo(3));
            Assert.That(RoomPlanner.Spread(slots, 99).Count, Is.EqualTo(slots.Count));
            Assert.That(RoomPlanner.Spread(new List<Slot>(), 3).Count, Is.EqualTo(0));
        }

        [Test]
        public void DecoysStayAwayFromLevers()
        {
            for (int r = 0; r < 3; r++)
            {
                var kits = RoomPlanner.Kits(Spots[r]);
                var slots = RoomPlanner.PerimeterSlots(r, Spots[r], kits, Center(r));
                var decoys = RoomPlanner.DecoySlots(Spots[r], kits, Center(r), slots, 6);
                Assert.That(decoys.Count, Is.EqualTo(6), "room " + (r + 1));
                foreach (var d in decoys)
                {
                    foreach (var p in Spots[r]) Assert.That(RoomPlanner.Dist(p.x, p.z, d.x, d.z), Is.GreaterThanOrEqualTo(1.5));
                    foreach (var k in kits) Assert.IsFalse(k.Cover.Contains(d.x, d.z));
                }
            }
        }

        [Test]
        public void Box2CornersRotate()
        {
            var b = new Box2(0, 0, 2, 1, 90).Bounds();
            Assert.That(b.maxX - b.minX, Is.EqualTo(1).Within(1e-9));
            Assert.That(b.maxZ - b.minZ, Is.EqualTo(2).Within(1e-9));
        }
    }
}
