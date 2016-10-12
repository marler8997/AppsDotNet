using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace More.Nfs
{
#if comment
    public class ControlServer
    {
        public void AcceptCallback(ref SelectControl control, Socket listenSock, Buf safeBuffer)
        {
            Socket newSock = listenSock.Accept();

            DebugClientData clientData = new DebugClientData(newSock);
            control.AddReceiveSocket(newSock, clientData.DataCallback);
            clientData.SendPrompt();
        }
        class DebugClientData : IDisposable
        {
            public readonly LineParser lineParser;
            public readonly StreamWriter writer;
            public DebugClientData(Socket socket)
            {
                this.lineParser = new LineParser(Encoding.ASCII, 64, 128);
                this.writer = new StreamWriter(new NetworkStream(socket));
            }
            public void SendPrompt()
            {
                writer.Write("NfsServerDebug> ");
                writer.Flush();
            }
            public void DataCallback(ref SelectControl control, Socket socket, Buf safeBuffer)
            {
                int bytesReceived;
                try
                {
                    bytesReceived = socket.Receive(safeBuffer.array);
                }
                catch (SocketException)
                {
                    bytesReceived = -1;
                }
                if (bytesReceived <= 0)
                {
                    socket.ShutdownSafe();
                    control.DisposeAndRemoveReceiveSocket(socket);
                }

                lineParser.Add(safeBuffer.array, 0, (uint)bytesReceived);

                while (true)
                {
                    String line = lineParser.GetLine();
                    if (line == null) break;


                    if (line[0] == 'd' || line[0] == 'D')
                    {
                        if (NfsServerLog.performanceLog == null)
                        {
                            writer.Write("Cannot dump performance log because it was not enabled");
                        }
                        else
                        {
                            NfsServerLog.performanceLog.DumpLog(writer);
                        }
                    }
                    else if (line[0] == 'h' || line[0] == 'H')
                    {
                        writer.WriteLine("Commands: dump, help");
                    }
                    else if (line[0] == 'e' || line[0] == 'E')
                    {
                        socket.ShutdownSafe();
                        control.DisposeAndRemoveReceiveSocket(socket);
                    }
                    else
                    {
                        writer.WriteLine("UnknownCommand '{0}'", line);
                        writer.WriteLine("Commands: dump, help, exit");
                    }
                    SendPrompt();
                }
            }
            public void Dispose()
            {
                writer.Dispose();
            }
        }

    }
#endif
}
