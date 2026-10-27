using Team8.Core;
using UnityEngine;

// The three room themes and the pieces they are made of, built from simple shapes with a few
// shared materials per theme (few draw calls on Quest). Every piece is built in its own local
// frame: +z faces into the room / toward the room centre, y = 0 is the floor.
// Hiding kits use exactly the sizes from RoomPlanner, so the hiding geometry is the same in
// every room and only the look changes.
public abstract class RoomTheme
{
    public abstract string Name { get; }
    protected readonly RoomPieces P = new RoomPieces();
    public int RoomIndex { set { P.RoomIndex = value; } }

    public abstract Material Floor { get; }
    public abstract Material Wall { get; }

    public abstract void UnderKit(Transform root, HideKit kit);    // cover 1.6 x 1.0, top 0.55, open below 0.35
    public abstract void BehindKit(Transform root, HideKit kit);   // cover 1.4 x 0.6, 1.15 high
    public abstract void InsideKit(Transform root, HideKit kit);   // cabinet 0.9 x 0.6 x 1.3, open toward -z
    public abstract void WallPiece(Transform root, int index);     // fits 1.0 x 0.5, back against the wall at -z
    public abstract void CenterPiece(Transform root);              // fits 1.2 x 0.8, 1.2 high
    public abstract void Decor(Transform room);                    // wall decor, rug, ceiling fixture (no colliders)
    public abstract Material Accent { get; }                       // used for simple decoys when the kit has no small props

    // small grabbable decoy (under 0.3 m) when the kit has no small props
    public void SmallProp(Transform r, int i)
    {
        switch (i % 3)
        {
            case 0: P.Box(r, "Box", new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.16f, 0.18f), Accent); break;
            case 1: P.Cyl(r, "Jar", new Vector3(0, 0.11f, 0), new Vector3(0.12f, 0.22f, 0.12f), Accent); break;
            default: P.Books(r, new Vector3(0.05f, 0, 0), 3, Accent, Wall, Floor); break;
        }
    }

    // shared cabinet shell used by every Inside kit: open at -z (away from the room centre)
    protected Transform CabinetShell(Transform root, Material body, Material door, float doorAngle)
    {
        const float w = 0.9f, d = 0.6f, h = 1.3f, t = 0.04f;
        P.Box(root, "Side_L", new Vector3(-w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), body);
        P.Box(root, "Side_R", new Vector3(w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), body);
        P.Box(root, "Back", new Vector3(0, h / 2, d / 2 - t / 2), new Vector3(w, h, t), body);
        P.Box(root, "Top", new Vector3(0, h - t / 2, 0), new Vector3(w, t, d), body);
        P.Box(root, "Bottom", new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), body);
        P.Box(root, "Shelf", new Vector3(0, 0.42f, 0.02f), new Vector3(w - 2 * t, 0.03f, d - 0.06f), body);
        var hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(w / 2, 0, -d / 2);
        hinge.localRotation = Quaternion.Euler(0, doorAngle, 0);
        P.Box(hinge, "Door", new Vector3(-w / 2, h / 2, -0.01f), new Vector3(w, h - 0.04f, 0.03f), door);
        return hinge;
    }
}

// Classic English bedroom: hunter green, walnut, leather, plaid, brass.
public class BedroomTheme : RoomTheme
{
    public override string Name => "bedroom";
    Material green, walnut, leather, navy, plaid, brass, shade, cream, sisal;

    public BedroomTheme()
    {
        green = Kit.Tinted("Bed_WallGreen", "wall_plaster", "wall_concrete", new Vector2(4, 1.5f), new Color(0.16f, 0.27f, 0.21f));
        walnut = Kit.Flat("Bed_Walnut", new Color(0.30f, 0.18f, 0.10f));
        leather = Kit.Tinted("Bed_Leather", "leather", "wall_plaster", Vector2.one, new Color(0.55f, 0.30f, 0.15f));
        navy = Kit.Flat("Bed_Navy", new Color(0.10f, 0.15f, 0.30f));
        plaid = Kit.Flat("Bed_PlaidRed", new Color(0.55f, 0.12f, 0.10f));
        brass = Kit.Metal("Bed_Brass", new Color(0.80f, 0.62f, 0.28f), 0.9f, 0.7f);
        shade = Kit.Emissive("Bed_LampShade", new Color(1f, 0.85f, 0.6f));
        cream = Kit.Flat("Bed_Cream", new Color(0.90f, 0.86f, 0.76f));
        sisal = Kit.Flat("Bed_Sisal", new Color(0.62f, 0.50f, 0.33f));
    }

    public override Material Floor => Kit.Tinted("Bed_Floor", "floor_wood", "floor_concrete", new Vector2(4, 4), new Color(0.62f, 0.45f, 0.30f));
    public override Material Wall => green;
    public override Material Accent => leather;

    public override void UnderKit(Transform r, HideKit k)
    {
        // low bed: frame + mattress + plaid duvet + tufted leather headboard on the far side
        P.Legs(r, 1.6f, 1.0f, 0.35f, walnut);
        P.Box(r, "Frame", new Vector3(0, 0.40f, 0), new Vector3(1.6f, 0.10f, 1.0f), walnut);
        P.Box(r, "Mattress", new Vector3(0, 0.50f, 0), new Vector3(1.55f, 0.10f, 0.95f), cream);
        P.Box(r, "Duvet", new Vector3(0, 0.555f, 0.05f), new Vector3(1.62f, 0.02f, 0.9f), navy);
        P.Box(r, "Stripe", new Vector3(0, 0.567f, 0.05f), new Vector3(1.63f, 0.005f, 0.06f), plaid);
        P.Box(r, "Pillow", new Vector3(0, 0.6f, -0.35f), new Vector3(1.2f, 0.1f, 0.25f), navy);
        P.Box(r, "Headboard", new Vector3(0, 0.75f, -0.52f), new Vector3(1.7f, 1.1f, 0.08f), leather);
        P.Box(r, "Throw", new Vector3(0.45f, 0.575f, 0.2f), new Vector3(0.6f, 0.03f, 0.55f), Kit.Flat("Bed_Fur", new Color(0.08f, 0.09f, 0.12f)));
    }

    public override void BehindKit(Transform r, HideKit k)
    {
        // leather settee facing the room, back toward the lever
        P.Box(r, "Seat", new Vector3(0, 0.30f, 0.05f), new Vector3(1.4f, 0.45f, 0.5f), leather);
        P.Box(r, "Back", new Vector3(0, 0.75f, -0.22f), new Vector3(1.4f, 0.8f, 0.16f), leather);
        P.Box(r, "Arm_L", new Vector3(-0.64f, 0.55f, 0), new Vector3(0.12f, 0.35f, 0.6f), leather);
        P.Box(r, "Arm_R", new Vector3(0.64f, 0.55f, 0), new Vector3(0.12f, 0.35f, 0.6f), leather);
        P.Box(r, "Cushion", new Vector3(0.3f, 0.6f, -0.05f), new Vector3(0.35f, 0.3f, 0.12f), plaid);
    }

    public override void InsideKit(Transform r, HideKit k)
    {
        CabinetShell(r, walnut, walnut, 110);
        P.Box(r, "Rail", new Vector3(0, 1.15f, 0), new Vector3(0.82f, 0.02f, 0.02f), brass);
    }

    public override void WallPiece(Transform r, int i)
    {
        switch (i % 4)
        {
            case 0: // nightstand + brass lamp
                P.Box(r, "Nightstand", new Vector3(0, 0.3f, 0), new Vector3(0.55f, 0.6f, 0.45f), walnut);
                P.Lamp(r, new Vector3(0, 0.6f, 0), brass, shade);
                P.Books(r, new Vector3(0.35f, 0, 0), 4, walnut, navy, plaid);
                break;
            case 1: // console with riding boots under it
                P.Box(r, "Console", new Vector3(0, 0.82f, 0), new Vector3(1.0f, 0.05f, 0.45f), walnut);
                P.Legs(r, 1.0f, 0.45f, 0.8f, walnut);
                for (int b = 0; b < 3; b++)
                    P.Box(r, "Boot" + b, new Vector3(-0.3f + b * 0.3f, 0.25f, 0), new Vector3(0.1f, 0.5f, 0.25f), leather);
                P.Box(r, "Box", new Vector3(0.2f, 0.9f, 0), new Vector3(0.35f, 0.12f, 0.25f), leather);
                break;
            case 2: // leather trunk + stacked books
                P.Box(r, "Trunk", new Vector3(0, 0.25f, 0), new Vector3(0.95f, 0.5f, 0.48f), leather);
                P.Box(r, "Band", new Vector3(0, 0.25f, 0), new Vector3(0.97f, 0.06f, 0.5f), brass);
                P.Books(r, new Vector3(-0.2f, 0.5f, 0), 5, walnut, navy, plaid);
                break;
            default: // tall bookcase
                P.Shelves(r, 1.0f, 0.45f, 1.9f, 4, walnut, i2 => P.Books(r, new Vector3(0, i2, 0), 9, navy, plaid, leather));
                break;
        }
    }

    public override void CenterPiece(Transform r)
    {
        // chest of drawers with a standing mirror
        P.Box(r, "Chest", new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.8f), walnut);
        for (int d = 0; d < 3; d++) P.Box(r, "Drawer" + d, new Vector3(0, 0.18f + d * 0.27f, 0.405f), new Vector3(1.0f, 0.2f, 0.01f), leather);
        P.Box(r, "Mirror", new Vector3(0, 1.05f, -0.2f), new Vector3(0.7f, 0.3f, 0.04f), Kit.Metal("Bed_Mirror", new Color(0.8f, 0.82f, 0.85f), 1f, 0.95f));
        P.Lamp(r, new Vector3(0.45f, 0.9f, 0.15f), brass, shade);
    }

    public override void Decor(Transform room)
    {
        var gold = Kit.Metal("Bed_Gold", new Color(0.75f, 0.58f, 0.25f), 0.8f, 0.6f);
        // gallery wall on the back wall + wainscoting + crown molding all round
        P.Gallery(room, 'N', 0.6f, 7.4f, 1.45f, 2.7f, 14, gold, walnut, 3);
        P.Gallery(room, 'W', 0.6f, 2.6f, 1.5f, 2.6f, 3, gold, walnut, 7);
        P.Gallery(room, 'W', 5.4f, 7.4f, 1.5f, 2.6f, 3, gold, walnut, 11);
        P.Trim(room, walnut, 1.0f, true);
        P.Rug(room, new Vector3(4, 0, 4), new Vector2(4.2f, 3.0f), sisal);
        P.Curtains(room, 'S', 1.6f, plaid);
        P.Curtains(room, 'S', 6.4f, plaid);
        P.Chandelier(room, new Vector3(4, 0, 4), brass, shade);
        P.Clock(room, 'E', 6.3f, brass, cream);
    }
}

// Antique shop / attic: dusty wood, dust sheets, trunks, mirrors, leaning frames.
public class AtticTheme : RoomTheme
{
    public override string Name => "attic";
    Material plank, wood, sheet, leather, brass, silver, shade, rug, dark;

    public AtticTheme()
    {
        plank = Kit.Tinted("Attic_Wall", "wall_plaster", "wall_concrete", new Vector2(4, 1.5f), new Color(0.62f, 0.55f, 0.45f));
        wood = Kit.Flat("Attic_Wood", new Color(0.36f, 0.26f, 0.17f));
        dark = Kit.Flat("Attic_DarkWood", new Color(0.22f, 0.15f, 0.10f));
        sheet = Kit.Flat("Attic_Sheet", new Color(0.86f, 0.84f, 0.79f));
        leather = Kit.Flat("Attic_Leather", new Color(0.40f, 0.24f, 0.14f));
        brass = Kit.Metal("Attic_Brass", new Color(0.65f, 0.52f, 0.30f), 0.8f, 0.4f);
        silver = Kit.Metal("Attic_Mirror", new Color(0.75f, 0.77f, 0.80f), 1f, 0.9f);
        shade = Kit.Emissive("Attic_Bulb", new Color(1f, 0.8f, 0.5f));
        rug = Kit.Flat("Attic_Rug", new Color(0.45f, 0.18f, 0.15f));
    }

    public override Material Floor => Kit.Tinted("Attic_Floor", "floor_wood", "floor_concrete", new Vector2(4, 4), new Color(0.48f, 0.36f, 0.25f));
    public override Material Wall => plank;
    public override Material Accent => brass;

    public override void UnderKit(Transform r, HideKit k)
    {
        // low table under a dust sheet that hangs down to 0.35 m
        P.Legs(r, 1.6f, 1.0f, 0.51f, wood);
        P.Box(r, "Top", new Vector3(0, 0.53f, 0), new Vector3(1.6f, 0.04f, 1.0f), wood);
        P.Box(r, "SheetTop", new Vector3(0, 0.555f, 0), new Vector3(1.66f, 0.01f, 1.06f), sheet);
        P.Box(r, "SheetFront", new Vector3(0, 0.45f, 0.53f), new Vector3(1.66f, 0.2f, 0.01f), sheet);
        P.Box(r, "SheetBack", new Vector3(0, 0.45f, -0.53f), new Vector3(1.66f, 0.2f, 0.01f), sheet);
        P.Box(r, "Headboard", new Vector3(0, 0.75f, -0.52f), new Vector3(1.0f, 1.1f, 0.06f), silver); // old mirror leaning behind
    }

    public override void BehindKit(Transform r, HideKit k)
    {
        // stack of big frames and canvases leaning toward the room
        for (int i = 0; i < 4; i++)
        {
            var f = P.Box(r, "Frame" + i, new Vector3(-0.1f + i * 0.07f, 0.575f - i * 0.06f, -0.15f + i * 0.1f),
                          new Vector3(1.4f - i * 0.12f, 1.15f - i * 0.12f, 0.05f), i % 2 == 0 ? dark : brass);
            f.localRotation = Quaternion.Euler(-8, 0, 0);
        }
        P.Box(r, "Canvas", new Vector3(0, 0.55f, 0.18f), new Vector3(1.0f, 0.8f, 0.02f), sheet);
    }

    public override void InsideKit(Transform r, HideKit k)
    {
        CabinetShell(r, dark, wood, 100);
        P.Box(r, "SheetOnTop", new Vector3(0, 1.31f, 0), new Vector3(0.95f, 0.02f, 0.65f), sheet);
    }

    public override void WallPiece(Transform r, int i)
    {
        switch (i % 4)
        {
            case 0: // dresser under a dust sheet
                P.Box(r, "Dresser", new Vector3(0, 0.45f, 0), new Vector3(0.95f, 0.9f, 0.45f), wood);
                P.Box(r, "Sheet", new Vector3(0, 0.6f, 0.01f), new Vector3(1.0f, 0.65f, 0.48f), sheet);
                break;
            case 1: // stacked chairs
                for (int c = 0; c < 3; c++)
                {
                    P.Box(r, "Seat" + c, new Vector3(0, 0.45f + c * 0.12f, 0.02f * c), new Vector3(0.45f, 0.04f, 0.42f), dark);
                    P.Box(r, "Back" + c, new Vector3(0, 0.75f + c * 0.12f, -0.2f), new Vector3(0.45f, 0.6f, 0.03f), dark);
                }
                P.Legs(r, 0.45f, 0.42f, 0.45f, dark);
                break;
            case 2: // trunks
                P.Box(r, "Trunk1", new Vector3(0, 0.22f, 0), new Vector3(0.95f, 0.44f, 0.46f), leather);
                P.Box(r, "Trunk2", new Vector3(0.05f, 0.6f, 0), new Vector3(0.75f, 0.32f, 0.4f), wood);
                P.Box(r, "Band", new Vector3(0, 0.22f, 0), new Vector3(0.97f, 0.05f, 0.48f), brass);
                break;
            default: // tall cabinet with a lamp on top
                P.Box(r, "Cabinet", new Vector3(0, 0.85f, 0), new Vector3(0.9f, 1.7f, 0.45f), dark);
                P.Lamp(r, new Vector3(0.2f, 1.7f, 0), brass, shade);
                break;
        }
    }

    public override void CenterPiece(Transform r)
    {
        // big wardrobe under a dust sheet
        P.Box(r, "Wardrobe", new Vector3(0, 0.6f, 0), new Vector3(1.15f, 1.2f, 0.75f), wood);
        P.Box(r, "Sheet", new Vector3(0, 0.75f, 0), new Vector3(1.2f, 0.95f, 0.8f), sheet);
    }

    public override void Decor(Transform room)
    {
        // roof beams, leaning frames and mirrors along the walls, worn rug, bare bulbs, slow ceiling fan
        for (int b = 0; b < 4; b++)
            P.Box(room, "Beam" + b, new Vector3(1 + b * 2f, 2.85f, 4), new Vector3(0.2f, 0.2f, 8f), dark);
        P.Gallery(room, 'N', 0.8f, 7.2f, 1.3f, 2.4f, 7, brass, dark, 5);
        P.Gallery(room, 'E', 0.8f, 2.5f, 1.4f, 2.4f, 2, silver, dark, 9);
        P.Trim(room, dark, 0.15f, false);
        P.Rug(room, new Vector3(4, 0, 4), new Vector2(3.4f, 2.4f), rug);
        P.Bulb(room, new Vector3(2.5f, 0, 4), shade);
        P.Bulb(room, new Vector3(5.5f, 0, 4), shade);
        P.Fan(room, new Vector3(4, 0, 2.2f), dark);
    }
}

// Victorian kitchen / pantry: checkered tile, cream walls, painted cabinets, jars, copper.
public class KitchenTheme : RoomTheme
{
    public override string Name => "kitchen";
    Material cream, paint, wood, copper, glass, sack, iron, white, shade;

    public KitchenTheme()
    {
        cream = Kit.Tinted("Kitchen_Wall", "wall_plaster", "wall_concrete", new Vector2(4, 1.5f), new Color(0.88f, 0.84f, 0.72f));
        paint = Kit.Flat("Kitchen_Paint", new Color(0.42f, 0.50f, 0.46f));
        wood = Kit.Flat("Kitchen_Wood", new Color(0.52f, 0.37f, 0.22f));
        copper = Kit.Metal("Kitchen_Copper", new Color(0.75f, 0.45f, 0.28f), 0.9f, 0.6f);
        glass = Kit.Metal("Kitchen_Jar", new Color(0.72f, 0.80f, 0.70f), 0f, 0.9f);
        sack = Kit.Flat("Kitchen_Sack", new Color(0.76f, 0.66f, 0.48f));
        iron = Kit.Metal("Kitchen_Iron", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.4f);
        white = Kit.Flat("Kitchen_Tile", new Color(0.93f, 0.93f, 0.90f));
        shade = Kit.Emissive("Kitchen_Pendant", new Color(1f, 0.9f, 0.7f));
    }

    public override Material Floor => Kit.Checker("Kitchen_Floor", new Color(0.12f, 0.12f, 0.13f), new Color(0.88f, 0.86f, 0.80f), 16);
    public override Material Wall => cream;
    public override Material Accent => copper;

    public override void UnderKit(Transform r, HideKit k)
    {
        // low butcher-block work table with a pot rack shelf on the far side
        P.Legs(r, 1.6f, 1.0f, 0.5f, wood);
        P.Box(r, "Top", new Vector3(0, 0.525f, 0), new Vector3(1.6f, 0.05f, 1.0f), wood);
        P.Box(r, "Apron", new Vector3(0, 0.45f, 0.48f), new Vector3(1.6f, 0.1f, 0.03f), wood);
        P.Box(r, "Headboard", new Vector3(0, 0.75f, -0.52f), new Vector3(1.6f, 1.1f, 0.05f), paint);
        P.Pot(r, new Vector3(-0.4f, 0.55f, 0.1f), copper);
        P.Pot(r, new Vector3(0.3f, 0.55f, -0.1f), iron);
    }

    public override void BehindKit(Transform r, HideKit k)
    {
        // stacked flour sacks
        for (int row = 0; row < 3; row++)
            for (int c = 0; c < 3 - row; c++)
                P.Cyl(r, "Sack" + row + c, new Vector3(-0.45f + c * 0.45f + row * 0.22f, 0.2f + row * 0.38f, 0), new Vector3(0.42f, 0.6f, 0.42f), sack, 90);
        P.Box(r, "Crate", new Vector3(0.5f, 0.2f, 0.05f), new Vector3(0.4f, 0.4f, 0.4f), wood);
    }

    public override void InsideKit(Transform r, HideKit k)
    {
        CabinetShell(r, paint, paint, 105);
        P.Jars(r, new Vector3(0, 1.1f, 0.1f), 4, glass, 0.18f);
    }

    public override void WallPiece(Transform r, int i)
    {
        switch (i % 4)
        {
            case 0: // pantry shelves full of jars
                P.Shelves(r, 1.0f, 0.4f, 1.9f, 4, wood, y => P.Jars(r, new Vector3(0, y, 0), 6, glass, 0.14f));
                break;
            case 1: // range / stove with pots
                P.Box(r, "Range", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.9f, 0.5f), iron);
                P.Pot(r, new Vector3(-0.25f, 0.9f, 0.05f), copper);
                P.Pot(r, new Vector3(0.25f, 0.9f, 0.05f), copper);
                P.Box(r, "Hood", new Vector3(0, 1.9f, -0.05f), new Vector3(0.9f, 0.25f, 0.4f), copper);
                break;
            case 2: // lower cabinet + counter
                P.Box(r, "Cabinet", new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.84f, 0.48f), paint);
                P.Box(r, "Counter", new Vector3(0, 0.86f, 0.01f), new Vector3(1.02f, 0.04f, 0.5f), white);
                P.Jars(r, new Vector3(0, 0.88f, 0), 3, glass, 0.2f);
                break;
            default: // hutch with plates
                P.Box(r, "HutchBase", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.9f, 0.45f), paint);
                P.Shelves(r, 1.0f, 0.3f, 1.9f, 3, paint, y => { if (y > 0.9f) P.Plates(r, new Vector3(0, y, -0.08f), 4, white); });
                break;
        }
    }

    public override void CenterPiece(Transform r)
    {
        // kitchen island with open shelf and a basket
        P.Box(r, "Island", new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.8f), paint);
        P.Box(r, "Top", new Vector3(0, 0.92f, 0), new Vector3(1.25f, 0.05f, 0.85f), wood);
        P.Box(r, "Shelf", new Vector3(0, 1.15f, 0), new Vector3(1.1f, 0.03f, 0.3f), wood);
        P.Jars(r, new Vector3(0, 1.17f, 0), 4, glass, 0.15f);
    }

    public override void Decor(Transform room)
    {
        // tiled lower walls, wall shelves with jars, hanging copper pan rack, two pendants, clock
        P.Trim(room, white, 1.1f, true);
        P.WallShelf(room, 'N', 2.0f, 1.7f, wood, (t, y) => P.Jars(t, new Vector3(0, y, 0), 5, glass, 0.14f));
        P.WallShelf(room, 'N', 6.0f, 1.7f, wood, (t, y) => P.Jars(t, new Vector3(0, y, 0), 5, glass, 0.14f));
        P.PanRack(room, new Vector3(4, 0, 4), iron, copper);
        P.Bulb(room, new Vector3(2.7f, 0, 4), shade);
        P.Bulb(room, new Vector3(5.3f, 0, 4), shade);
        P.Clock(room, 'S', 6.3f, iron, white);
    }
}
