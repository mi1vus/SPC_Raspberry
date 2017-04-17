using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ProjectSummer.Repository
{
    public class TelnetServer
    {
        private enum EClientState
        {
            NotLogged = 0,
            Logging = 1,
            LoggedIn = 2
        }

        private class Client
        {
            private int commandIndex = 0;
            public int CommandIndex
            {
                get
                {
                    return commandIndex;
                }
                set
                {
                    if (value < 0)
                        commandIndex = 0;
                    else if (value >= LastCommand.Count)
                        commandIndex = LastCommand.Count - 1;
                    else
                        commandIndex = value;
                    Console.WriteLine(commandIndex);
                }
            }
            public string Path = "";
            public List<string> LastCommand = new List<string>();
            public IPEndPoint remoteEndPoint;
            public DateTime connectedAt;
            public EClientState clientState;
            public string commandIssued = string.Empty;

            public Client(IPEndPoint _remoteEndPoint, DateTime _connectedAt, EClientState _clientState)
            {
                this.remoteEndPoint = _remoteEndPoint;
                this.connectedAt = _connectedAt;
                this.clientState = _clientState;
            }
        }

        public string ServerName
        {
            get;
            private set;
        }
        public string Password
        {
            get;
            private set;
        }
        private Socket serverSocket;
        private byte[] data = new byte[dataSize];
        private bool newClients = true;
        private const int dataSize = 1024;
        private int send_data_len = 64;
        private Dictionary<Socket, Client> clientList = new Dictionary<Socket, Client>();
        private Encoding enc = Encoding.GetEncoding(866);

        public TelnetServer(int Port, string ServerName, string Password, RunCommandDelegate RunCommand)
        {
            this.ServerName = ServerName;
            this.Password = Password;
            this.RunCommand = RunCommand;

            Console.WriteLine("Starting...");
            //new Thread(new ThreadStart(backgroundThread)) { IsBackground = true }.Start();
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, Port);
            serverSocket.Bind(endPoint);
            serverSocket.Listen(0);
            serverSocket.BeginAccept(new AsyncCallback(AcceptConnection), serverSocket);
            Console.WriteLine("Server socket listening to upcoming connections.");
        }

        private void backgroundThread()
        {
            while (true)
            {
                string Input = Console.ReadLine();

                if (Input == "clients")
                {
                    if (clientList.Count == 0) continue;
                    int clientNumber = 0;
                    foreach (KeyValuePair<Socket, Client> client in clientList)
                    {
                        Client currentClient = client.Value;
                        clientNumber++;
                        Console.WriteLine(string.Format("Client #{0} (From: {1}:{2}, ECurrentState: {3}, Connection time: {4})", clientNumber,
                            currentClient.remoteEndPoint.Address.ToString(), currentClient.remoteEndPoint.Port, currentClient.clientState, currentClient.connectedAt));
                    }
                }

                if (Input.StartsWith("kill"))
                {
                    string[] _Input = Input.Split(' ');
                    int clientID = 0;
                    try
                    {
                        if (Int32.TryParse(_Input[1], out clientID) && clientID >= clientList.Keys.Count)
                        {
                            int currentClient = 0;
                            foreach (Socket currentSocket in clientList.Keys.ToArray())
                            {
                                currentClient++;
                                if (currentClient == clientID)
                                {
                                    currentSocket.Shutdown(SocketShutdown.Both);
                                    currentSocket.Close();
                                    clientList.Remove(currentSocket);
                                    Console.WriteLine("Client has been disconnected and cleared up.");
                                }
                            }
                        }
                        else { Console.WriteLine("Could not kick client: invalid client number specified."); }
                    }
                    catch { Console.WriteLine("Could not kick client: invalid client number specified."); }
                }

                if (Input == "killall")
                {
                    int deletedClients = 0;
                    foreach (Socket currentSocket in clientList.Keys.ToArray())
                    {
                        currentSocket.Shutdown(SocketShutdown.Both);
                        currentSocket.Close();
                        clientList.Remove(currentSocket);
                        deletedClients++;
                    }

                    Console.WriteLine("{0} clients have been disconnected and cleared up.", deletedClients);
                }

                if (Input == "lock") { newClients = false; Console.WriteLine("Refusing new connections."); }
                if (Input == "unlock") { newClients = true; Console.WriteLine("Accepting new connections."); }
            }
        }
        private const string line = "*************************************************************************************************************";
        private void AcceptConnection(IAsyncResult result)
        {
            if (!newClients) return;
            Socket oldSocket = (Socket)result.AsyncState;
            Socket newSocket = oldSocket.EndAccept(result);
            Client client = new Client((IPEndPoint)newSocket.RemoteEndPoint, DateTime.Now, EClientState.NotLogged);
            clientList.Add(newSocket, client);
            Console.WriteLine("Client connected. (From: " + string.Format("{0}:{1}", client.remoteEndPoint.Address.ToString(), client.remoteEndPoint.Port) + ")");
            string output =  $"{ServerName}\r\n{line}\n\r\n\r";
            output += "Введите пароль: ";

            client.clientState = EClientState.Logging;
            byte[] message = enc.GetBytes(output);
            newSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), newSocket);
            serverSocket.BeginAccept(new AsyncCallback(AcceptConnection), serverSocket);
        }

        private void SendData(IAsyncResult result)
        {
            try
            {
                if (result.AsyncState is partialMessage)
                {
                    var info = (partialMessage)result.AsyncState;
                    if (info.cursor < info.Message.Length)
                    {
                        System.Threading.Thread.Sleep(20);
                        var array = info.Message[info.cursor++];
                        info.clientSocket.BeginSend(array, 0, array.Length, SocketFlags.None, new AsyncCallback(SendData), result.AsyncState);
                    }
                    else
                    {
                        info.clientSocket.EndSend(result);
                        info.clientSocket.BeginReceive(data, 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), info.clientSocket);
                    }

                }
                else
                {
                    Socket clientSocket = (Socket)result.AsyncState;
                    clientSocket.EndSend(result);
                    clientSocket.BeginReceive(data, 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                }
            }
            catch { }
        }
        class partialMessage
        {
            public partialMessage(byte[] message, int packLen)
            {
                int z = 0;
                Message = new byte[(int)Math.Ceiling(((double)message.Length) / packLen)][];
                for (z = 0; z < Message.Length; z++)
                {
                    if (z != Message.Length - 1)
                        Message[z] = new byte[packLen];
                    else
                        Message[z] = new byte[message.Length - (z * packLen)];
                    Array.Copy(message, z * packLen, Message[z], 0, Message[z].Length);
                }
            }
            public byte[][] Message;
            public int cursor;
            public Socket clientSocket;
        }
        private void disconnectClient(Socket clientSocket)
        {
            try
            {
                Client client;
                clientList.TryGetValue(clientSocket, out client);

                clientSocket.Close();
                clientList.Remove(clientSocket);
                serverSocket.BeginAccept(new AsyncCallback(AcceptConnection), serverSocket);
                Console.WriteLine("Client disconnected. (From: " + string.Format("{0}:{1}", client.remoteEndPoint.Address.ToString(), client.remoteEndPoint.Port) + ")");
            }
            catch { }
        }
        private void ReceiveData(IAsyncResult result)
        {
            try
            {
                var data = this.data;

                Socket clientSocket = (Socket)result.AsyncState;
                Client client;
                clientList.TryGetValue(clientSocket, out client);
                int received = clientSocket.EndReceive(result);
                if (received == 0)
                {
                    disconnectClient(clientSocket);
                    return;
                }

                Console.WriteLine("Received '{0}' (From: {1}:{2})", BitConverter.ToString(data, 0, received), client.remoteEndPoint.Address.ToString(), client.remoteEndPoint.Port);

                // 0x2E & 0X0D => return/intro
                if (data[0] == 0x2E && data[1] == 0x0D && client.commandIssued.Length == 0)
                {
                    string currentCommand = client.commandIssued;
                    Console.WriteLine(string.Format("Received '{0}' while EClientStatus '{1}' (From: {2}:{3})", currentCommand, client.clientState.ToString(), client.remoteEndPoint.Address.ToString(), client.remoteEndPoint.Port));
                    client.commandIssued = "";
                    byte[] message = enc.GetBytes(/*"\u001B[1J\u001B[H" +*/ HandleCommand(clientSocket, currentCommand));
                    clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), clientSocket);
                }

                else if (data[0] == 0x0D && data[1] == 0x0A)
                {
                    string currentCommand = client.commandIssued;
                    Console.WriteLine(string.Format("Received CMD: '{0}' (From: {1}:{2}", currentCommand, client.remoteEndPoint.Address.ToString(), client.remoteEndPoint.Port));
                    client.commandIssued = "";
                    byte[] message = enc.GetBytes(/*"\u001B[1J\u001B[H" + */HandleCommand(clientSocket, currentCommand));
                    partialMessage pmessage = new partialMessage(message, send_data_len) { clientSocket = clientSocket };
                    var array = pmessage.Message[pmessage.cursor++];
                    clientSocket.BeginSend(array, 0, array.Length, SocketFlags.None, new AsyncCallback(SendData), pmessage);
                    //clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), clientSocket);
                }
                else
                {
                    // 0x08 => remove character
                    if (data[0] == 0x08)
                    {
                        if (client.commandIssued.Length > 0)
                        {
                            client.commandIssued = client.commandIssued.Substring(0, client.commandIssued.Length - 1);
                            byte[] message = enc.GetBytes("\u0020\u0008");
                            clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), clientSocket);
                        }
                        else
                        {
                            byte[] message = enc.GetBytes(" ");
                            clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), clientSocket);

                            //                            clientSocket.BeginReceive(new byte[dataSize], 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                    }
                    //else if(data[0] == 0x1B)
                    //{
                    //    clientSocket.BeginReceive(new byte[dataSize], 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                    //}
                    // 0x7F => delete character                    
                    else if (data[0] == 0x7F)
                    {
                        clientSocket.BeginReceive(this.data, 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                    }
                    else
                    {
                        string currentCommand = client.commandIssued;
                        if (data[0] != 0x1B)
                        {
                            client.commandIssued += enc.GetString(data, 0, received);
                        }
                        else if (data[1] == 0x5B)
                        {
                            var len = client.commandIssued.Length;
                            if ((data[2] == 0x41 || data[2] == 0x42) && client.LastCommand.Count > 0)
                            {
                                if (client.commandIssued != "")
                                {
                                    if (data[2] == 0x41)
                                        client.CommandIndex = client.CommandIndex - 1;
                                    else if (data[2] == 0x42)
                                        client.CommandIndex = client.CommandIndex + 1;
                                }
                                client.commandIssued = client.LastCommand[client.CommandIndex];
                                var answer = "";
                                for (int z = 0; z < len; z++)
                                    answer += "\b \b";
                                answer += client.commandIssued;
                                byte[] message = enc.GetBytes(answer);
                                clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendData), clientSocket);
                                return;
                            }
                        }
                        clientSocket.BeginReceive(this.data, 0, dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                    }
                }
            }
            catch { }
        }
        private string promo
        {
            get
            {
                if (Promo == null || Promo == "")
                    return $"\r\n{System.Environment.MachineName}";
                else
                    return $"\r\n"+Promo;
            }
        }
        public string Promo
        {
            get; set;
        }

        private string HandleCommand(Socket clientSocket, string Input)
        {

            string Output = "";
            byte[] dataInput = enc.GetBytes(Input);
            Client client;
            clientList.TryGetValue(clientSocket, out client);

            /*if (client.clientState == EClientState.NotLogged)
            {
                Console.WriteLine("Client not logged in, marking login operation in progress...");
                client.clientState = EClientState.Logging;
                Output += "Please input your password:\n\r";
            }*/
            if (client.clientState == EClientState.Logging)
            {
                if (Input == (Password ?? ""))
                {
                    Console.WriteLine("Client has logged in (correct password), marking as logged...");
                    client.clientState = EClientState.LoggedIn;
                    Output += "\n\rУспешный вход.\n\r" + promo+ "# ";
                }
                else
                {
                    Console.WriteLine("Client login failed (incorrect password).");
                    Output += "\n\rНеверный пароль.\r\nВведите корректный пароль: ";
                }
            }
            else if (client.clientState == EClientState.LoggedIn)
            {
                if (Input != "" && Input != null)
                {
                    if (!(client.LastCommand.Count > 0 && client.LastCommand[client.LastCommand.Count - 1] == Input))
                    {
                        client.LastCommand.Add(Input);
                        client.CommandIndex = client.LastCommand.Count - 1;
                    }
                }

                if (Input == "ping")
                {
                    Output += "ping ok";
                }
                if (Input == "clear")
                {
                    Output += "\u001B[1J\u001B[H";
                }
                else if (Input == "exit")
                {
                    if(client.Path == "")
                        disconnectClient(clientSocket);
                    else
                    {
                        if (client.Path.Contains('.'))
                            client.Path = client.Path.Remove(client.Path.LastIndexOf('.'));
                        else
                            client.Path = "";
                    }
                }
                else
                {
                    try
                    {
                        //string Path = "";
                        Output += RunCommand(Input, ref client.Path);
                        //client.Path = Path;
                    }
                    catch { }
                }
                Output += promo + ((client.Path != "")?"."+client.Path+"":"") + "# ";
            }
            return Output;
        }
        public delegate string RunCommandDelegate(string Command, ref string Path);
        RunCommandDelegate RunCommand
        { get; set; }

        //string RunCmd(string Cmd)
        //{
        //    if (Cmd.ToLower() == "get datetime")
        //    {
        //        return "Текущее время: " + DateTime.Now.ToString();
        //    }                
        //    return $"Некорректная команда";
        //}
    }
}
