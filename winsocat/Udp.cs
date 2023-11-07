using System.Net.Sockets;
using System.Net;

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

public class UdpListenPiper : IListenPiper
{
    private UdpListener _server;

    public UdpListenPiper(UdpListener server)
    {
        _server = server;
        _server.Start();
    }
    
    public UdpListenPiper(IPAddress address, int port) : this(new UdpListener(address, port)) {}

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
    private UdpClient _client;

    public UdpStreamPiper(UdpClient client) : base(client.GetStream())
    {
        _client = client;
    }
    
    public UdpStreamPiper(string host, int port) : this(new UdpClient(host, port)) {}

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