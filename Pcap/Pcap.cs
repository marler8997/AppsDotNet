using System;
using System.IO;
using System.Net;
using System.Text;

using More;

namespace More.Net
{
    // Types found at http://www.tcpdump.org/linktypes.html
    public enum PcapDataLinkType : uint
    {
        Null = 0,
        Ethernet = 1,
        AX25 = 3,
        // More can be added from http://www.tcpdump.org/linktypes.html
    }

    public class PcapGlobalHeader : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "MagicNumber"),
            new BigEndianUInt16Reflector(typeof(PcapGlobalHeader), "CurrentVersionMajor"),
            new BigEndianUInt16Reflector(typeof(PcapGlobalHeader), "CurrentVersionMinor"),
            new BigEndianInt32Reflector (typeof(PcapGlobalHeader), "gmtToLocalCorrection"),
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "timestampSigFigs"),
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "maxPacketLength"),
            new BigEndianUnsignedEnumReflector<PcapDataLinkType>(typeof(PcapGlobalHeader), "dataLinkType", 4),
        });

        public const UInt32 MagicNumber = 0xa1b2c3d4;
        public const UInt16 CurrentVersionMajor = 2;
        public const UInt16 CurrentVersionMinor = 4;

        public Int32 gmtToLocalCorrection;
        public UInt32 timestampSigFigs;
        public UInt32 maxPacketLength;
        public PcapDataLinkType dataLinkType;

        public PcapGlobalHeader(Int32 gmtToLocalCorrection, UInt32 timestampSigFigs,
            UInt32 maxPacketLength, PcapDataLinkType dataLinkType)
            : base(memberSerializers)
        {
            this.gmtToLocalCorrection = gmtToLocalCorrection;
            this.timestampSigFigs = timestampSigFigs;
            this.maxPacketLength = maxPacketLength;
            this.timestampSigFigs = timestampSigFigs;
        }
    }

    public class PcapPacket : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "timestampSeconds"),
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "timestampMicroseconds"),
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "captureLength"),
            new BigEndianUInt32Reflector(typeof(PcapGlobalHeader), "actualLength"),
        });

        public UInt32 timestampSeconds;
        public UInt32 timestampMicroseconds;
        public UInt32 captureLength;
        public UInt32 actualLength;

        public PcapPacket(UInt32 timestampSeconds, UInt32 timestampMicroseconds,
            UInt32 captureLength, UInt32 actualLength)
            : base(memberSerializers)
        {
            this.timestampSeconds = timestampSeconds;
            this.timestampMicroseconds = timestampMicroseconds;
            this.captureLength = captureLength;
            this.actualLength = actualLength;
        }
    }


    public static class Pcapng
    {
        public const UInt32 SectionHeaderBlockType        = 0x0A0D0D0A; // why? palindromic and since it is newline codes, if the file
                                                                        // is transferred and changes newlines, this value will change
                                                                        // and the change will be detected

        public const UInt32 SectionHeaderByteOrderMagic = 0x1A2B3C4D;
        public const UInt16 SectionHeaderDefaultMajorVersion = 1;
        public const UInt16 SectionHeaderDefaultMinorVersion = 0;

        // TODO: include name resolution block


        public const UInt32 InterfaceDescriptionBlockType = 0x00000001;
        public const UInt16 LINKTYPE_NULL = 0;


        public const UInt32 EnhancedPacketBlockType = 0x00000006;
    }

    public abstract class TcpConnectionLogger
    {
    }
    public class Ipv4TcpConnectionLogger
    {
        public UInt32 ipOfA, ipOfB;
        public UInt16 portOfA, portOfB;
        public UInt32 seqForA, ackForA;
        public UInt32 seqForB, ackForB;
    }

    public class PcapLogger
    {
        readonly ISink sink;
        public readonly UInt32 snaplen;
        public PcapLogger(ISink sink, UInt32 snaplen)
        {
            this.sink = sink;
            this.snaplen = snaplen;
        }

        const UInt32 SectionHeaderMinBlockLength =
            4 + // Block Type
            4 + // Block Total Length
            4 + // Byte-Order Magic
            4 + // Major and Minor version
            8 + // Section Length
            0 + // Options...
            4 ; // Block Total Length (again)

        const UInt32 InterfaceDescriptionMinBlockLength =
            4 + // Block Type
            4 + // Block Total Length
            2 + // Link Type
            2 + // Reserved
            4 + // Snaplen
            0 + // Options...
            4 ; // Block Total Length (again)

        const UInt32 EnhancedPacketMinBlockLength =
            4 + // Block Type
            4 + // Block Total Length
            4 + // Interface ID
            8 + // Timestamp (High and Low)
            4 + // Captured Packet Length
            4 + // Original Packet Length
            0 + // Packet data (padded to 32 bits)
            0 + // Options...
            4 ; // Block Total Length (again)

        public void WriteFileHeaders()
        {
            //
            // Write Section Header Block
            //
            {
                UInt32 sectionHeaderBlockLength = SectionHeaderMinBlockLength;

                // TODO: add option lengths to sectionHeaderBlockLength

                sink.Put(Pcapng.SectionHeaderBlockType);
                sink.Put(sectionHeaderBlockLength);
                sink.Put(Pcapng.SectionHeaderByteOrderMagic);
                sink.Put(Pcapng.SectionHeaderDefaultMajorVersion);
                sink.Put(Pcapng.SectionHeaderDefaultMinorVersion);

                sink.Put(UInt32.MaxValue); // section length unspecified (-1)
                sink.Put(UInt32.MaxValue);

                sink.Put(sectionHeaderBlockLength);
            }

            //
            // Write Interface Description Block
            //
            {
                UInt32 interfaceDescriptionBlockLength = InterfaceDescriptionMinBlockLength;

                // TODO: add option lengths to interfaceDescriptionBlockLength

                sink.Put(Pcapng.InterfaceDescriptionBlockType);
                sink.Put(interfaceDescriptionBlockLength); // NOT IMPLEMENTED
                sink.Put(Pcapng.LINKTYPE_NULL); // LINKTYPE_NULL
                sink.Put((UInt16)0); // Reserved
                sink.Put(snaplen);
                sink.Put(interfaceDescriptionBlockLength);
            }

            sink.Flush();
        }

        public static UInt32 Padded32Bit(UInt32 value)
        {
            return ((value & 0x3) == 0) ? value : ((value >> 2) + 1) << 2;
        }




        unsafe void PopulateTcpHeader(byte* data, IPEndPoint src, IPEndPoint dst, UInt32 seq, UInt32 ack)
        {
            NativeEndian.Write16Bit(data    , (UInt16)src.Port);
            NativeEndian.Write16Bit(data + 2, (UInt16)dst.Port);
            NativeEndian.Write32Bit(data + 4, seq);
            NativeEndian.Write32Bit(data + 8, ack);
        }


        public void LogTcpData(IPEndPoint src, IPEndPoint dst, Byte[] data, UInt32 offset, UInt32 length)
        {
            UInt32 enhancedPacketBlockLength = EnhancedPacketMinBlockLength;

            UInt32 paddedPacketLength = Padded32Bit(length);
            enhancedPacketBlockLength += paddedPacketLength;

            sink.Put(Pcapng.EnhancedPacketBlockType);
            sink.Put(enhancedPacketBlockLength);
            sink.Put(0U); // Interface ID
            sink.Put(0U); // Timestamp (high)
            sink.Put(0U); // Timestamp (low)
            sink.Put(length); // Captured length
            sink.Put(length); // Original length
            sink.Put(data, offset, length);
            if (paddedPacketLength > length)
            {
                sink.PutZeros(paddedPacketLength - length);
            }
            sink.Put(enhancedPacketBlockLength);
            sink.Flush();
            Console.WriteLine("EPB length {0}", enhancedPacketBlockLength);
            Console.WriteLine("Logged a packet of length {0}", length);
        }
    }

}