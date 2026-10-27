using System;
using UnityEngine;

// Small building blocks for room themes. Every part is a primitive without a collider
// (pieces get one box collider from RoomDresser). Positions are local to the parent.
public class RoomPieces
{
    public int RoomIndex;  // 0..2, used to keep decor out of doorways
    System.Random rng = new System.Random(464);

    public Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        return Make(parent, PrimitiveType.Cube, name, center, size, mat);
    }

    // size = (diameter x, height, diameter z); rotZ lays it on its side
    public Transform Cyl(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float rotZ = 0)
    {
        var t = Make(parent, PrimitiveType.Cylinder, name, center, new Vector3(size.x, size.y / 2f, size.z), mat);
        t.localRotation = Quaternion.Euler(0, 0, rotZ);
        return t;
    }

    Transform Make(Transform parent, PrimitiveType type, string name, Vector3 center, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    public void Legs(Transform r, float w, float d, float h, Material mat)
    {
        float x = w / 2 - 0.05f, z = d / 2 - 0.05f;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            Box(r, "Leg", new Vector3(sx * x, h / 2, sz * z), new Vector3(0.06f, h, 0.06f), mat);
    }

    // open shelving unit; fill(y) is called for the top of each shelf board
    public void Shelves(Transform r, float w, float d, float h, int boards, Material mat, Action<float> fill)
    {
        Box(r, "Side_L", new Vector3(-w / 2 + 0.02f, h / 2, 0), new Vector3(0.04f, h, d), mat);
        Box(r, "Side_R", new Vector3(w / 2 - 0.02f, h / 2, 0), new Vector3(0.04f, h, d), mat);
        Box(r, "Back", new Vector3(0, h / 2, -d / 2 + 0.01f), new Vector3(w, h, 0.02f), mat);
        for (int i = 0; i < boards; i++)
        {
            float y = 0.05f + i * (h - 0.1f) / (boards - 1);
            Box(r, "Board" + i, new Vector3(0, y, 0), new Vector3(w - 0.04f, 0.03f, d), mat);
            if (i < boards - 1) fill?.Invoke(y + 0.015f);
        }
    }

    public void Books(Transform r, Vector3 at, int count, params Material[] mats)
    {
        float x = at.x - count * 0.045f;
        for (int i = 0; i < count; i++)
        {
            float h = 0.2f + (float)rng.NextDouble() * 0.1f, w = 0.035f + (float)rng.NextDouble() * 0.03f;
            Box(r, "Book", new Vector3(x + w / 2, at.y + h / 2, at.z), new Vector3(w, h, 0.16f), mats[i % mats.Length]);
            x += w + 0.005f;
        }
    }

    public void Jars(Transform r, Vector3 at, int count, Material mat, float spacing)
    {
        float x0 = at.x - (count - 1) * spacing / 2;
        for (int i = 0; i < count; i++)
        {
            float h = 0.14f + (float)rng.NextDouble() * 0.1f;
            Cyl(r, "Jar", new Vector3(x0 + i * spacing, at.y + h / 2, at.z), new Vector3(0.1f, h, 0.1f), mat);
        }
    }

    public void Plates(Transform r, Vector3 at, int count, Material mat)
    {
        for (int i = 0; i < count; i++)
        {
            var p = Cyl(r, "Plate", new Vector3(at.x - 0.3f + i * 0.2f, at.y + 0.1f, at.z), new Vector3(0.18f, 0.015f, 0.18f), mat);
            p.localRotation = Quaternion.Euler(80, 0, 0);
        }
    }

    public void Pot(Transform r, Vector3 at, Material mat)
    {
        Cyl(r, "Pot", at + new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.16f, 0.24f), mat);
    }

    public void Lamp(Transform r, Vector3 at, Material baseMat, Material shadeMat)
    {
        Cyl(r, "LampBase", at + new Vector3(0, 0.15f, 0), new Vector3(0.05f, 0.3f, 0.05f), baseMat);
        Cyl(r, "LampFoot", at + new Vector3(0, 0.015f, 0), new Vector3(0.14f, 0.03f, 0.14f), baseMat);
        Cyl(r, "LampShade", at + new Vector3(0, 0.38f, 0), new Vector3(0.3f, 0.2f, 0.3f), shadeMat);
    }

    // ---------- wall decor (room-local, inner faces of the walls) ----------

    // position on a wall: t along the wall, y height, d distance out from the wall surface
    public static Vector3 OnWall(char wall, float t, float y, float d, out float yaw)
    {
        switch (wall)
        {
            case 'N': yaw = 180; return new Vector3(t, y, 7.9f - d);
            case 'S': yaw = 0; return new Vector3(t, y, 0.1f + d);
            case 'W': yaw = 90; return new Vector3(0.1f + d, y, t);
            default: yaw = -90; return new Vector3(7.9f - d, y, t);
        }
    }

    Transform WallAnchor(Transform room, string name, char wall, float t, float y, float d)
    {
        float yaw;
        var a = new GameObject(name).transform;
        a.SetParent(room, false);
        a.localPosition = OnWall(wall, t, y, d, out yaw);
        a.localRotation = Quaternion.Euler(0, yaw, 0);
        return a;
    }

    // gallery wall: count frames of mixed sizes between t0..t1 and heights y0..y1
    public void Gallery(Transform room, char wall, float t0, float t1, float y0, float y1, int count, Material frameA, Material frameB, int seed)
    {
        var r = new System.Random(seed);
        var canvases = new[]
        {
            Kit.Flat("Canvas_Ivory", new Color(0.86f, 0.82f, 0.70f)), Kit.Flat("Canvas_Sepia", new Color(0.55f, 0.43f, 0.30f)),
            Kit.Flat("Canvas_Slate", new Color(0.30f, 0.33f, 0.36f)), Kit.Flat("Canvas_Oxblood", new Color(0.45f, 0.15f, 0.12f)),
        };
        int cols = Mathf.Max(1, Mathf.CeilToInt(count / 2f));
        int rows = count > cols ? 2 : 1;
        float cw = (t1 - t0) / cols, rh = (y1 - y0) / rows;
        int n = 0;
        for (int row = 0; row < rows && n < count; row++)
            for (int c = 0; c < cols && n < count; c++, n++)
            {
                float w = cw * (0.55f + (float)r.NextDouble() * 0.3f), h = rh * (0.6f + (float)r.NextDouble() * 0.3f);
                var a = WallAnchor(room, "Frame", wall, t0 + cw * (c + 0.5f), y0 + rh * (row + 0.5f), 0.02f);
                Box(a, "Border", Vector3.zero, new Vector3(w, h, 0.03f), n % 2 == 0 ? frameA : frameB);
                Box(a, "Picture", new Vector3(0, 0, 0.016f), new Vector3(w * 0.8f, h * 0.8f, 0.005f), canvases[r.Next(canvases.Length)]);
            }
    }

    // wainscoting / tiled lower wall up to height, plus crown molding at the top; skips doorways
    public void Trim(Transform room, Material mat, float height, bool crown)
    {
        foreach (char wall in new[] { 'N', 'S', 'W', 'E' })
            foreach (var (a, b) in WallRuns(wall))
            {
                float len = b - a, mid = (a + b) / 2;
                var w = WallAnchor(room, "Wainscot", wall, mid, height / 2, 0.015f);
                Box(w, "Panel", Vector3.zero, new Vector3(len, height, 0.03f), mat);
                Box(w, "Rail", new Vector3(0, height / 2, 0.02f), new Vector3(len, 0.04f, 0.04f), mat);
                if (crown)
                {
                    var c = WallAnchor(room, "Crown", wall, mid, 2.93f, 0.04f);
                    Box(c, "Molding", Vector3.zero, new Vector3(len, 0.14f, 0.08f), mat);
                }
            }
    }

    // stretches of wall without doorways
    (float, float)[] WallRuns(char wall)
    {
        switch (wall)
        {
            case 'E': return new[] { (0.1f, 3.2f), (4.8f, 7.9f) };
            case 'W': return RoomIndex == 0 ? new[] { (0.1f, 7.9f) } : new[] { (0.1f, 3.2f), (4.8f, 7.9f) };
            case 'S': return new[] { (0.1f, 3.3f), (4.7f, 7.9f) };
            default: return new[] { (0.1f, 7.9f) };
        }
    }

    public void Rug(Transform room, Vector3 center, Vector2 size, Material mat)
    {
        Box(room, "Rug", center + new Vector3(0, 0.005f, 0), new Vector3(size.x, 0.01f, size.y), mat);
    }

    public void Curtains(Transform room, char wall, float t, Material mat)
    {
        var a = WallAnchor(room, "Curtain", wall, t, 1.3f, 0.12f);
        Box(a, "Left", new Vector3(-0.35f, 0, 0), new Vector3(0.45f, 2.4f, 0.05f), mat);
        Box(a, "Right", new Vector3(0.35f, 0, 0), new Vector3(0.45f, 2.4f, 0.05f), mat);
        Box(a, "Window", new Vector3(0, 0.2f, -0.03f), new Vector3(0.7f, 1.4f, 0.01f), Kit.Emissive("Window_Glow", new Color(0.55f, 0.65f, 0.75f)));
    }

    public void Chandelier(Transform room, Vector3 floorPos, Material metal, Material glow)
    {
        var c = new GameObject("Chandelier").transform;
        c.SetParent(room, false);
        c.localPosition = new Vector3(floorPos.x, 2.45f, floorPos.z);
        Cyl(c, "Chain", new Vector3(0, 0.27f, 0), new Vector3(0.02f, 0.55f, 0.02f), metal);
        Cyl(c, "Ring", Vector3.zero, new Vector3(0.6f, 0.04f, 0.6f), metal);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3;
            Cyl(c, "Candle", new Vector3(Mathf.Cos(a) * 0.27f, 0.08f, Mathf.Sin(a) * 0.27f), new Vector3(0.04f, 0.12f, 0.04f), glow);
        }
    }

    public void Bulb(Transform room, Vector3 floorPos, Material glow)
    {
        var cord = Kit.Flat("Cord", new Color(0.1f, 0.1f, 0.1f));
        Cyl(room, "Cord", new Vector3(floorPos.x, 2.75f, floorPos.z), new Vector3(0.01f, 0.5f, 0.01f), cord);
        Cyl(room, "Bulb", new Vector3(floorPos.x, 2.45f, floorPos.z), new Vector3(0.12f, 0.16f, 0.12f), glow);
    }

    public void Fan(Transform room, Vector3 floorPos, Material mat)
    {
        var fan = new GameObject("CeilingFan").transform;
        fan.SetParent(room, false);
        fan.localPosition = new Vector3(floorPos.x, 2.65f, floorPos.z);
        Cyl(fan, "Hub", Vector3.zero, new Vector3(0.2f, 0.08f, 0.2f), mat);
        for (int i = 0; i < 4; i++)
            Box(fan, "Blade", Vector3.zero, new Vector3(1.0f, 0.02f, 0.14f), mat).localRotation = Quaternion.Euler(0, i * 45, 0);
        fan.gameObject.AddComponent<AmbientSpin>().degreesPerSecond = 40f;
    }

    public void Clock(Transform room, char wall, float t, Material rim, Material face)
    {
        var a = WallAnchor(room, "WallClock", wall, t, 2.3f, 0.03f);
        Cyl(a, "Rim", Vector3.zero, new Vector3(0.34f, 0.04f, 0.34f), rim, 0).localRotation = Quaternion.Euler(90, 0, 0);
        Cyl(a, "Face", new Vector3(0, 0, 0.021f), new Vector3(0.3f, 0.005f, 0.3f), face).localRotation = Quaternion.Euler(90, 0, 0);
        var hand = new GameObject("SecondHand").transform;
        hand.SetParent(a, false);
        hand.localPosition = new Vector3(0, 0, 0.03f);
        Box(hand, "Hand", new Vector3(0, 0.06f, 0), new Vector3(0.01f, 0.12f, 0.005f), rim);
        var spin = hand.gameObject.AddComponent<AmbientSpin>();
        spin.axis = Vector3.forward;
        spin.degreesPerSecond = 6f; // one turn a minute
    }

    public void WallShelf(Transform room, char wall, float t, float y, Material mat, Action<Transform, float> fill)
    {
        var a = WallAnchor(room, "WallShelf", wall, t, y, 0.13f);
        Box(a, "Board", Vector3.zero, new Vector3(1.2f, 0.03f, 0.25f), mat);
        fill?.Invoke(a, 0.015f);
    }

    public void PanRack(Transform room, Vector3 floorPos, Material iron, Material copper)
    {
        var rack = new GameObject("PanRack").transform;
        rack.SetParent(room, false);
        rack.localPosition = new Vector3(floorPos.x, 2.25f, floorPos.z);
        Box(rack, "Bar", Vector3.zero, new Vector3(1.4f, 0.04f, 0.04f), iron);
        Cyl(rack, "RodL", new Vector3(-0.6f, 0.37f, 0), new Vector3(0.02f, 0.75f, 0.02f), iron);
        Cyl(rack, "RodR", new Vector3(0.6f, 0.37f, 0), new Vector3(0.02f, 0.75f, 0.02f), iron);
        for (int i = 0; i < 5; i++)
            Cyl(rack, "Pan", new Vector3(-0.5f + i * 0.25f, -0.18f, 0), new Vector3(0.22f, 0.03f, 0.22f), copper).localRotation = Quaternion.Euler(90, 0, 0);
    }
}
