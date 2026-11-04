using UnityEngine;
using UnityEngine.UI;

// Small vector UI faces: independent of OS emoji fonts, textures and hidden roles.
namespace Thuong.Emotes
{
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerEmoteGraphic : MaskableGraphic
{
    public enum Shape { Face, Wheel, Bubble }
    public Shape Kind;
    private int emoteId = 1;
    private int selected;
    public int EmoteId { get => emoteId; set { if (emoteId != value) { emoteId = value; SetVerticesDirty(); } } }
    public int Selected { get => selected; set { if (selected != value) { selected = value; SetVerticesDirty(); } } }

    private static readonly Color Ink = new Color32(56, 36, 30, 255);
    private static readonly Color Gold = new Color32(247, 196, 86, 255);
    private VertexHelper mesh;
    private float scale;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        mesh = vh;
        scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
        if (Kind == Shape.Wheel) DrawWheel();
        else if (Kind == Shape.Bubble)
        {
            Disk(Vector2.zero, .96f, new Color32(247, 241, 220, 255));
            Triangle(new Vector2(-.2f, -.83f), new Vector2(.2f, -.83f), new Vector2(0, -1.15f), new Color32(247, 241, 220, 255));
        }
        else DrawFace();
    }

    private void DrawWheel()
    {
        for (int id = 1; id <= PlayerEmoteCatalog.Count; id++)
        {
            float middle = (id - 1) * 45f;
            ArcBand(middle - 21.7f, middle + 21.7f, .34f, .98f,
                selected == id ? new Color32(102, 77, 38, 250) : new Color32(28, 38, 46, 248));
            ArcBand(middle - 21.7f, middle + 21.7f, .958f, .98f,
                selected == id ? Gold : new Color32(89, 105, 111, 170));
        }
        Disk(Vector2.zero, .315f, new Color32(15, 24, 31, 255));
        ArcBand(0, 360, .31f, .317f, new Color32(119, 129, 126, 180));
    }

    private void DrawFace()
    {
        Disk(new Vector2(0, -.04f), .9f, new Color32(112, 63, 29, 180));
        Disk(Vector2.zero, .88f, emoteId == 4 ? new Color32(246, 133, 83, 255) : Gold);
        ArcBand(205, 325, .75f, .8f, new Color32(255, 225, 137, 210));

        if (emoteId == 8)
        {
            Heart(new Vector2(-.32f, .22f)); Heart(new Vector2(.32f, .22f));
            Mouth(false);
            return;
        }
        if (emoteId == 7)
        {
            Line(new Vector2(-.5f, .15f), new Vector2(-.32f, .32f), .07f, Ink);
            Line(new Vector2(-.32f, .32f), new Vector2(-.14f, .15f), .07f, Ink);
            Line(new Vector2(.14f, .15f), new Vector2(.32f, .32f), .07f, Ink);
            Line(new Vector2(.32f, .32f), new Vector2(.5f, .15f), .07f, Ink);
            Disk(new Vector2(0, -.29f), .3f, Ink);
            Line(new Vector2(-.2f, -.15f), new Vector2(.2f, -.15f), .09f, Color.white);
            return;
        }

        Disk(new Vector2(-.31f, .2f), .085f, Ink);
        Disk(new Vector2(.31f, .2f), .085f, Ink);
        switch (emoteId)
        {
            case 2:
                Disk(new Vector2(0, -.3f), .19f, Ink);
                Brow(-1, .51f, .51f); Brow(1, .51f, .51f);
                break;
            case 3:
                Brow(-1, .55f, .43f); Brow(1, .34f, .42f);
                Line(new Vector2(-.25f, -.28f), new Vector2(.25f, -.2f), .065f, Ink);
                break;
            case 4:
                Brow(-1, .48f, .29f); Brow(1, .48f, .29f);
                Mouth(true);
                break;
            case 5:
                Brow(-1, .4f, .52f); Brow(1, .4f, .52f);
                Mouth(true);
                Disk(new Vector2(.5f, -.17f), .12f, new Color32(75, 182, 239, 255));
                Triangle(new Vector2(.38f, -.12f), new Vector2(.62f, -.12f), new Vector2(.5f, .12f), new Color32(75, 182, 239, 255));
                break;
            case 6:
                Brow(-1, .41f, .55f); Brow(1, .41f, .55f);
                for (int i = 0; i < 4; i++)
                    Line(new Vector2(-.32f + i * .16f, -.3f + (i % 2) * .1f),
                        new Vector2(-.16f + i * .16f, -.3f + ((i + 1) % 2) * .1f), .065f, Ink);
                Disk(new Vector2(.62f, .56f), .13f, new Color32(75, 182, 239, 255));
                break;
            default:
                Mouth(false);
                Disk(new Vector2(-.56f, -.08f), .13f, new Color32(238, 133, 105, 175));
                Disk(new Vector2(.56f, -.08f), .13f, new Color32(238, 133, 105, 175));
                break;
        }
    }

    private void Brow(int side, float outsideY, float insideY) =>
        Line(new Vector2(side * .51f, outsideY), new Vector2(side * .14f, insideY), .065f, Ink);

    private void Mouth(bool sad)
    {
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= 16; i++)
        {
            float x = Mathf.Lerp(-.38f, .38f, i / 16f);
            float curve = 1f - x * x / (.38f * .38f);
            Vector2 point = new Vector2(x, sad ? -.4f + .19f * curve : -.19f - .23f * curve);
            if (i > 0) Line(previous, point, .07f, Ink);
            previous = point;
        }
    }

    private void Heart(Vector2 center)
    {
        Color red = new Color32(211, 66, 85, 255);
        Disk(center + new Vector2(-.09f, .05f), .14f, red);
        Disk(center + new Vector2(.09f, .05f), .14f, red);
        Triangle(center + new Vector2(-.22f, .04f), center + new Vector2(.22f, .04f), center + new Vector2(0, -.23f), red);
    }

    private void Disk(Vector2 center, float radius, Color tint)
    {
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
            Triangle(center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, tint);
        }
    }

    private void ArcBand(float from, float to, float inner, float outer, Color tint)
    {
        int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) / 5));
        for (int i = 0; i < steps; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(from, to, (i + 1) / (float)steps) * Mathf.Deg2Rad;
            Vector2 da = new Vector2(Mathf.Sin(a), Mathf.Cos(a)), db = new Vector2(Mathf.Sin(b), Mathf.Cos(b));
            Triangle(da * inner, da * outer, db * outer, tint);
            Triangle(da * inner, db * outer, db * inner, tint);
        }
    }

    private void Line(Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        Triangle(a - normal, a + normal, b + normal, tint);
        Triangle(a - normal, b + normal, b - normal, tint);
    }

    private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int start = mesh.currentVertCount;
        Color final = tint * color;
        mesh.AddVert(a * scale + rectTransform.rect.center, final, Vector2.zero);
        mesh.AddVert(b * scale + rectTransform.rect.center, final, Vector2.zero);
        mesh.AddVert(c * scale + rectTransform.rect.center, final, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }
}

}
