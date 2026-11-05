using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace PaletMon.UI;

public static class Brand
{
    // Paletinho: a pallet seen from the front; the fork pockets are its eyes.
    public static Control Logo(double width)
    {
        var canvas = new Canvas { Width = 64, Height = 44 };
        void Rect(double x, double y, double w, double h, string key, double r = 0, IBrush fixedBrush = null)
        {
            var rect = new Rectangle { Width = w, Height = h, RadiusX = r, RadiusY = r };
            if (fixedBrush != null) rect.Fill = fixedBrush; else rect.Res(Shape.FillProperty, key);
            Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }
        Rect(0, 2, 64, 9, "Wood", 1.5);
        foreach (var x in new[] { 0, 24, 48 }) Rect(x, 11, 16, 17, "Wood");
        Rect(16, 11, 8, 17, "Ink", 2);
        Rect(40, 11, 8, 17, "Ink", 2);
        Rect(17.5, 13, 3, 4, null, 1, Brushes.White);
        Rect(41.5, 13, 3, 4, null, 1, Brushes.White);
        var grain = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M6,6.5 H24 M32,5 H44 M48,7.5 H58"), StrokeThickness = 1.2, StrokeLineCap = PenLineCap.Round }.Res(Shape.StrokeProperty, "WoodDark");
        canvas.Children.Add(grain);
        var smile = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M28,20.5 Q32,24.5 36,20.5"), StrokeThickness = 2, StrokeLineCap = PenLineCap.Round }.Res(Shape.StrokeProperty, "Ink");
        canvas.Children.Add(smile);
        Rect(0, 28, 64, 8, "WoodDark", 1.5);
        Rect(4, 36, 10, 5, "WoodDark", 1);
        Rect(50, 36, 10, 5, "WoodDark", 1);
        return new Viewbox { Width = width, Child = canvas };
    }

    public static Control Wordmark(double size, string color = "Fg")
    {
        var palet = Ui.Text("Palet", size, FontWeight.Normal, color, false).With(t => t.FontFamily = Ui.Display);
        var mon = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 2, 8, 0), Child = Ui.Text("Mon", size, FontWeight.Normal, "AccentFg", false).With(t => t.FontFamily = Ui.Display) }
            .Res(Border.BackgroundProperty, "Accent");
        return Ui.Row(6, palet, mon);
    }
}

// Yellow/black floor-marking stripe.
public sealed class HazardStripe : Control
{
    static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#f2b300"));
    static readonly IBrush Black = new SolidColorBrush(Color.Parse("#1f1c19"));

    public bool Rounded { get; set; } = true;

    public HazardStripe() { Height = 10; }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        using (ctx.PushClip(new RoundedRect(new Rect(0, 0, w, h), Rounded ? 5 : 0)))
        {
            ctx.FillRectangle(Yellow, new Rect(0, 0, w, h));
            const double band = 14;
            for (double x = -h; x < w + h; x += band * 2)
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(x, h), true);
                    c.LineTo(new Point(x + band, h));
                    c.LineTo(new Point(x + band + h, 0));
                    c.LineTo(new Point(x + h, 0));
                    c.EndFigure(true);
                }
                ctx.DrawGeometry(Black, null, g);
            }
        }
    }
}
