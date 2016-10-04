using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using More;
using More.Net;

public class CommandLine : CommandLineParser
{
    public readonly CommandLineSwitch listenMode;

    public readonly CommandLineArgument<UInt16> localPort;
    public readonly CommandLineArgumentString localHost;

    public readonly CommandLineArgumentInt32 bufferSizes;
    public readonly CommandLineArgumentInt32 tcpSendWindow;
    public readonly CommandLineArgumentInt32 tcpReceiveWindow;

    public CommandLine()
        : base()
    {
        listenMode = new CommandLineSwitch('l', "listen mode", "Specifies that NetCat will listen for a tcp connection");
        Add(listenMode);

        localPort = new CommandLineArgument<UInt16>(UInt16.Parse, 'p', "the local port to bind to");
        localPort.Default = 0;
        Add(localPort);

        localHost = new CommandLineArgumentString('i', "the local host or ip address to bind to");
        localHost.Default = "0.0.0.0";
        Add(localHost);

        bufferSizes = new CommandLineArgumentInt32('s', "tunnel buffer sizes");
        bufferSizes.Default = 8192;
        Add(bufferSizes);

        tcpSendWindow = new CommandLineArgumentInt32("send-window", "Size of TCP send window (0 means network stack window size)");
        tcpSendWindow.Default = 0;
        Add(tcpSendWindow);
        tcpReceiveWindow = new CommandLineArgumentInt32("recv-window", "Size of TCP recv window (0 means network stack window size)");
        tcpReceiveWindow.Default = 0;
        Add(tcpReceiveWindow);
    }

    public override void PrintUsageHeader()
    {
        Console.WriteLine("Outbound Connection: NetCat.exe [options] <host-connector> <port>");
        Console.WriteLine("InBound Connection : NetCat.exe [options] -l -p <listen-port>");
    }
}
public class NetCat
{
    static Byte[] socketToConsoleBuffer;
    static Byte[] consoleToSocketBuffer;

    static Stream consoleOutputStream;
    static Stream consoleInputStream;
    static void PrepareConsole()
    {
        if (consoleOutputStream == null)
        {
            Console.Out.Flush();
            consoleOutputStream = Console.OpenStandardOutput();
            consoleInputStream = Console.OpenStandardInput();
        }
    }

    static Socket dataSocket;
    static Boolean closed;

    static Int32 Main(string[] args)
    {
        CommandLine commandLine = new CommandLine();
        if (args.Length == 0)
        {
            commandLine.PrintUsage();
            return 0;
        }

        List<String> nonOptionArgs = commandLine.Parse(args);

        socketToConsoleBuffer = new Byte[commandLine.bufferSizes.ArgValue];
        consoleToSocketBuffer = new Byte[commandLine.bufferSizes.ArgValue];

        if (commandLine.listenMode.set)
        {
            IPAddress localAddress = EndPoints.ParseIPOrResolveHost(commandLine.localHost.ArgValue, DnsPriority.IPv4ThenIPv6);

            Socket listenSocket = new Socket(localAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(localAddress, commandLine.localPort.ArgValue));
            listenSocket.Listen(1);

            dataSocket = listenSocket.Accept();
        }
        else if (nonOptionArgs.Count != 2)
        {
            if (nonOptionArgs.Count == 0)
            {
                commandLine.PrintUsage();
                return 0;
            }
            return commandLine.ErrorAndUsage("In client/connect mode there should be 2 non-option command line arguments but got {0}", nonOptionArgs.Count);
        }
        else
        {
            String connectorString = nonOptionArgs[0];

            String portString = nonOptionArgs[1];
            UInt16 port = UInt16.Parse(portString);

            InternetHost host;
            {
                Proxy proxy;
                String ipOrHost = Proxy.StripAndParseProxies(connectorString, DnsPriority.IPv4ThenIPv6, out proxy);
                host = new InternetHost(ipOrHost, port, DnsPriority.IPv4ThenIPv6, proxy);
            }

            dataSocket = new Socket(host.GetAddressFamilyForTcp(), SocketType.Stream, ProtocolType.Tcp);
            if (commandLine.localPort.set)
            {
                dataSocket.Bind(new IPEndPoint(IPAddress.Any, commandLine.localPort.ArgValue));
            }

            BufStruct leftOverData = new BufStruct(socketToConsoleBuffer);
            host.Connect(dataSocket, DnsPriority.IPv4ThenIPv6, ProxyConnectOptions.None, ref leftOverData);
            if (leftOverData.contentLength > 0)
            {
                PrepareConsole();
                consoleOutputStream.Write(leftOverData.buf, 0, (int)leftOverData.contentLength);
            }
        }

        // Note: I'm not sure these options are actually working
        if (commandLine.tcpSendWindow.ArgValue != 0)
        {
            dataSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, commandLine.tcpSendWindow.ArgValue);
        }
        if (commandLine.tcpReceiveWindow.ArgValue != 0)
        {
            dataSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, commandLine.tcpReceiveWindow.ArgValue);
        }

        PrepareConsole();

        Thread consoleReadThread = new Thread(ConsoleReceiveThread);
        consoleReadThread.Name = "Console Read Thread";

        // Don't know how to cancel a console input stream read, so I have to
        // put the console read thread as a background thread so it doesn't keep
        // the program running.
        consoleReadThread.IsBackground = true;

        consoleReadThread.Start();

        try
        {
            while (true)
            {
                Int32 bytesRead = dataSocket.Receive(socketToConsoleBuffer, socketToConsoleBuffer.Length, SocketFlags.None);
                if (bytesRead <= 0)
                {
                    lock (typeof(NetCat))
                    {
                        if (!closed)
                        {

                            closed = true;
                        }
                    }
                    break;
                }
                consoleOutputStream.Write(socketToConsoleBuffer, 0, bytesRead);
            }
        }
        catch (SocketException)
        {
        }
        finally
        {
            lock (typeof(NetCat))
            {
                if (!closed)
                {
                    consoleInputStream.Dispose();
                    closed = true;
                }
            }
            // Do not join other thread, just exit
        }

        return 0;
    }
    static void ConsoleReceiveThread()
    {
        try
        {
            while (true)
            {
                Int32 bytesRead = consoleInputStream.Read(consoleToSocketBuffer, 0, consoleToSocketBuffer.Length);
                if (bytesRead <= 0)
                {
                    break;
                }
                dataSocket.Send(consoleToSocketBuffer, 0, bytesRead, SocketFlags.None);
            }
        }
        finally
        {
            lock (typeof(NetCat))
            {
                if (!closed)
                {
                    dataSocket.ShutdownAndDispose();
                    closed = true;
                }
            }
        }
    }
}
