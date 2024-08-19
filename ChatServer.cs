using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Data.SQLite;

class ChatServer
{
    private static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();
    private static SQLiteConnection dbConnection;

    static void Main(string[] args)
    {
        Console.SetWindowSize(40,30);
        Console.WriteLine("Starting server...");
        StartDatabase();
        StartServer();
    }


    //Method that will start the database by creating it first if does not exist
    static void StartDatabase()
    {
        dbConnection = new SQLiteConnection("Data Source=ChatDatabase.sqlite;Version=3;");
        dbConnection.Open();

        //Preparing the database
        string createTableQuery = @"CREATE TABLE IF NOT EXISTS Messages (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Username TEXT,
                                    Message TEXT,
                                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP)";
        SQLiteCommand command = new SQLiteCommand(createTableQuery, dbConnection);
        
        //Execute database creation if it does not exist.
        command.ExecuteNonQuery();
    }

    //Starts the server where the users can connect to chat
    static void StartServer()
    {
        //Use TCP to allow connection between the server and the users.
        TcpListener server = new TcpListener(IPAddress.Any, 8888);
        server.Start();

        Console.WriteLine("Server started on port 8888...");

        while (true)
        {
            //Accept client conenction
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream(); 
        byte[] buffer = new byte[1024];
        string username = "";

        int bytesRead = stream.Read(buffer, 0, buffer.Length); //sets the bytes to be read in the network stream
        username = Encoding.ASCII.GetString(buffer, 0, bytesRead); //gets the username set by the user, byte to ASCII encoded string

        //Focus in this thread when a user connects to the server
        lock (clients)
        {
            clients[username] = client;
            Console.WriteLine($"{username} has connected.");
            Broadcast($"{username} has joined the chat.", "Server");
        }

        //Gets the message sent by the users
        while (true)
        {
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Broadcast(message, username);
                SaveMessageToDatabase(username, message);
            }
            catch
            {
                break;
            }
        }

        //Focus on this thread when a user diconnects
        lock (clients)
        {
            clients.Remove(username);
            Broadcast($"{username} has left the chat.", "Server");
            Console.WriteLine($"{username} has disconnected.");
        }
    }

    //Broadcast the messages sent by the users.
    static void Broadcast(string message, string username)
    {
        byte[] buffer = Encoding.ASCII.GetBytes($"{username}: {message}");

        lock (clients)
        {
            foreach (var client in clients.Values)
            {
                NetworkStream stream = client.GetStream();
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }

    //Save messages in the database
    static void SaveMessageToDatabase(string username, string message)
    {
        string query = "INSERT INTO Messages (Username, Message) VALUES (@username, @message)";
        SQLiteCommand command = new SQLiteCommand(query, dbConnection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@message", message);
        command.ExecuteNonQuery();
    }
}
