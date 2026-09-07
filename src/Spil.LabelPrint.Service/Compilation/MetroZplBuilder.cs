using System.Globalization;
using System.Text;
using Spil.LabelPrint.Service.Models;

namespace Spil.LabelPrint.Service.Compilation;

/// <summary>Fixed millimetre glass label (ERP / metro). Same idea as ZplLabelTester.</summary>
public static class MetroZplBuilder
{
    public static string Build(MetroLabelFields data, int dpmm, double widthMm, double heightMm)
    {
        data ??= new MetroLabelFields();
        var stock = new LabelStock(widthMm, heightMm, dpmm);
        var left = stock.MarginMm;
        var contentW = stock.ContentWidthMm;
        const double rightColW = 36.0;
        var rightColX = stock.WidthMm - stock.MarginMm - rightColW;

        var sb = new StringBuilder(2048);
        sb.AppendLine("^XA");
        sb.AppendLine("^CI28");
        sb.AppendLine(F("^PW{0}", stock.WidthDots));
        sb.AppendLine(F("^LL{0}", stock.HeightDots));
        sb.AppendLine("^LH0,0");
        sb.AppendLine("^LS0");
        sb.AppendLine("^PON");
        sb.AppendLine("^PQ1");

        sb.AppendLine(Box(stock, left, stock.MarginMm, 29, 11, 0.25));
        sb.AppendLine(Text(stock, left, stock.MarginMm + 4, 29, 2.75, "[CLIENT LOGO]", false, 'C'));

        var orderBarX = left + 33.5;
        var orderBarW = (stock.WidthMm - stock.MarginMm) - orderBarX;
        sb.AppendLine(Filled(stock, orderBarX, stock.MarginMm, orderBarW, 8.5));
        sb.AppendLine(Text(stock, orderBarX, stock.MarginMm + 1.2, orderBarW, 6.25, data.OrderNo, true, 'C'));

        sb.AppendLine(Text(stock, orderBarX, 12.5, 26, 3.5, "Cust. Ord #:", false, 'L'));
        sb.AppendLine(Text(stock, orderBarX + 26, 12.5, orderBarW - 26, 3.5, data.CustOrderNo, false, 'L'));

        sb.AppendLine(Filled(stock, left, 17.5, contentW, 5.5));
        sb.AppendLine(Text(stock, left + 1.25, 18.2, contentW - 2.5, 4.0, data.JobDescription, true, 'L'));

        sb.AppendLine(Text(stock, left, 25.0, contentW, 6.9, data.Dimensions, false, 'L'));
        sb.AppendLine(Text(stock, left, 33.0, contentW, 4.5, data.GlassSpec, false, 'L'));
        sb.AppendLine(Text(stock, left, 40.0, rightColX - left - 2, 3.5, "Mark As: " + (data.MarkAs ?? ""), false, 'L'));

        sb.AppendLine(Filled(stock, rightColX, 38.5, rightColW, 6.5));
        sb.AppendLine(Text(stock, rightColX, 39.5, rightColW, 4.5, data.DeliveryDate, true, 'C'));

        sb.AppendLine(Text(stock, left, 47.5, rightColX - left - 2, 3.5, "SQM: " + data.Sqm, false, 'L'));
        sb.AppendLine(Text(stock, left, 52.0, rightColX - left - 2, 3.5, "Line Ref : " + data.LineRef, false, 'L'));
        sb.AppendLine(Filled(stock, rightColX, 46.5, rightColW, 4.0));
        sb.AppendLine(Text(stock, rightColX, 46.8, rightColW, 3.0, "Delivery", true, 'C'));
        sb.AppendLine(Box(stock, rightColX, 50.5, rightColW, 5.5, 0.25));
        sb.AppendLine(Text(stock, rightColX, 51.2, rightColW, 3.5, data.Route, false, 'C'));

        sb.AppendLine(Filled(stock, left, 58.5, contentW, 5.5));
        sb.AppendLine(Text(stock, left, 59.2, contentW, 4.0, (data.WeightKg ?? "") + " KG's", true, 'C'));

        var barcodeTop = stock.HeightMm - 4.0 - 4.5 - 15.0;
        var barcode = ZplUtil.Sanitize(data.BarcodeValue ?? "");
        if (!string.IsNullOrEmpty(barcode))
        {
            var module = 2;
            sb.AppendLine(F("^FO{0},{1}^BY{2},2.5,{3}", stock.Dots(left + 8), stock.Dots(barcodeTop), module, stock.Dots(15)));
            sb.AppendLine(F("^BCN,{0},Y,N,N", stock.Dots(15)));
            sb.AppendLine(F("^FD{0}^FS", barcode));
        }

        var y = 66.0;
        foreach (var note in data.ProcessNotes ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(note) || y + 5 > barcodeTop) break;
            sb.AppendLine(Box(stock, left, y, 3.75, 3.75, 0.25));
            sb.AppendLine(Text(stock, left + 5, y + 0.2, contentW - 6, 3.25, note, false, 'L'));
            y += 5.0;
        }

        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    private sealed class LabelStock
    {
        public double WidthMm { get; }
        public double HeightMm { get; }
        public int Dpmm { get; }
        public double MarginMm { get; } = 2.5;
        public LabelStock(double w, double h, int dpmm)
        {
            WidthMm = w > 0 ? w : 100;
            HeightMm = h > 0 ? h : 150;
            Dpmm = dpmm is 6 or 8 or 12 or 24 ? dpmm : 12;
        }
        public int Dots(double mm) => (int)Math.Round(mm * Dpmm, MidpointRounding.AwayFromZero);
        public int WidthDots => Dots(WidthMm);
        public int HeightDots => Dots(HeightMm);
        public double ContentWidthMm => WidthMm - 2 * MarginMm;
    }

    private static string Box(LabelStock s, double x, double y, double w, double h, double t) =>
        F("^FO{0},{1}^GB{2},{3},{4}^FS", s.Dots(x), s.Dots(y), s.Dots(w), s.Dots(h), Math.Max(1, s.Dots(t)));

    private static string Filled(LabelStock s, double x, double y, double w, double h)
    {
        var hd = s.Dots(h);
        return F("^FO{0},{1}^GB{2},{3},{4},B^FS", s.Dots(x), s.Dots(y), s.Dots(w), hd, hd);
    }

    private static string Text(LabelStock s, double x, double y, double widthMm, double fontMm, string? value, bool reversed, char just)
    {
        var font = Math.Max(6, s.Dots(fontMm));
        return F("^FO{0},{1}^FB{2},1,0,{3}^A0N,{4},{4}{5}^FD{6}^FS",
            s.Dots(x), s.Dots(y), Math.Max(1, s.Dots(widthMm)), just, font,
            reversed ? "^FR" : "", ZplUtil.Sanitize(value));
    }

    private static string F(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
