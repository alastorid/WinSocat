using System.Net.Sockets;
using System.Net;
using System.ComponentModel;
using ObjectDumper;
using System.Diagnostics;
using System;
using System.Reflection.PortableExecutable;

namespace Firejox.App.WinSocat;

public class UdpStreamPiperInfo
{
    private readonly string _host;
    private readonly int _port;

    public string Host => _host;
    public int Port => _port;

    public UdpStreamPiperInfo(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public static UdpStreamPiperInfo TryParse(AddressElement element)
    {
        if (!element.Tag.Equals("UDP", StringComparison.OrdinalIgnoreCase)) return null!;

        string host;
        int sepIndex = element.Address.LastIndexOf(':');

        if (sepIndex == -1 || sepIndex == 0)
            host = "0.0.0.0";
        else
            host = element.Address.Substring(0, sepIndex);

        int port = Int32.Parse(element.Address.Substring(sepIndex + 1));

        return new UdpStreamPiperInfo(host, port);
    }
}

public class UdpListenPiperInfo
{
    private readonly IPAddress _address;
    private readonly int _port;

    public IPAddress Address => _address;
    public int Port => _port;

    public UdpListenPiperInfo(IPAddress address, int port)
    {
        _address = address;
        _port = port;
    }

    public static UdpListenPiperInfo TryParse(AddressElement element)
    {
        if (element.Tag.Equals("UDP-LISTEN", StringComparison.OrdinalIgnoreCase))
        {
            IPAddress address;
            int sepIndex = element.Address.LastIndexOf(':');

            if (sepIndex == -1 || sepIndex == 0)
                address = IPAddress.Any;
            else
                address = IPAddress.Parse(element.Address.Substring(0, sepIndex));

            int port = Int32.Parse(element.Address.Substring(sepIndex + 1));
            return new UdpListenPiperInfo(address, port);
        }

        return null!;
    }
}

//
// A special UdpClient that care about WinDBG more about security risk
// Side Note: WinDBG KD UDP traffic are encrypted.
//
public class UdpClientEx : UdpClient
{
    private Stream _stream;
    private IPEndPoint _remoteEndPoint;
    public IPEndPoint RemoteEndPoint
    {
        get
        {
            return _remoteEndPoint;
        }
        set
        {
            _remoteEndPoint = value;
        }
    }

    // Used by listener
    public UdpClientEx() : base()
    {
        _remoteEndPoint = null!;
        _stream = Stream.Null;
    }

    // Used by client 
    public UdpClientEx(string hostname, int port) : base(port)
    {
        // Console.WriteLine("UdpClientEx!");
        base.Connect(hostname, port);
        _remoteEndPoint = base.Client.RemoteEndPoint as IPEndPoint ?? throw new Exception("Help me!!!");
        _stream = new FakeUdpNetworkStream(this);
    }

    // Used by listener
    public UdpClientEx(IPEndPoint localEP) : base(localEP)
    {
        _remoteEndPoint = null!;
        _stream = Stream.Null;
    }

    // .oooooo..o oooooooooooo   .oooooo.   ooooo     ooo ooooooooo.   ooooo ooooooooooooo oooooo   oooo      ooooo   ooooo   .oooooo.   ooooo        oooooooooooo 
    //d8P'    `Y8 `888'     `8  d8P'  `Y8b  `888'     `8' `888   `Y88. `888' 8'   888   `8  `888.   .8'       `888'   `888'  d8P'  `Y8b  `888'        `888'     `8 
    //Y88bo.       888         888           888       8   888   .d88'  888       888        `888. .8'         888     888  888      888  888          888         
    // `"Y8888o.   888oooo8    888           888       8   888ooo88P'   888       888         `888.8'          888ooooo888  888      888  888          888oooo8    
    //     `"Y88b  888    "    888           888       8   888`88b.     888       888          `888'           888     888  888      888  888          888    "    
    //oo     .d8P  888       o `88b    ooo   `88.    .8'   888  `88b.   888       888           888            888     888  `88b    d88'  888       o  888       o 
    //8""88888P'  o888ooooood8  `Y8bood8P'     `YbodP'    o888o  o888o o888o     o888o         o888o          o888o   o888o  `Y8bood8P'  o888ooooood8 o888ooooood8 
    //
    // Note: It records the last RemoteEP to send data to!!!
    // It is intended to make WinDBG Kernel Debug over internet work
    // 
    public new async Task<UdpReceiveResult> ReceiveAsync()
    {
        var result = await base.ReceiveAsync();
        this.RemoteEndPoint = result.RemoteEndPoint;
        return result;
    }
    public Stream GetStream()
    {
        if (_stream == Stream.Null)
        {
            _stream = new FakeUdpNetworkStream(this);
        }
        return _stream;
    }

    public new void Connect(IPEndPoint endPoint)
    {
        this.FakeConnect(endPoint, false);
    }

    public void FakeConnect(IPEndPoint endPoint, bool force = false)
    {
        // If not force, get UDP from everywhere
        if (force)
        {
            base.Connect(endPoint);
        }
        _remoteEndPoint = endPoint;
        base.Active = true;
    }
}

public static class whatever
{
    public static void k()
    {
        // Console.WriteLine(new System.Diagnostics.StackTrace());
    }
    public static void AddRange(this List<byte> me, byte[] buffer, int offset, int count)
    {
        var newbuffer = new byte[count];
        Buffer.BlockCopy(buffer, offset, newbuffer, 0, count);
        me.AddRange(newbuffer);
    }
}

public class FakeUdpNetworkStream : Stream
{
    private readonly UdpClientEx _udpClient;
    private IPEndPoint _remoteEndPoint;

    public FakeUdpNetworkStream(UdpClientEx udpClient)
    {
        _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
        _remoteEndPoint = udpClient.RemoteEndPoint as IPEndPoint ?? null!;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length
    {
        get
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
            return 5566;
        }
    }

    public override long Position
    {
        get
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
            return 5566;
        }
        set
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        }
    }

    public override void Flush()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        //throw new NotImplementedException();
    }

    // I have no buffer
    // Data loss if buffer too small
    public override int Read(byte[] buffer, int offset, int count)
    {
        var actual_copy = 0;
        var data = _udpClient.Receive(ref _remoteEndPoint);
        if (data != null)
        {
            actual_copy = data.Length < count ? data.Length : count;
            Buffer.BlockCopy(data, 0, buffer, offset, actual_copy);
        }
        return actual_copy;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        throw new NotImplementedException();
    }
    const int magic = 1024;
    List<byte> _writeBuffer = new List<byte>();
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Console.WriteLine("Write(byte[] buffer, int offset, int count)!");

        _writeBuffer.AddRange(buffer, offset, count);
        var theCount = Math.Min(magic, count - offset);
        var dataToSend = _writeBuffer.GetRange(0, theCount).ToArray();
        var bytesSent = _udpClient.Send(dataToSend, dataToSend.Length, _remoteEndPoint);
        _writeBuffer.RemoveRange(0, bytesSent);
    }
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Console.WriteLine("WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)!");
        // Console.WriteLine($"send to {_remoteEndPoint}");
        var d = new ReadOnlyMemory<byte>(buffer, offset, count);
        try
        {
            if (_udpClient.RemoteEndPoint != null)
            {
                await _udpClient.SendAsync(d, cancellationToken);
            }
            else
            {
                await _udpClient.SendAsync(d, _remoteEndPoint, cancellationToken);
            }
        }
        catch (Exception e)
        {
            // Console.WriteLine(e);
        }
        // Console.WriteLine("hihi");
    }
    public void bind_it_now()
    {
        try
        {
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _udpClient.RemoteEndPoint.Port));
        }
        catch (Exception e)
        {
            //Console.WriteLine(e);
        }
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Console.WriteLine("WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)!");
        await this.WriteAsync(buffer.Span.ToArray(), 0, buffer.Length, cancellationToken);        
    }
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.BeginRead(buffer, offset, count, callback, state);
    }
    public override void Close()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        base.Close();
    }
    public override ValueTask DisposeAsync()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.DisposeAsync();
    }
    public override int EndRead(IAsyncResult asyncResult)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.EndRead(asyncResult);
    }
    public override bool Equals(object? obj)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.Equals(obj);
    }
    public override int GetHashCode()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.GetHashCode();
    }
    public override object InitializeLifetimeService()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.InitializeLifetimeService();
    }

    public override string ToString()
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.ToString();
    }
    public override bool CanTimeout
    {
        get
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
            return base.CanTimeout;
        }
    }
    public override int ReadTimeout
    {
        get
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
            return base.ReadTimeout;
        }
        set
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod()); base.ReadTimeout = value;
        }
    }
    public override int WriteTimeout
    {
        get
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
            return base.WriteTimeout;
        }
        set
        {
            // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod()); base.WriteTimeout = value;
        }
    }
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod());
        return base.FlushAsync(cancellationToken);
    }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        // Console.WriteLine("Write(ReadOnlySpan<byte> buffer)!");

        bind_it_now();
        // Console.WriteLine("Write!");
        base.Write(buffer);
    }
    public override void WriteByte(byte value)
    {
        // Console.Write("WriteByte");
        base.WriteByte(value);
    }
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        // Console.Write("BeginWrite!");
        return base.BeginWrite(buffer, offset, count, callback, state);
    }
    public override void EndWrite(IAsyncResult asyncResult)
    {
        // Console.WriteLine("EndWrite!");
        base.EndWrite(asyncResult);
    }
    public override void CopyTo(Stream destination, int bufferSize)
    {
        // Console.WriteLine("CopyTo!");
        base.CopyTo(destination, bufferSize);
    }
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        // Console.WriteLine("CopyToAsync!");
        return base.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    private byte[] ReadAsyncBuffer;
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Console.WriteLine("ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)!");

        bind_it_now();

        try
        {
            var res = await _udpClient.ReceiveAsync(cancellationToken);
            var actualRead = 0;

            _remoteEndPoint = res.RemoteEndPoint;

            if (ReadAsyncBuffer != null)
            {
                int toCopy = Math.Min(count, ReadAsyncBuffer.Length);
                Buffer.BlockCopy(ReadAsyncBuffer, 0, buffer, offset, toCopy);
                actualRead += toCopy;

                if (ReadAsyncBuffer.Length > count)
                {
                    byte[] newAsyncBuffer = new byte[ReadAsyncBuffer.Length - count];
                    Buffer.BlockCopy(ReadAsyncBuffer, count, newAsyncBuffer, 0, newAsyncBuffer.Length);
                    ReadAsyncBuffer = newAsyncBuffer;
                }
                else
                {
                    ReadAsyncBuffer = null;
                }
            }

            if (actualRead < count && res.Buffer.Length > 0)
            {
                int remaining = count - actualRead;
                int toCopy = Math.Min(remaining, res.Buffer.Length);
                Buffer.BlockCopy(res.Buffer, 0, buffer, offset + actualRead, toCopy);
                actualRead += toCopy;

                if (res.Buffer.Length > remaining)
                {
                    int existingLength = ReadAsyncBuffer?.Length ?? 0;
                    byte[] newAsyncBuffer = new byte[existingLength + res.Buffer.Length - remaining];

                    if (ReadAsyncBuffer != null)
                    {
                        Buffer.BlockCopy(ReadAsyncBuffer, 0, newAsyncBuffer, 0, existingLength);
                    }

                    Buffer.BlockCopy(res.Buffer, remaining, newAsyncBuffer, existingLength, newAsyncBuffer.Length - existingLength);
                    ReadAsyncBuffer = newAsyncBuffer;
                }
            }
            // Console.WriteLine($@"actualRead = {actualRead}");
            return actualRead;
        }
        catch (Exception ex)
        {
            // Console.WriteLine(ex.ToString());
        }
        return 0;
    }
    public override int Read(Span<byte> buffer)
    {
        // Console.WriteLine("Read!");
        return base.Read(buffer);
    }
    public override int ReadByte()
    {
        // Console.WriteLine("ReadByte!");
        return base.ReadByte();
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Console.WriteLine("ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)!");

        bind_it_now();
        var buff = new byte[buffer.Length];
        var res = await this.ReadAsync(buff, 0, buff.Length, cancellationToken);
        buff.CopyTo(buffer.Span);
        return res;
    }

}

public class FakeUdpListener
{
    private UdpClientEx _udpClient;
    private IPEndPoint _localEndPoint;
    private bool _isListening;
    private long _gaveOneClient;

    public FakeUdpListener(IPAddress localaddr, int port)
    {
        _localEndPoint = new IPEndPoint(localaddr, port);
        // Console.WriteLine(_localEndPoint);
        _udpClient = new UdpClientEx(_localEndPoint);
        _gaveOneClient = 0;
    }

    public void Start()
    {
        _isListening = true;
    }

    public void Stop()
    {
        // Console.WriteLine("Stop");
        _isListening = false;
        _udpClient.Close();
    }

    public async Task<UdpClientEx> AcceptUdpClientAsync()
    {
        if (!_isListening)
        {
            throw new InvalidOperationException("Listener is not started.");
        }

        if (0 != Interlocked.CompareExchange(ref _gaveOneClient, 1, 0))
        {
            return await new TaskCompletionSource<UdpClientEx>().Task;
        }
        return _udpClient;
    }

    public UdpClientEx AcceptUdpClient()
    {
        return AcceptUdpClientAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _udpClient?.Dispose();
    }
}

public class UdpListenPiper : IListenPiper
{
    private FakeUdpListener _server;

    public UdpListenPiper(FakeUdpListener server)
    {
        _server = server;
        _server.Start();
    }

    public UdpListenPiper(IPAddress address, int port) : this(new FakeUdpListener(address, port)) { }

    public IPiper NewIncomingPiper()
    {
        return new UdpStreamPiper(_server.AcceptUdpClient());
    }

    public async Task<IPiper> NewIncomingPiperAsync()
    {
        var client = await _server.AcceptUdpClientAsync();
        return new UdpStreamPiper(client);
    }

    public void Close()
    {
        _server.Stop();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            if (disposing && _server != null)
                _server.Stop();
        }
        finally
        {
            _server = null!;
        }
    }
}

public class UdpListenPiperStrategy : ListenPiperStrategy
{
    private readonly UdpListenPiperInfo _info;
    public UdpListenPiperInfo Info => _info;

    public UdpListenPiperStrategy(UdpListenPiperInfo info)
    {
        _info = info;
    }

    protected override IListenPiper NewListenPiper()
    {
        return new UdpListenPiper(_info.Address, _info.Port);
    }

    public static UdpListenPiperStrategy TryParse(AddressElement element)
    {
        UdpListenPiperInfo info;

        if ((info = UdpListenPiperInfo.TryParse(element)) != null)
            return new UdpListenPiperStrategy(info);

        return null!;
    }
}

public class UdpStreamPiperStrategy : PiperStrategy
{
    private readonly UdpStreamPiperInfo _info;
    public UdpStreamPiperInfo Info => _info;

    public UdpStreamPiperStrategy(UdpStreamPiperInfo info)
    {
        _info = info;
    }

    protected override IPiper NewPiper()
    {
        return new UdpStreamPiper(_info.Host, _info.Port);
    }

    public static UdpStreamPiperStrategy TryParse(AddressElement element)
    {
        UdpStreamPiperInfo info;

        if ((info = UdpStreamPiperInfo.TryParse(element)) != null)
            return new UdpStreamPiperStrategy(info);

        return null!;
    }
}

public class UdpStreamPiper : StreamPiper
{
    private UdpClientEx _client;

    public UdpStreamPiper(UdpClientEx client) : base(client.GetStream())
    {
        _client = client;
    }

    public UdpStreamPiper(string host, int port) : this(new UdpClientEx(host, port)) { }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
            if (disposing && _client != null)
            {
                _client.Dispose();
            }
        }
        finally
        {
            _client = null!;
        }
    }
}

public class UdpStreamPiperFactory : IPiperFactory
{
    private readonly UdpStreamPiperInfo _info;
    public UdpStreamPiperInfo Info => _info;

    public UdpStreamPiperFactory(UdpStreamPiperInfo info)
    {
        _info = info;
    }

    public IPiper NewPiper()
    {
        // Console.WriteLine("newPiper!!!");
        return new UdpStreamPiper(_info.Host, _info.Port);
    }

    public static UdpStreamPiperFactory TryParse(AddressElement element)
    {
        UdpStreamPiperInfo info;

        if ((info = UdpStreamPiperInfo.TryParse(element)) != null)
            return new UdpStreamPiperFactory(info);

        return null!;
    }
}