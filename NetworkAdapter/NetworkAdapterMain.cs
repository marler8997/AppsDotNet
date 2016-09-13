using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

using More;

public enum AdapterType { ClientServer, ClientClient, ServerServer };
public static class AdapterTypeMethods
{
    public static AdapterType ParseAdapterType(String adapterType)
    {
        if (adapterType.Equals("cs", StringComparison.CurrentCultureIgnoreCase) ||
            adapterType.Equals("client-server", StringComparison.CurrentCultureIgnoreCase))
        {
            return AdapterType.ClientServer;
        }
        if (adapterType.Equals("cc", StringComparison.CurrentCultureIgnoreCase) ||
            adapterType.Equals("client-client", StringComparison.CurrentCultureIgnoreCase))
        {
            return AdapterType.ClientClient;
        }
        if (adapterType.Equals("ss", StringComparison.CurrentCultureIgnoreCase) ||
            adapterType.Equals("server-server", StringComparison.CurrentCultureIgnoreCase))
        {
            return AdapterType.ServerServer;
        }
        throw new FormatException(String.Format("Unrecognized Adapter Type '{0}', expected 'client-server', 'client-client' or 'server-server'", adapterType));
    }
}
public enum ClientConnectWaitMode { Immediate, OtherEndToConnect, OtherEndReceiveConnectRequest };
public static class ClientConnectWaitModeMethods
{
    public static ClientConnectWaitMode Parse(String waitMode)
    {
        if (waitMode.Equals("i", StringComparison.CurrentCultureIgnoreCase) ||
            waitMode.Equals("immediate", StringComparison.CurrentCultureIgnoreCase))
        {
            return ClientConnectWaitMode.Immediate;
        }
        if (waitMode.Equals("c", StringComparison.CurrentCultureIgnoreCase) ||
            waitMode.Equals("connect", StringComparison.CurrentCultureIgnoreCase))
        {
            return ClientConnectWaitMode.OtherEndToConnect;
        }
        if (waitMode.Equals("w", StringComparison.CurrentCultureIgnoreCase) ||
            waitMode.Equals("wait", StringComparison.CurrentCultureIgnoreCase))
        {
            return ClientConnectWaitMode.OtherEndReceiveConnectRequest;
        }
        throw new FormatException(String.Format("Unrecognized ClientConnectWaitMode '{0}', expected 'immediate', 'connect' or 'wait'", waitMode));
    }
}
public class CommandLine : CommandLineParser
{
    public readonly CommandLineArgumentString dataLogFile;
    public readonly CommandLineArgumentUInt32 dataSnaplen;

    public readonly CommandLineArgumentString specialPortList;
    public readonly CommandLineArgumentUInt32 readBufferSize;
    public readonly CommandLineArgumentUInt32 socketBackLog;
    public readonly CommandLineSwitch logData;
    public readonly CommandLineSwitch noTransferMessages;

    private Boolean adapterTypeModeIsSet;
    private AdapterType adapterTypeMode;

    public CommandLine()
        : base()
    {
        dataLogFile = new CommandLineArgumentString("data-log", "Data Log File");
        Add(dataLogFile);

        dataSnaplen = new CommandLineArgumentUInt32("snaplen", "data snap length (0 means no snaplen)");
        dataSnaplen.Default = 0;
        Add(dataSnaplen);

        specialPortList = new CommandLineArgumentString('c', "Connect Request Port List", "Special list of ports that when connected, must establish a connection request. A connection request must be established with incoming connections if the connection is to another instance of NetworkAdapter where it's opposite end is a client that is waiting for a connection request.");
        Add(specialPortList);

        readBufferSize = new CommandLineArgumentUInt32('b', "Read Buffer Size", "The size of the buffer used to hold the bytes being read from a socket");
        readBufferSize.Default = 4096;
        Add(readBufferSize);

        socketBackLog = new CommandLineArgumentUInt32('s', "Server Socket BackLog", "The maximum length of the pending connections queue");
        socketBackLog.Default = 32;
        Add(socketBackLog);

        logData = new CommandLineSwitch('d', "log-data", "Log the socket data");
        Add(logData);

        noTransferMessages = new CommandLineSwitch('t', "no-transfer-messages", "Do not log transfer messages");
        Add(noTransferMessages);

        this.adapterTypeModeIsSet = false;
    }

    public void SetAdapterTypeMode(AdapterType adapterType)
    {
        this.adapterTypeModeIsSet = true;
        this.adapterTypeMode = adapterType;
    }

    public override void PrintUsageHeader()
    {
        if (!adapterTypeModeIsSet)
        {
            Console.WriteLine("NetworkAdapter <adapter-type> [options]");
            Console.WriteLine();
        }
        if (!adapterTypeModeIsSet || (adapterTypeMode == AdapterType.ClientServer))
        {
            Console.WriteLine("NetworkAdapter cs|client-server [options] <server-connector> <listen-port-list>");
        }
        if (!adapterTypeModeIsSet || (adapterTypeMode == AdapterType.ClientClient))
        {
            Console.WriteLine("NetworkAdapter cc|client-client [options] <not-implemented-yet>");
        }

        if (!adapterTypeModeIsSet || (adapterTypeMode == AdapterType.ServerServer))
        {
            Console.WriteLine();
            Console.WriteLine("NetworkAdapter ss|server-server [options] <tunnel-list>");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Example 1: NetworkAdapter ss 80 80-81 80,81,82-90");
            Console.WriteLine("This means listen on ports 80, 81, 82 and 90");
            Console.WriteLine("Tunnel '80'          : if 2 clients come in on port 80, connect them");
            Console.WriteLine("Tunnel '80-81'       : if a client comes in on port 80 and another on port 81, connect them");
            Console.WriteLine("Tunnel '80,81,82-90' : if a client comes in on either port 80, 81, or 82, and another on port 90, connect them");
            Console.WriteLine();
            Console.WriteLine("NetworkAdapter server-server 80 81$");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("Note: I need to provide a way to specify that a client should wait for a connect request from the other end. The conditions for this are that the other adapter are:");
        Console.WriteLine("   1. Has a client on the other end");
        Console.WriteLine("   2. When it was run, they specified that the client should wait for a connection request");
        Console.WriteLine();
        Console.WriteLine("Type Grammar:");
        Console.WriteLine("   <tunnel-list>  = <tunnel> | <tunnel> <tunnel-list>");
        Console.WriteLine("   <tunnel>       = <port-list> | <port-list> '-' <port-list>");
        Console.WriteLine("   <port-list>    = <port> | <port> ',' <port-list>");
        Console.WriteLine("   <port>         = 1-65535");
        Console.WriteLine();
    }
}
public class NetworkAdapterProgram
{
    static Int32 Main(string[] args)
    {
        CommandLine commandLine = new CommandLine();
        List<String> nonOptionArgs = commandLine.Parse(args);

        if (nonOptionArgs.Count <= 0)
        {
            return commandLine.ErrorAndUsage("Please specify the adapter type 'client-server' ('cs'), 'client-client' ('cc') or 'server-server' ('ss')");
        }

        AdapterType adapterType = AdapterTypeMethods.ParseAdapterType(nonOptionArgs[0]);
        nonOptionArgs.RemoveAt(0);
        commandLine.SetAdapterTypeMode(adapterType);

        if (adapterType == AdapterType.ClientServer)
        {
            return ClientServer.Run(commandLine, nonOptionArgs);
        }
        
        if (adapterType == AdapterType.ClientClient)
        {
            throw new NotImplementedException();
        }
        
        if (adapterType == AdapterType.ServerServer)
        {
            throw new NotImplementedException();
        }

        throw new FormatException(String.Format("Invalid Enum '{0}' ({1})", adapterType, (Int32)adapterType));
    }


}
