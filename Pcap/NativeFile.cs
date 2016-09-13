using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.IO;
using System.Security;

using More;


static class WindowsNativeMethods
{
    [DllImport("kernel32")]
    public static extern UInt32 GetLastError();

    [DllImport("kernel32", SetLastError = true)]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [SuppressUnmanagedCodeSecurity]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateFile(
         [MarshalAs(UnmanagedType.LPTStr)] String filename,
         [MarshalAs(UnmanagedType.U4)] FileAccess access,
         [MarshalAs(UnmanagedType.U4)] FileShare share,
         IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
         [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
         [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
         IntPtr templateFile);

    /*
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WriteFile(IntPtr hFile, byte[] buffer,
       UInt32 nNumberOfBytesToWrite, out UInt32 lpNumberOfBytesWritten,
       [In] ref System.Threading.NativeOverlapped lpOverlapped);*/
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WriteFile(IntPtr hFile, byte[] buffer,
       UInt32 nNumberOfBytesToWrite, out UInt32 lpNumberOfBytesWritten,
       [In] IntPtr lpOverlapped);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe extern bool WriteFile(IntPtr hFile, byte* buffer,
       UInt32 nNumberOfBytesToWrite, out UInt32 lpNumberOfBytesWritten,
       [In] IntPtr lpOverlapped);
}


public static class NativeFile
{
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
    public static IntPtr TryOpen(String filename, FileMode mode, FileAccess access, FileShare share)
    {
        return WindowsNativeMethods.CreateFile(filename, access, share,
            IntPtr.Zero, mode, FileAttributes.Normal, IntPtr.Zero);
    }
    public static IntPtr Open(String filename, FileMode mode, FileAccess access, FileShare share)
    {
        var fileHandle = WindowsNativeMethods.CreateFile(filename, access, share,
            IntPtr.Zero, mode, FileAttributes.Normal, IntPtr.Zero);
        if (fileHandle == INVALID_HANDLE_VALUE)
        {
            throw new Exception(String.Format("CreateFile '{0}' (mode={1}, access={2}, share={3}) failed (error={4})",
                filename, mode, access, share, WindowsNativeMethods.GetLastError()));
        }
        return fileHandle;
    } 
}


/*
public class NativeFile : SafeHandle
{
    static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    public IntPtr fileHandle;
    public NativeFile(String filename, FileMode mode, FileAccess access, FileShare share)
    {
        this.fileHandle = WindowsNativeMethods.CreateFile(filename, access, share,
            IntPtr.Zero, mode, FileAttributes.Normal, IntPtr.Zero);
        if (this.fileHandle == INVALID_HANDLE_VALUE)
        {
            throw new Exception(String.Format("CreateFile '{0}' (mode={1}, access={2}, share={3}) failed (error={4})",
                filename, mode, access, share, WindowsNativeMethods.GetLastError()));
        }
    }
    public void Dispose()
    {
        if (fileHandle != INVALID_HANDLE_VALUE)
        {
            WindowsNativeMethods.CloseHandle(fileHandle);
            fileHandle = INVALID_HANDLE_VALUE;
        }
    }
}
*/


public interface ISink
{
    void Flush();

    void PutZeros(UInt32 length);

    void Put(Byte value);
    void Put(SByte value);
    void Put(UInt16 value);
    void Put(Int16 value);
    void Put(UInt32 value);
    void Put(Int32 value);
    void Put(Byte[] data, UInt32 offset, UInt32 length);
    void Put(String s);
}

public static class NativeEndian
{
    public unsafe static void Write16Bit(Byte* buffer, UInt16 value)
    {
        var valuePtr = (byte*)&value;
        buffer[0] = valuePtr[0];
        buffer[1] = valuePtr[1];
    }
    public unsafe static void Write32Bit(Byte* buffer, UInt32 value)
    {
        var valuePtr = (byte*)&value;
        buffer[0] = valuePtr[0];
        buffer[1] = valuePtr[1];
        buffer[2] = valuePtr[2];
        buffer[3] = valuePtr[3];
    }
}


public unsafe class NativeEndianFileSink : ISink
{
    IntPtr fileHandle;
    byte[] buffer;
    uint contentLength;
    public NativeEndianFileSink(IntPtr fileHandle, byte[] buffer)
    {
        this.fileHandle = fileHandle;
        this.buffer = buffer;
    }

    public void Flush()
    {
        if (contentLength > 0)
        {
            UInt32 written;
            if (false == WindowsNativeMethods.WriteFile(fileHandle, buffer, contentLength, out written, IntPtr.Zero))
            {
                throw new IOException(String.Format("WriteFile({0} bytes) failed (error={1})", contentLength, WindowsNativeMethods.GetLastError()));
            }
            if (written != contentLength)
            {
                throw new IOException(String.Format("Only wrote {0} out of {1}", written, contentLength));
            }
            contentLength = 0;
        }
    }

    // TODO: tweek this value to maximize performance
    //       Compare the cost of a flush to the cost of copying the data to
    //       the buffer and flushing the buffer later.
    //       if you buffer
    //          copy data to buffer
    //          (later you will flush)
    //       if you don't buffer
    //          
    //          write data to file
    //          (later you will call flush)
    const UInt32 SmallEnoughToBuffer = 8;
    unsafe void Put(Byte* data, UInt32 length)
    {
        if(length <= SmallEnoughToBuffer)
        {
            if(contentLength + length > buffer.Length)
            {
                Flush();
            }
            // TODO: call a native function to perform the copy
            for(uint i = 0; i < length; i++)
            {
                buffer[contentLength + i] = data[i];
            }
            contentLength += length;
        }
        else
        {
            Flush();
            UInt32 written;
            if (false == WindowsNativeMethods.WriteFile(fileHandle, data, length, out written, IntPtr.Zero))
            {
                throw new IOException(String.Format("WriteFile({0} bytes) failed (error={1})", length, WindowsNativeMethods.GetLastError()));
            }
            if (written != length)
            {
                throw new IOException(String.Format("Only wrote {0} out of {1}", written, length));
            }
        }
    }

    public void PutZeros(UInt32 length)
    {
        if (contentLength + length <= buffer.Length)
        {
            Array.Clear(buffer, (int)contentLength, (int)length);
            contentLength += length;
        }
        else
        {
            throw new NotImplementedException();
            //Flush();
        }
    }
    public void Put(byte value)
    {
        Put((byte*)&value, 1);
    }
    public void Put(sbyte value)
    {
        Put((byte*)&value, 1);
    }
    public void Put(ushort value)
    {
        Put((byte*)&value, 2);
    }
    public void Put(short value)
    {
        Put((byte*)&value, 2);
    }
    public void Put(uint value)
    {
        Put((byte*)&value, 4);
    }
    public void Put(int value)
    {
        Put((byte*)&value, 4);
    }
    public unsafe void Put(Byte[] data, UInt32 offset, UInt32 length)
    {
        fixed (byte* ptr = data)
        {
            Put(ptr + offset, length);
        }
    }
    public void Put(string s)
    {
        throw new NotImplementedException();
    }
}