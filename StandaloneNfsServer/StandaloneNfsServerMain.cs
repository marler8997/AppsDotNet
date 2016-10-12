using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using More;
using More.Nfs;

namespace StandaloneNfsServer
{
    public class CommandLineOptions : CommandLineParser
    {
        public CommandLineArgument<IPAddress> listenIPAddress;
        public CommandLineArgument<UInt16> debugListenPort;
        //public CommandLineArgument<UInt16> npcListenPort;
        public CommandLineArgumentString logFile;

        public CommandLineArgumentEnum<LogLevel> logLevel;
        public CommandLineArgumentString performanceLog;
        public CommandLineArgumentUInt32 receiveBufferSize;

#if WindowsCE
        public CLSwitch jediTimer;
#endif

        public CommandLineOptions()
        {
            listenIPAddress = new CommandLineArgument<IPAddress>(IPAddress.Parse, 'l', "Listen IP Address");
            listenIPAddress.Default = IPAddress.Any;
            Add(listenIPAddress);

            debugListenPort = new CommandLineArgument<UInt16>(UInt16.Parse, 'd', "DebugListenPort", "The TCP port that the debug server will be listening to (If no port is specified, the debug server will not be running)");
            Add(debugListenPort);

            //npcListenPort = new CommandLineArgument<UInt16>(UInt16.Parse, 'n', "NpcListenPort", "The TCP port that the NPC server will be listening to (If no port is specified, the NPC server will not be running)");
            // Add(npcListenPort);

            logFile = new CommandLineArgumentString('f', "LogFile", "Log file (logs to stdout if not specified)");
            Add(logFile);

            logLevel = new CommandLineArgumentEnum<LogLevel>('v', "LogLevel", "Level of statements to log");
            logLevel.SetDefault(LogLevel.None);
            Add(logLevel);

            performanceLog = new CommandLineArgumentString('p', "PerformanceLog", "Where to log performance ('internal',<filename>)");
            Add(performanceLog);

            receiveBufferSize = new CommandLineArgumentUInt32("receive-buffer-size", "Size of the buffer to create to receive data");
            receiveBufferSize.Default = 8192;
            Add(receiveBufferSize);

#if WindowsCE
            jediTimer = new CLSwitch('j', "JediTimer", "Adds the jedi timer timestamp to printed commands");
            Add(jediTimer);
#endif
        }
        public override void PrintUsageHeader()
        {
            Console.WriteLine("Usage: NfsServer.exe [options] (<local-share-path1> <remote-share-name1>)...");
        }
    }

    class Program
    {
        static void Main(String[] args)
        {
            //NfsServerLog.stopwatchTicksBase = Stopwatch.GetTimestamp();

            CommandLineOptions options = new CommandLineOptions();
            List<String> nonOptionArguments = options.Parse(args);

            if (nonOptionArguments.Count < 2)
            {
                options.ErrorAndUsage("Expected at least 2 non-option arguments but got '{0}'", nonOptionArguments.Count);
                return;
            }
            if (nonOptionArguments.Count % 2 == 1)
            {
                options.ErrorAndUsage("Expected an even number of non-option arguments but got {0}", nonOptionArguments.Count);
            }

            RootShareDirectory[] rootShareDirectories = new RootShareDirectory[nonOptionArguments.Count / 2];
            for (int i = 0; i < rootShareDirectories.Length; i++)
            {
                String localSharePath = nonOptionArguments[2 * i];
                String remoteShareName = nonOptionArguments[2 * i + 1];
                rootShareDirectories[i] = new RootShareDirectory(localSharePath, remoteShareName);
            }

            //
            // Options not exposed via command line yet
            //
            Int32 mountListenPort = 59733;
            Int32 backlog = 4;

            UInt32 readSizeMax = 65536;
            UInt32 suggestedReadSizeMultiple = 4096;

            //
            // Listen IP Address
            //
            IPAddress listenIPAddress = options.listenIPAddress.ArgValue;

            //
            // Debug Server
            //
            IPEndPoint debugServerEndPoint = !options.debugListenPort.set ? null :
                new IPEndPoint(listenIPAddress, options.debugListenPort.ArgValue);

            /*
            //
            // Npc Server
            //
            IPEndPoint npcServerEndPoint = !options.npcListenPort.set ? null :
                new IPEndPoint(listenIPAddress, options.npcListenPort.ArgValue);
            */

            //
            // Logging Options
            //                
#if WindowsCE
            JediTimer.printJediTimerPrefix = options.jediTimer.set;
#endif
            if (options.performanceLog.set)
            {
                throw new NotImplementedException();
                /*
                if (options.performanceLog.ArgValue.Equals("internal", StringComparison.CurrentCultureIgnoreCase))
                {
                    NfsServerLog.performanceLog = new InternalPerformanceLog();
                    if (!options.debugListenPort.set)
                    {
                        options.ErrorAndUsage("Invalid option combination: you cannot set '-i internal' unless you also set -d <port>");
                        return;
                    }
                }
                else
                {
                    try
                    {
                        FileStream fileStream = new FileStream(options.performanceLog.ArgValue, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                        NfsServerLog.performanceLog = new WriterPerformanceLog(fileStream);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to create performance log file '{0}'", options.performanceLog.ArgValue);
                        throw e;
                    }
                }
                 */
            }

            TextWriter selectServerEventsLog = null;
            if (options.logLevel.ArgValue != LogLevel.None)
            {
                throw new NotImplementedException();
                /*
                TextWriter logWriter;
                if (options.logFile.set)
                {
                    logWriter = new StreamWriter(new FileStream(options.logFile.ArgValue, FileMode.Create, FileAccess.Write, FileShare.Read));
                }
                else
                {
                    logWriter = Console.Out;
                }

                NfsServerLog.sharedFileSystemLogger = (options.logLevel.ArgValue >= LogLevel.Info) ? logWriter : null;
                NfsServerLog.rpcCallLogger = (options.logLevel.ArgValue >= LogLevel.Info) ? logWriter : null;
                NfsServerLog.warningLogger = (options.logLevel.ArgValue >= LogLevel.Warning) ? logWriter : null;
                NfsServerLog.npcEventsLogger = (options.logLevel.ArgValue >= LogLevel.Info) ? logWriter : null;

                RpcPerformanceLog.rpcMessageSerializationLogger = (options.logLevel.ArgValue >= LogLevel.Info) ? logWriter : null;

                selectServerEventsLog = (options.logLevel.ArgValue >= LogLevel.All) ? logWriter : null;
                 */
            }

            //
            // Permissions
            //
            ModeFlags defaultDirectoryPermissions =
                ModeFlags.OtherExecute | ModeFlags.OtherWrite | ModeFlags.OtherRead |
                ModeFlags.GroupExecute | ModeFlags.GroupWrite | ModeFlags.GroupRead |
                ModeFlags.OwnerExecute | ModeFlags.OwnerWrite | ModeFlags.OwnerRead;
            /*ModeFlags.SaveSwappedText | ModeFlags.SetUidOnExec | ModeFlags.SetGidOnExec;*/
            ModeFlags defaultFilePermissions =
                ModeFlags.OtherExecute | ModeFlags.OtherWrite | ModeFlags.OtherRead |
                ModeFlags.GroupExecute | ModeFlags.GroupWrite | ModeFlags.GroupRead |
                ModeFlags.OwnerExecute | ModeFlags.OwnerWrite | ModeFlags.OwnerRead;
            /*ModeFlags.SaveSwappedText | ModeFlags.SetUidOnExec | ModeFlags.SetGidOnExec;*/
            IPermissions permissions = new ConstantPermissions(defaultDirectoryPermissions, defaultFilePermissions);


            IFileIDsAndHandlesDictionary fileIDDictionary = new FreeStackFileIDDictionary(512, 512, 4096, 1024);

            SharedFileSystem sharedFileSystem = new SharedFileSystem(fileIDDictionary, permissions, rootShareDirectories);



            /*
            new RpcServicesManager().Run(
                selectServerEventsLog,
                debugServerEndPoint,
                npcServerEndPoint,
                listenIPAddress,
                backlog, sharedFileSystem,
                Ports.PortMap, mountListenPort, Ports.Nfs,
                readSizeMax, suggestedReadSizeMultiple);\
             */
        }
    }
}
