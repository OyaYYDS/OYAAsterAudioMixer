// IconGen — ASTER 音量合成器图标工具
// 用法:
//   IconGen.exe                    生成内置矢量设计图标到桌面
//   IconGen.exe <图片路径>          把指定图片转成图标（居中适配方形画布，透明边）
// 输出到桌面: aster-mixer-256.png（预览）+ aster-mixer.ico（多尺寸 256/64/48/32/24/16）
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var outputDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
var pngPath = Path.Combine(outputDir, "aster-mixer-256.png");
var icoPath = Path.Combine(outputDir, "aster-mixer.ico");

Bitmap master = args.Length > 0 ? ConvertToSquare256(args[0]) : CreateVector();
using (master)
{
    master.Save(pngPath, ImageFormat.Png);
    SaveIco(icoPath, master);
}
Console.WriteLine($"已生成: {pngPath}");
Console.WriteLine($"已生成: {icoPath}");

// ---------- 图片转图标 ----------

static Bitmap ConvertToSquare256(string srcPath)
{
    using var src = Image.FromFile(srcPath);
    var w = src.Width;
    var h = src.Height;
    var side = Math.Max(w, h);
    var scale = 256f / side;
    var dw = (int)Math.Round(w * scale);
    var dh = (int)Math.Round(h * scale);

    var canvas = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(canvas))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.DrawImage(src,
            new Rectangle((256 - dw) / 2, (256 - dh) / 2, dw, dh),
            new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);
    }
    return ApplyRoundedCorners(canvas);
}

static Bitmap ApplyRoundedCorners(Bitmap src)
{
    var result = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(result))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var path = RoundedRect(new RectangleF(0, 0, src.Width, src.Height), src.Width * 0.22f);
        g.SetClip(path);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
    }
    return result;
}

// ---------- ICO 容器（PNG 编码多尺寸） ----------

static void SaveIco(string icoPath, Bitmap master)
{
    var sizes = new[] { 256, 64, 48, 32, 24, 16 };
    var pngs = new List<byte[]>();
    foreach (var s in sizes)
    {
        using var scaled = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(master, new Rectangle(0, 0, s, s));
        }
        using var ms = new MemoryStream();
        scaled.Save(ms, ImageFormat.Png);
        pngs.Add(ms.ToArray());
    }

    using var fs = File.Create(icoPath);
    using var w = new BinaryWriter(fs);
    w.Write((ushort)0);
    w.Write((ushort)1);
    w.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        var s = sizes[i];
        w.Write((byte)(s == 256 ? 0 : s));
        w.Write((byte)(s == 256 ? 0 : s));
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write((uint)pngs[i].Length);
        w.Write((uint)offset);
        offset += pngs[i].Length;
    }
    foreach (var p in pngs)
        w.Write(p);
}

// ---------- 内置矢量设计（备用） ----------

static Bitmap CreateVector()
{
    var bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.Clear(Color.Transparent);
    DrawVectorIcon(g, 256);
    return bmp;
}

static void DrawVectorIcon(Graphics g, float s)
{
    var rect = new RectangleF(s * 0.03f, s * 0.03f, s * 0.94f, s * 0.94f);
    var radius = s * 0.22f;
    using var bgPath = RoundedRect(rect, radius);
    using var bgBrush = new LinearGradientBrush(
        rect, Color.FromArgb(77, 139, 255), Color.FromArgb(138, 75, 245), 45f);
    g.FillPath(bgBrush, bgPath);

    var white = Color.FromArgb(252, 252, 255);

    var topLeft = new PointF(s * 0.42f, s * 0.355f);
    var topRight = new PointF(s * 0.575f, s * 0.355f);
    var bottomRight = new PointF(s * 0.63f, s * 0.685f);
    var bottomLeft = new PointF(s * 0.365f, s * 0.685f);
    using (var body = new GraphicsPath())
    {
        body.AddPolygon(new[] { topLeft, topRight, bottomRight, bottomLeft });
        using var bodyBrush = new SolidBrush(white);
        g.FillPath(bodyBrush, body);
        var r = s * 0.02f;
        foreach (var p in new[] { topLeft, topRight, bottomRight, bottomLeft })
        {
            g.FillEllipse(bodyBrush, p.X - r, p.Y - r, r * 2, r * 2);
        }
    }

    var arcCenterX = s * 0.635f;
    var arcCenterY = s * 0.52f;
    using (var arcPen = new Pen(white, s * 0.042f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
    {
        var r1 = s * 0.115f;
        var r2 = s * 0.19f;
        g.DrawArc(arcPen, arcCenterX - r1, arcCenterY - r1, r1 * 2, r1 * 2, -58, 116);
        g.DrawArc(arcPen, arcCenterX - r2, arcCenterY - r2, r2 * 2, r2 * 2, -58, 116);
    }

    var faderX = s * 0.815f;
    var trackTop = s * 0.26f;
    var trackBottom = s * 0.74f;
    using (var trackPen = new Pen(Color.FromArgb(200, 255, 255, 255), s * 0.045f)
           { StartCap = LineCap.Round, EndCap = LineCap.Round })
    {
        g.DrawLine(trackPen, faderX, trackTop, faderX, trackBottom);
    }
    var knobR = s * 0.055f;
    var knobY = s * 0.45f;
    using var knobBrush = new SolidBrush(white);
    g.FillEllipse(knobBrush, faderX - knobR, knobY - knobR, knobR * 2, knobR * 2);
}

static GraphicsPath RoundedRect(RectangleF rect, float radius)
{
    var d = radius * 2;
    var path = new GraphicsPath();
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}
