using LKZ.Server.Managers;
using LKZ.Server.Network.Objects;
using System.Net;
using System.Net.Sockets;

public class BaseClient
{
    private uint id;
    private UdpClient updClient;
    private IPEndPoint endpoint; // Store IPEndPoint
    private uint playerId;
    private Lobby lobby;
    private int ping = 100; //ms

    public BaseClient(uint id, IPEndPoint endpoint) // Added IPEndPoint to constructor
    {
        Id = id;
        this.endpoint = endpoint; // Set the endpoint in the constructor
    }

    public int Ping
    {
        get { return ping; }
        set { ping = value; }
    }
    public uint Id
    {
        get { return id; }
        private set { id = value; }
    }

    public UdpClient UpdClient
    {
        get { return updClient; }
        private set { updClient = value; }
    }

    public IPEndPoint Endpoint // Property for IPEndPoint
    {
        get { return endpoint; }
        set { endpoint = value; }
    }

    public uint PlayerId
    {
        get { return playerId; }
        set { playerId = value; }
    }

    public Lobby Lobby
    {
        get { return lobby; }
        set { lobby = value; }
    }
}
