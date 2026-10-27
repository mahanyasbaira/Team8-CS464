using System;
using System.Collections.Generic;
using System.Linq;

namespace Team8.Core
{
    // Footprint of a box on the floor (room-local metres). Yaw in degrees like Unity:
    // yaw 0 faces +z, yaw 90 faces +x. W is the side-to-side size, D the front-to-back size.
    public struct Box2
    {
        public double X, Z, W, D, Yaw;

        public Box2(double x, double z, double w, double d, double yaw)
        {
            X = x; Z = z; W = w; D = d; Yaw = yaw;
        }

        public (double x, double z)[] Corners()
        {
            double a = Yaw * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            var pts = new (double, double)[4];
            int i = 0;
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                double lx = sx * W / 2, lz = sz * D / 2;
                pts[i++] = (X + lx * c + lz * s, Z - lx * s + lz * c);
            }
            return pts;
        }

        // axis-aligned bounds, grown by margin on every side
        public (double minX, double maxX, double minZ, double maxZ) Bounds(double margin = 0)
        {
            var c = Corners();
            return (c.Min(p => p.x) - margin, c.Max(p => p.x) + margin, c.Min(p => p.z) - margin, c.Max(p => p.z) + margin);
        }

        public bool Contains(double x, double z, double margin = 0)
        {
            var b = Bounds(margin);
            return x >= b.minX && x <= b.maxX && z >= b.minZ && z <= b.maxZ;
        }

        public bool Overlaps(Box2 other, double margin = 0)
        {
            var a = Bounds(margin);
            var b = other.Bounds();
            return a.minX < b.maxX && a.maxX > b.minX && a.minZ < b.maxZ && a.maxZ > b.minZ;
        }
    }

    public enum KitKind { Under, Behind, Inside }

    // How one lever spot is hidden. Same sizes in every room, only the look changes.
    public class HideKit
    {
        public KitKind Kind;
        public Box2 Cover;
        public double CoverHeight;
        public double Clearance;          // open gap under the cover (Under kit)
        public double LeverX, LeverY, LeverZ, LeverYaw;
    }

    public struct Slot
    {
        public double X, Z, Yaw;          // yaw faces into the room
        public char Wall;                 // N, S, E, W
    }

    // Plans the dressing of one 8 x 8 m room from its lever spots and existing obstacles.
    // Pure math so it is unit tested; RoomDresser turns the plan into objects.
    public static class RoomPlanner
    {
        public const double RoomSize = 8.0;
        public const double SlotWidth = 1.0, SlotDepth = 0.5;
        public const double MinFurnitureToLever = 1.4, MinDecoyToLever = 1.5;

        // A = under, B = behind, C = inside, in every room
        public static readonly KitKind[] KitForSpot = { KitKind.Under, KitKind.Behind, KitKind.Inside };

        static (double x, double z) Facing(double x, double z)
        {
            double fx = RoomSize / 2 - x, fz = RoomSize / 2 - z, n = Math.Sqrt(fx * fx + fz * fz);
            return n < 1e-6 ? (0, 1) : (fx / n, fz / n);
        }

        static double YawOf(double fx, double fz) => Math.Atan2(fx, fz) * 180.0 / Math.PI;

        public static HideKit Kit(double spotX, double spotZ, KitKind kind)
        {
            var (fx, fz) = Facing(spotX, spotZ);   // toward the room centre
            double yaw = YawOf(fx, fz);
            var kit = new HideKit { Kind = kind, LeverYaw = yaw };
            switch (kind)
            {
                case KitKind.Under:   // lever on the floor under a low cover (bed / draped table / big table)
                    kit.Cover = new Box2(spotX, spotZ, 1.6, 1.0, yaw);
                    kit.CoverHeight = 0.55;
                    kit.Clearance = 0.35;
                    kit.LeverX = spotX; kit.LeverY = 0.0; kit.LeverZ = spotZ;
                    break;
                case KitKind.Behind:  // lever on the floor behind a waist-high cover (armchair / frames / sacks)
                    kit.Cover = new Box2(spotX + fx * 0.65, spotZ + fz * 0.65, 1.4, 0.6, yaw);
                    kit.CoverHeight = 1.15;
                    kit.LeverX = spotX; kit.LeverY = 0.0; kit.LeverZ = spotZ;
                    break;
                default:              // lever inside a cabinet whose opening faces away from the room centre
                    kit.Cover = new Box2(spotX, spotZ, 0.9, 0.6, yaw);
                    kit.CoverHeight = 1.3;
                    kit.LeverX = spotX - fx * 0.05; kit.LeverY = 0.45; kit.LeverZ = spotZ - fz * 0.05;
                    kit.LeverYaw = yaw + 180;   // handle points out of the opening
                    break;
            }
            return kit;
        }

        public static HideKit[] Kits(IList<(double x, double z)> spots)
        {
            var kits = new HideKit[spots.Count];
            for (int i = 0; i < spots.Count; i++)
                kits[i] = Kit(spots[i].x, spots[i].z, KitForSpot[i % KitForSpot.Length]);
            return kits;
        }

        public const double CenterW = 1.2, CenterD = 0.8, CenterH = 1.2;

        // One free-standing piece near the middle of every room (same size everywhere),
        // clear of the lever spots and their kits. Replaces the old greybox obstacles.
        public static List<Box2> CenterPieces(IList<(double x, double z)> spots, IList<HideKit> kits, int count = 1)
        {
            var cand = new List<Box2>();
            for (int i = 0; i <= 12; i++)
            for (int j = 0; j <= 12; j++)
            {
                double x = 2.0 + i / 3.0, z = 2.0 + j / 3.0;
                var b = new Box2(x, z, CenterW, CenterD, 0);
                if (spots.Any(s => Dist(s.x, s.z, x, z) < 1.6)) continue;
                if (kits.Any(k => b.Overlaps(k.Cover, 0.6))) continue;
                cand.Add(b);
            }
            var picked = new List<Box2>();
            foreach (var b in cand.OrderBy(c => Dist(c.X, c.Z, RoomSize / 2, RoomSize / 2)))
            {
                if (picked.Count >= count) break;
                if (picked.All(q => !b.Overlaps(q, 1.2))) picked.Add(b);
            }
            return picked;
        }

        // Doorways and the start area stay clear on every wall.
        static bool InDoorway(char wall, double t, int roomIndex)
        {
            switch (wall)
            {
                case 'E': return Math.Abs(t - 4) < 1.3;                 // gate / exit
                case 'W': return Math.Abs(t - 4) < 1.3;                 // previous gate, or entrance + start in room 1
                case 'S': return Math.Abs(t - 4) < 1.0;                 // corridor door
                default: return false;
            }
        }

        // Furniture spots along the walls, clear of doorways, lever spots, kits, obstacles and each other.
        public static List<Slot> PerimeterSlots(int roomIndex, IList<(double x, double z)> spots,
                                                IList<HideKit> kits, IList<Box2> obstacles)
        {
            var result = new List<Slot>();
            var taken = new List<Box2>();
            double off = 0.1 + SlotDepth / 2;
            foreach (char wall in new[] { 'N', 'W', 'S', 'E' })
            {
                for (double t = 0.9; t <= 7.11; t += 1.3)
                {
                    if (InDoorway(wall, t, roomIndex)) continue;
                    double x, z, yaw;
                    switch (wall)
                    {
                        case 'N': x = t; z = RoomSize - off; yaw = 180; break;
                        case 'S': x = t; z = off; yaw = 0; break;
                        case 'W': x = off; z = t; yaw = 90; break;
                        default: x = RoomSize - off; z = t; yaw = -90; break;
                    }
                    var box = new Box2(x, z, SlotWidth, SlotDepth, yaw);
                    if (spots.Any(s => Dist(s.x, s.z, x, z) < MinFurnitureToLever)) continue;
                    if (kits.Any(k => box.Overlaps(k.Cover, 0.4))) continue;
                    if (obstacles.Any(o => box.Overlaps(o, 0.3))) continue;
                    if (taken.Any(b => box.Overlaps(b, 0.05))) continue;
                    taken.Add(box);
                    result.Add(new Slot { X = x, Z = z, Yaw = yaw, Wall = wall });
                }
            }
            return result;
        }

        // Keep the same number of wall pieces in every room: take a spread-out subset.
        public static List<Slot> Spread(IList<Slot> slots, int count)
        {
            var picked = new List<Slot>();
            if (slots.Count == 0 || count <= 0) return picked;
            picked.Add(slots[0]);
            while (picked.Count < Math.Min(count, slots.Count))
            {
                Slot best = slots[0];
                double bestD = -1;
                foreach (var s in slots)
                {
                    if (picked.Contains(s)) continue;
                    double d = picked.Min(p => Dist(p.X, p.Z, s.X, s.Z));
                    if (d > bestD) { bestD = d; best = s; }
                }
                picked.Add(best);
            }
            return picked;
        }

        // Floor spots for grabbable decoys: away from lever spots, kits, obstacles, wall furniture and doorway lanes.
        public static List<(double x, double z)> DecoySlots(IList<(double x, double z)> spots, IList<HideKit> kits,
                                                           IList<Box2> obstacles, IList<Slot> furniture, int count)
        {
            var cand = new List<(double x, double z)>();
            for (double x = 1.0; x <= 7.01; x += 0.5)
            for (double z = 1.0; z <= 7.01; z += 0.5)
            {
                if (spots.Any(s => Dist(s.x, s.z, x, z) < MinDecoyToLever)) continue;
                if (kits.Any(k => k.Cover.Contains(x, z, 0.4))) continue;
                if (obstacles.Any(o => o.Contains(x, z, 0.35))) continue;
                if (furniture.Any(f => new Box2(f.X, f.Z, SlotWidth, SlotDepth, f.Yaw).Contains(x, z, 0.35))) continue;
                if ((x < 1.6 || x > 6.4) && Math.Abs(z - 4) < 1.3) continue;   // doorway lanes
                if (z < 1.3 && Math.Abs(x - 4) < 1.0) continue;                 // in front of the corridor door
                cand.Add((x, z));
            }
            var picked = new List<(double x, double z)>();
            if (cand.Count == 0) return picked;
            picked.Add(cand.OrderBy(p => Dist(p.x, p.z, 4, 4)).First());
            while (picked.Count < Math.Min(count, cand.Count))
                picked.Add(cand.OrderByDescending(p => picked.Min(q => Dist(p.x, p.z, q.x, q.z))).First());
            return picked;
        }

        public static double Dist(double ax, double az, double bx, double bz)
        {
            double dx = ax - bx, dz = az - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
