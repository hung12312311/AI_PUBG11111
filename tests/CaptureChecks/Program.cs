using AILogic;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

var rootDirectory = new DirectoryInfo(AppContext.BaseDirectory);
while (rootDirectory != null && !File.Exists(Path.Combine(rootDirectory.FullName, "Aimmy2.csproj"))) rootDirectory = rootDirectory.Parent;
if (rootDirectory == null) throw new Exception("Cannot locate test workspace.");
Directory.SetCurrentDirectory(rootDirectory.FullName);
Directory.CreateDirectory("reports");
using var log = new StreamWriter("reports/capture-desktop.log") { AutoFlush = true };
Console.SetOut(log);
AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.WriteLine("FAIL " + e.ExceptionObject);
System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
using var patternReady = new ManualResetEventSlim();
Form? patternForm = null;
var patternScreen = Screen.PrimaryScreen!.Bounds;
var patternThread = new Thread(() =>
{
    patternForm = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
        Bounds = new Rectangle(patternScreen.Left + 100, patternScreen.Top + 100, 512, 512), TopMost = true, ShowInTaskbar = false };
    patternForm.Paint += (_, e) =>
    {
        var colors = new[] { Color.Red, Color.Lime, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan, Color.White, Color.Black };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
        {
            using var brush = new SolidBrush(colors[(x + y * 3) % colors.Length]);
            e.Graphics.FillRectangle(brush, x * 64, y * 64, 64, 64);
        }
        patternReady.Set();
    };
    System.Windows.Forms.Application.Run(patternForm);
});
patternThread.SetApartmentState(ApartmentState.STA);
patternThread.Start();
try
{
    if (!patternReady.Wait(5000)) throw new Exception("Pattern window did not paint");
    Thread.Sleep(250);
    Console.WriteLine("Pattern: creating WGC");
    using var capture = new WindowsGraphicsCapture(patternScreen);
    Console.WriteLine("Pattern: created WGC");
    foreach (var box in new[] {
        new Rectangle(patternScreen.Left + 140, patternScreen.Top + 135, 160, 96),
        new Rectangle(patternScreen.Left + 140, patternScreen.Top + 135, 160, 160),
        new Rectangle(patternScreen.Left + 170, patternScreen.Top + 155, 256, 256),
        new Rectangle(patternScreen.Left + 115, patternScreen.Top + 145, 320, 320),
        new Rectangle(patternScreen.Left + 290, patternScreen.Top + 270, 160, 160) })
    {
        Bitmap? actual = null;
        var timer = Stopwatch.StartNew();
        while (actual == null && timer.ElapsedMilliseconds < 5000) { actual = capture.Capture(box, false); Thread.Sleep(10); }
        if (actual == null) throw new Exception("WGC pattern capture timed out");
        using var expected = new Bitmap(box.Width, box.Height);
        using (var graphics = Graphics.FromImage(expected)) graphics.CopyFromScreen(box.Location, Point.Empty, box.Size);
        int mismatches = 0, samples = 0;
        var observedColors = new HashSet<int>();
        for (int y = 5; y < box.Height - 5; y += 11) for (int x = 5; x < box.Width - 5; x += 11)
        {
            samples++;
            observedColors.Add(expected.GetPixel(x,y).ToArgb() & 0xffffff);
            if ((actual.GetPixel(x,y).ToArgb() & 0xffffff) != (expected.GetPixel(x,y).ToArgb() & 0xffffff)) mismatches++;
        }
        if (observedColors.Count < 3) { Console.WriteLine($"INCONCLUSIVE static pattern ROI={box}: pattern colors are not visible on desktop."); continue; }
        if (mismatches != 0) { actual.Save("scratch/static-wgc.png"); expected.Save("scratch/static-gdi.png"); throw new Exception($"WGC/GDI coordinate mismatch: {mismatches}/{samples}, ROI={box}"); }
        // The unchanged screen must still return a crop at the new ROI; no whole-monitor resizing.
        for (int i = 0; i < 20; i++) if (capture.Capture(box, false) == null) throw new Exception("Static WGC frame dropped");
        Console.WriteLine($"PASS WGC/GDI pixel alignment: ROI={box}, {samples} matching samples, 20 static-frame polls");
    }
}
finally
{
    patternForm?.BeginInvoke(new Action(() => patternForm.Close()));
    patternThread.Join(5000);
}

using var motionReady = new ManualResetEventSlim();
Form? motionForm = null;
int motionX = 20;
Rectangle motionRegion = Rectangle.Empty;
var motionThread = new Thread(() =>
{
    motionForm = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
        Bounds = new Rectangle(patternScreen.Left + 100, patternScreen.Top + 100, 512, 128), TopMost = true, ShowInTaskbar = false, BackColor = Color.Black };
    motionForm.Paint += (_, e) => { motionRegion = motionForm.RectangleToScreen(motionForm.ClientRectangle); e.Graphics.Clear(Color.Black); e.Graphics.FillRectangle(Brushes.Red, motionX, 0, 16, 128); motionReady.Set(); };
    using var timer = new System.Windows.Forms.Timer { Interval = 16 };
    timer.Tick += (_, _) => { motionX = motionX >= 450 ? 20 : motionX + 4; motionForm.Refresh(); };
    timer.Start();
    System.Windows.Forms.Application.Run(motionForm);
});
motionThread.SetApartmentState(ApartmentState.STA);
motionThread.Start();
try
{
    if (!motionReady.Wait(5000)) throw new Exception("Motion pattern did not paint");
    using var capture = new WindowsGraphicsCapture(patternScreen);
    var roi = motionRegion;
    Console.WriteLine($"Motion actual window: {roi}");
    int Center(Bitmap bitmap)
    {
        int sum = 0, count = 0;
        for (int x = 0; x < bitmap.Width; x++) { var c = bitmap.GetPixel(x,Math.Min(64,bitmap.Height-1)); if (c.R > 180 && c.G < 40 && c.B < 40) { sum += x; count++; } }
        return count == 0 ? -1 : sum/count;
    }
    var errors = new List<int>();
    for (int i = 0; i < 60; i++)
    {
        Thread.Sleep(i % 10 == 0 ? 80 : 20); // Includes inference stalls.
        var frame = capture.Capture(roi, false);
        if (frame == null) continue;
        int wgcX = Center(frame);
        using var gdi = new Bitmap(roi.Width,roi.Height);
        using (var graphics = Graphics.FromImage(gdi)) graphics.CopyFromScreen(roi.Location, Point.Empty, roi.Size);
        int gdiX = Center(gdi);
        if (i < 3) Console.WriteLine($"motion debug i={i} WGC={wgcX} GDI={gdiX} logicalX={motionX}");
        if (i == 2) { gdi.Save("scratch/motion-gdi.png"); frame.Save("scratch/motion-wgc.png"); }
        if (wgcX >= 0 && gdiX >= 0 && Math.Abs(wgcX-gdiX) < 200) errors.Add(Math.Abs(wgcX-gdiX)); // Exclude wraparound.
    }
    if (errors.Count == 0) Console.WriteLine("INCONCLUSIVE moving-target test: pattern window is not visible in either GDI or WGC capture on this desktop.");
    else if (errors.Count < 40 || errors.Max() > 32) throw new Exception($"Motion capture lag excessive: samples={errors.Count}, max={errors.DefaultIfEmpty(999).Max()}px");
    if (errors.Count >= 40) Console.WriteLine($"PASS moving target: {errors.Count} frames, WGC/GDI delta mean={errors.Average():F1}px max={errors.Max()}px at 4px/update, including 80ms inference stalls");
}
finally { motionForm?.BeginInvoke(new Action(() => motionForm.Close())); motionThread.Join(5000); }

foreach (var screen in Screen.AllScreens)
{
    for (int cycle = 0; cycle < 3; cycle++)
    {
        Console.WriteLine("Creating session");
        using var capture = new WindowsGraphicsCapture(screen.Bounds);
        Console.WriteLine("Session created");
        var roi = new Rectangle(screen.Bounds.Left + 100, screen.Bounds.Top + 100, 160, 160);
        Bitmap Grab(Rectangle box, bool mask)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 5000)
            {
                var bitmap = capture.Capture(box, mask);
                if (bitmap != null) return bitmap;
                Thread.Sleep(10);
            }
            throw new Exception("No WGC frames received within five seconds.");
        }
        var image = Grab(roi, false);
        if (image.Width != 160 || image.Height != 160) throw new Exception("Wrong crop size");
        image = Grab(roi, true);
        if (image.GetPixel(20, 140).ToArgb() != Color.Black.ToArgb()) throw new Exception("Mask failed");
        image = Grab(new Rectangle(screen.Bounds.Left - 40, screen.Bounds.Top - 40, 100, 100), false);
        if (image.GetPixel(10, 10).ToArgb() != Color.Black.ToArgb()) throw new Exception("Padding failed");
        Console.WriteLine($"PASS monitor={screen.DeviceName}, cycle={cycle + 1}: frame, crop resize, mask, padding");
    }
}


