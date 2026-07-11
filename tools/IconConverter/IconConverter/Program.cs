using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var sizes = new[] { 256, 128, 64, 48, 32, 24, 16 };
var images = sizes.Select(DrawClock).ToList();

SaveAsIcon(images, "clock.ico");
images[0].Save("clock.png", ImageFormat.Png);

Console.WriteLine("Generated clock.ico and clock.png");

static Bitmap DrawClock(int size)
{
    float scale = size / 256f;
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float cx = size / 2f;
        float cy = size / 2f;
        float rimThickness = 12 * scale;
        float margin = 5 * scale;
        float outerRadius = size / 2f - margin - rimThickness / 2f;

        // Rim
        using (var rimPen = new Pen(Color.FromArgb(0x33, 0x33, 0x33), rimThickness))
        {
            rimPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            rimPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            g.DrawEllipse(rimPen, cx - outerRadius, cy - outerRadius, outerRadius * 2, outerRadius * 2);
        }

        // Ticks
        float tickThickness = 6 * scale;
        float tickLength = 20 * scale;
        float tickInnerRadius = outerRadius - tickLength;
        using (var tickPen = new Pen(Color.FromArgb(0x33, 0x33, 0x33), tickThickness))
        {
            tickPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            tickPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            for (int i = 0; i < 12; i++)
            {
                double angleDeg = 90 - i * 30;
                double angleRad = angleDeg * Math.PI / 180;
                float x1 = cx + tickInnerRadius * (float)Math.Cos(angleRad);
                float y1 = cy - tickInnerRadius * (float)Math.Sin(angleRad);
                float x2 = cx + outerRadius * (float)Math.Cos(angleRad);
                float y2 = cy - outerRadius * (float)Math.Sin(angleRad);
                g.DrawLine(tickPen, x1, y1, x2, y2);
            }
        }

        // Hour hand (10 hours, 8 minutes)
        DrawHand(g, cx, cy, 90 - (10 * 60 + 8) * 0.5, 68 * scale, 14 * scale, Color.FromArgb(0x33, 0x33, 0x33));
        // Minute hand (8 minutes)
        DrawHand(g, cx, cy, 90 - 8 * 6.0, 98 * scale, 11 * scale, Color.FromArgb(0x33, 0x33, 0x33));
        // Second hand (35 seconds)
        DrawHand(g, cx, cy, 90 - 35 * 6.0, 108 * scale, 4 * scale, Color.FromArgb(0xD3, 0x2F, 0x2F));

        // Center dot
        float centerRadius = 9 * scale;
        using (var centerBrush = new SolidBrush(Color.FromArgb(0x33, 0x33, 0x33)))
        {
            g.FillEllipse(centerBrush, cx - centerRadius, cy - centerRadius, centerRadius * 2, centerRadius * 2);
        }
    }
    return bmp;
}

static void DrawHand(Graphics g, float cx, float cy, double angleDeg, float length, float thickness, Color color)
{
    double angleRad = angleDeg * Math.PI / 180;
    float x = cx + length * (float)Math.Cos(angleRad);
    float y = cy - length * (float)Math.Sin(angleRad);
    using (var pen = new Pen(color, thickness))
    {
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        g.DrawLine(pen, cx, cy, x, y);
    }
}

static void SaveAsIcon(List<Bitmap> images, string path)
{
    using var ms = new MemoryStream();
    // ICO header
    ms.WriteByte(0); ms.WriteByte(0); // reserved
    ms.WriteByte(1); ms.WriteByte(0); // type: icon
    ms.WriteByte((byte)images.Count); ms.WriteByte(0); // count

    int offset = 6 + 16 * images.Count;
    var imageDataList = new List<byte[]>();

    foreach (var bmp in images)
    {
        byte[] data = GetIconImageData(bmp);
        imageDataList.Add(data);

        int width = bmp.Width;
        int height = bmp.Height;

        // Directory entry
        ms.WriteByte(width >= 256 ? (byte)0 : (byte)width);
        ms.WriteByte(height >= 256 ? (byte)0 : (byte)height);
        ms.WriteByte(0); // colors
        ms.WriteByte(0); // reserved
        ms.WriteByte(1); ms.WriteByte(0); // planes
        ms.WriteByte(32); ms.WriteByte(0); // bitcount
        ms.Write(BitConverter.GetBytes(data.Length), 0, 4);
        ms.Write(BitConverter.GetBytes(offset), 0, 4);
        offset += data.Length;
    }

    foreach (var data in imageDataList)
    {
        ms.Write(data, 0, data.Length);
    }

    File.WriteAllBytes(path, ms.ToArray());
}

static byte[] GetIconImageData(Bitmap bmp)
{
    int width = bmp.Width;
    int height = bmp.Height;
    int xorSize = width * height * 4;
    int andStride = ((width + 31) / 32) * 4;
    int andSize = andStride * height;

    using var ms = new MemoryStream();
    // BITMAPINFOHEADER
    ms.Write(BitConverter.GetBytes(40), 0, 4);
    ms.Write(BitConverter.GetBytes(width), 0, 4);
    ms.Write(BitConverter.GetBytes(height * 2), 0, 4);
    ms.Write(BitConverter.GetBytes((short)1), 0, 2);
    ms.Write(BitConverter.GetBytes((short)32), 0, 2);
    ms.Write(BitConverter.GetBytes(0), 0, 4);
    ms.Write(BitConverter.GetBytes(xorSize), 0, 4);
    ms.Write(BitConverter.GetBytes(0), 0, 4);
    ms.Write(BitConverter.GetBytes(0), 0, 4);
    ms.Write(BitConverter.GetBytes(0), 0, 4);
    ms.Write(BitConverter.GetBytes(0), 0, 4);

    // XOR data (BGRA, bottom-to-top)
    for (int y = height - 1; y >= 0; y--)
    {
        for (int x = 0; x < width; x++)
        {
            var c = bmp.GetPixel(x, y);
            ms.WriteByte(c.B);
            ms.WriteByte(c.G);
            ms.WriteByte(c.R);
            ms.WriteByte(c.A);
        }
    }

    // AND mask (1 bit per pixel; 0=opaque, 1=transparent)
    for (int y = 0; y < height; y++)
    {
        var rowBits = new byte[andStride];
        for (int x = 0; x < width; x++)
        {
            var c = bmp.GetPixel(x, height - 1 - y);
            if (c.A < 128)
            {
                int byteIndex = x / 8;
                int bitIndex = 7 - (x % 8);
                rowBits[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
        ms.Write(rowBits, 0, andStride);
    }

    return ms.ToArray();
}
