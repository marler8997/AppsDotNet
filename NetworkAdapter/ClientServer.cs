using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

using More;
using More.Net;

public class ClientServer
{
    static InternetHost server; // can't be readonly because it's a struct with non-readonly fields

    static StreamWriter dataLogStream;
    static UInt32 snaplen;

    public static Int32 Run(CommandLine commandLine, List<String> nonOptionArgs)
    {
        snaplen = commandLine.dataSnaplen.ArgValue;

        //
        // Options
        //
        if (nonOptionArgs.Count < 2) return commandLine.ErrorAndUsage("Not enough arguments");

        String clientSideConnector = nonOptionArgs[0];
        String listenPortsString = nonOptionArgs[1];

        {
            Proxy proxy;
            String ipOrHostAndPort = Proxy.StripAndParseProxies(clientSideConnector, DnsPriority.IPv4ThenIPv6, out proxy);
            UInt16 port;
            String ipOrHost = EndPoints.SplitIPOrHostAndPort(ipOrHostAndPort, out port);
            server = new InternetHost(ipOrHost, port, DnsPriority.IPv4ThenIPv6, proxy);
        }
        //SortedNumberSet listenPortSet = PortSet.ParsePortSet(listenPortsString);
        IEnumerable<UInt16> listenPortSet;
        {
            listenPortSet = new UInt16[] {UInt16.Parse(listenPortsString)};
        }

        var selectServer = new SelectServerSharedBuffer(false, new byte[commandLine.readBufferSize.ArgValue]);
        IPAddress listenIP = IPAddress.Any;

        foreach (var port in listenPortSet)
        {
            IPEndPoint listenEndPoint = new IPEndPoint(listenIP, port);
            Socket socket = new Socket(listenEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(listenEndPoint);
            socket.Listen((int)commandLine.socketBackLog.ArgValue);
            selectServer.AddListenSocket(socket, AcceptCallback);
        }
        if (commandLine.dataLogFile.set)
        {
            dataLogStream = new StreamWriter(new FileStream(commandLine.dataLogFile.ArgValue, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        }

        selectServer.Run();
        return 0;
    }

    static void AcceptCallback(SelectServerSharedBuffer selectServer, Socket listenSock)
    {
        Socket newSock = listenSock.Accept();
        if (dataLogStream != null)
        {
            dataLogStream.WriteLine("NetworkAdapter Connection: Accepted new client {0}", newSock.SafeRemoteEndPointString());
        }

        Socket clientSideSocket = new Socket(server.GetAddressFamilyForTcp(), SocketType.Stream, ProtocolType.Tcp);

        BufStruct leftOver = new BufStruct(selectServer.sharedBuffer);
        clientSideSocket.Connect(server, DnsPriority.IPv4ThenIPv6, ProxyConnectOptions.None, ref leftOver);
        if (leftOver.contentLength > 0)
        {
            newSock.Send(leftOver.buf, 0, (int)leftOver.contentLength, SocketFlags.None);
            if (dataLogStream != null)
            {
                if (snaplen == 0)
                {
                    dataLogStream.WriteLine("NetworkAdapter Data: {0} < {1} ({2} bytes)", newSock.SafeRemoteEndPointString(),
                        clientSideSocket.SafeRemoteEndPointString(), leftOver.contentLength);
                    dataLogStream.Flush();
                    dataLogStream.BaseStream.Write(leftOver.buf, 0, (int)leftOver.contentLength);
                    dataLogStream.Flush();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        SelectSocketTunnel tunnel = new SelectSocketTunnel(newSock, clientSideSocket);
        selectServer.AddReceiveSocket(newSock, tunnel.ReceiveCallback);
        selectServer.AddReceiveSocket(clientSideSocket, tunnel.ReceiveCallback);
    }
    public class SelectSocketTunnel
    {
        public readonly Socket a;
        public readonly Socket b;
        public SelectSocketTunnel(Socket a, Socket b)
        {
            this.a = a;
            this.b = b;
        }
        public void ReceiveCallback(SelectServerSharedBuffer control, Socket sock)
        {
            Socket other = (sock == a) ? b : a;

            int bytesReceived;
            try
            {
                bytesReceived = sock.Receive(control.sharedBuffer);
            }
            catch (SocketException)
            {
                bytesReceived = -1;
            }

            if (bytesReceived <= 0)
            {
                other.ShutdownSafe();
                control.DisposeAndRemoveReceiveSocket(sock);
            }
            else
            {
                try
                {
                    other.Send(control.sharedBuffer, bytesReceived, SocketFlags.None);
                    if (dataLogStream != null)
                    {
                        string directionChar = (sock == a) ? ">" : "<";
                        if (snaplen == 0)
                        {
                            dataLogStream.WriteLine("NetworkAdapter Data: {0} {1} {2} ({3} bytes)", a.SafeRemoteEndPointString(),
                                directionChar, b.SafeRemoteEndPointString(), bytesReceived);
                            dataLogStream.Flush();
                            dataLogStream.BaseStream.Write(control.sharedBuffer, 0, bytesReceived);
                            dataLogStream.Flush();
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }
                catch (SocketException)
                {
                    sock.ShutdownSafe();
                    control.DisposeAndRemoveReceiveSocket(sock);
                }
            }
        }
    }
}