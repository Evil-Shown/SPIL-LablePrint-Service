using System.Net.Sockets;
using System.Text;

namespace Spil.LabelPrint.Service.Compilation;

public sealed class PrinterTcpSender
{
    public void Send(string host, int port, string zpl)
    {
        host = (host ?? "").Trim();
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("Printer host is empty.");
        if (string.IsNullOrWhiteSpace(zpl))
            throw new ArgumentException("ZPL is empty.");
        if (port <= 0) port = 9100;

        using var client = new TcpClient();
        client.SendTimeout = 8000;
        client.ReceiveTimeout = 8000;
        client.Connect(host, port);
        using var stream = client.GetStream();
        var bytes = Encoding.UTF8.GetBytes(zpl);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
}
