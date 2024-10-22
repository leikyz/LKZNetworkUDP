using LKZ.Network.Common.Events;
using LKZ.Network.Server.Handlers.Approach;
using LKZ.Server.Handlers.Entity;
using LKZ.Server.Managers;
using LKZ.Server.Network.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LKZ.Server.Network
{
    public static class BaseServer
    {
        public const int MAX_BUFFER_SIZE = 1024;

        private static UdpClient _udpServer;
        private static bool _isRunning;
        private static List<BaseClient> clients = new List<BaseClient>();
        public static uint NextEntityId = 0;

        public static void Start(string ipAddress, int port)
        {
            _udpServer = new UdpClient(port);
            _isRunning = true;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"({ipAddress} {port}) UDP server started, waiting for clients...");
            Console.ResetColor();

            Task.Run(() => AcceptClients());

            RegisterEvents();
        }


        private static void RegisterEvents()
        {
            EventManager.RegisterEvent("OnlinePlayersCountMessage", ApproachHandler.HandleOnlinePlayerCountMessage);
            EventManager.RegisterEvent("LobbyCreatedMessage", ApproachHandler.HandleLobbyCreatedMessage);
            EventManager.RegisterEvent("LobbyListMessage", ApproachHandler.HandleLobbyListMessage);
            EventManager.RegisterEvent("LobbyJoinedMessage", ApproachHandler.HandleLobbyJoinedMessage);

            EventManager.RegisterEvent("EntityLastPositionMessage", EntityHandler.HandleEntityLastPositionMessage);
            EventManager.RegisterEvent("EntityCreatedMessage", EntityHandler.HandleEntityCreatedMessage);
            EventManager.RegisterEvent("EntityMovementMessage", EntityHandler.HandleEntityMovementMessage);
            EventManager.RegisterEvent("EntityRotationMessage", EntityHandler.HandleEntityRotationMessage);
            EventManager.RegisterEvent("SynchronizeEntitiesMessage", EntityHandler.HandleSynchronizeEntities);
        }
        private static async Task AcceptClients()
        {
            while (_isRunning)
            {
                // Wait for a datagram
                var receivedResult = await _udpServer.ReceiveAsync();
                var clientEndpoint = receivedResult.RemoteEndPoint;

                // Check if the client is already registered
                var existingClient = clients.FirstOrDefault(c => c.Endpoint.Equals(clientEndpoint));
                if (existingClient == null)
                {
                    // Create a new UdpClient for each new client (optional, depending on your design)
                    UdpClient newUdpClient = new UdpClient();

                    Console.WriteLine(ClientsCount);
                    Console.WriteLine($"Client connected from {clientEndpoint.Address}:{clientEndpoint.Port}");
                }

                // Handle the received data
                HandleDataReceived(receivedResult.Buffer, clientEndpoint);
            }
        }


        public static void Stop()
        {
            _isRunning = false;
            _udpServer.Close();
            Console.WriteLine("UDP server stopped.");
        }

        private static void HandleDataReceived(byte[] data, IPEndPoint clientEndpoint)
        {
            string message = Encoding.ASCII.GetString(data);
            string[] messages = message.Split('~', StringSplitOptions.RemoveEmptyEntries);
            foreach (var msg in messages)
            {

                var parts = msg.Split('|');

                if (parts.Length < 2)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Format incorrect pour le message : {msg}");
                    Console.ResetColor();
                    continue;
                }

                if (parts[0] == "ClientCreatedMessage")
                {
                    BaseClient newClient = new BaseClient((uint)(clients.Count + 1), clientEndpoint);
                    AddClient(newClient);
                }
                BaseClient client = clients.First(x => x.Endpoint == clientEndpoint);



                if (client != null)
                {
                  
                    EventManager.TriggerRaw(client, $"{parts[0]}|{parts[1]}");

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"({client.Endpoint}) Message received : {parts[0]} (ID: {client.Id}, Parameters: {parts[1]})");
                    Console.ResetColor();
                }

                else
                {
                    Console.WriteLine("Client non trouvé.");
                }
            }
        }
        public static void TriggerClientEvent(int clientId, string eventName, int lobbyId = -1, params object[] parameters)
        {
            string fullMessage = EventManager.Serialize(eventName, parameters);
            Debug.WriteLine(fullMessage);
            byte[] data = Encoding.ASCII.GetBytes(fullMessage);

            if (lobbyId > 0)
            {
                Lobby lobby = LobbyManager.GetLobby(lobbyId);
                if (lobby != null)
                {
                    // Case 1: Send to all clients in the specified lobby
                    if (clientId == -1)
                    {
                        foreach (var client in lobby.Clients)
                        {
                            Console.WriteLine($" sent to {client.Endpoint}: {fullMessage}");
                            _udpServer.SendAsync(data, data.Length, client.Endpoint);
                        }
                    }
                    // Case 2: Send to a specific client in the lobby
                    else if (clientId > 0)
                    {
                        var targetClient = lobby.Clients.FirstOrDefault(c => c.Id == clientId);
                        if (targetClient != null)
                        {
                            Console.WriteLine($" sent to {targetClient.Endpoint}: {fullMessage}");
                            _udpServer.SendAsync(data, data.Length);
                        }
                        else
                        {
                            Console.WriteLine($"Client {clientId} not found in lobby {lobbyId}.");
                        }
                    }
                    // Case 3: Send to all clients in the lobby except the specified one
                    else if (clientId == -2)
                    {
                        foreach (var client in lobby.Clients)
                        {
                            if (client.Id != uint.Parse(parameters[0].ToString()))
                            {
                                Console.WriteLine($" sent to {client.Endpoint}: {fullMessage}");
                                _udpServer.SendAsync(data, data.Length, client.Endpoint);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Lobby {lobbyId} not found.");
                }
            }
            // Send to a client without lobby
            else if (clientId > 0)
            {
                var targetClient = clients.FirstOrDefault(c => c.Id == clientId);
                if (targetClient != null)
                {
                    // Send to the specific client
                    _udpServer.SendAsync(data, data.Length, targetClient.Endpoint);
                }
                else
                {
                    Console.WriteLine($"Client {clientId} not found.");
                }
            }
            else
            {
                Console.WriteLine("Invalid lobby ID.");
            }

            // Optional: Debug logging for sent messages
            // Console.ForegroundColor = ConsoleColor.Cyan;
            // Console.WriteLine($"({clientId}) Message sent: {eventName} ({parameters.Length})");
            // Console.ResetColor();
        }
        public static void SendToClient(IPEndPoint clientEndpoint, string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            _udpServer.SendAsync(data, data.Length, clientEndpoint);
        }

        public static void TriggerGlobalEvent(string eventName, params object[] parameters)
        {
            string fullMessage = EventManager.Serialize(eventName, parameters);
            Debug.WriteLine(fullMessage);
            byte[] data = Encoding.ASCII.GetBytes(fullMessage);

            // Iterate over all connected clients and send the message

            Console.WriteLine(ClientsCount + "count");
            foreach (var client in clients)
            {
                try
                {
                    Console.WriteLine($" sent to {client.Endpoint}: {fullMessage}");
                    _udpServer.SendAsync(data, data.Length, client.Endpoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message to client {client.Id}: {ex.Message}");
                }
            }
        }
        public static void AddClient(BaseClient client)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if (!clients.Contains(client))
                {
                    clients.Add(client);
                    Console.WriteLine($"({client.Endpoint}) Client with ID {client.Id} connected successfully.");
                }
                else
                {
                    Console.WriteLine($"Client with ID {client.Id} already exists.");
                }
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding client with ID {client.Id}: {ex.Message}");
            }
        }

        public static  int ClientsCount => clients.Count;
    }
}
