using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace More
{
    public static class DateTimeExtensions
    {
        public static readonly DateTime UnixZeroTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static UInt32 ToUnixTime(this DateTime dateTime)
        {
            return (UInt32)(dateTime - UnixZeroTime).TotalSeconds;
        }
    }
    public static class PlatformPath
    {
        public static Boolean IsValidUnixFileName(String fileName)
        {
            return !fileName.Contains("/");
        }
        public static String LocalCombine(String parent, String child)
        {
            StringBuilder builder = new StringBuilder(parent.Length + 1 + child.Length);
            builder.Append(parent);
            builder.Append(Path.DirectorySeparatorChar);
            builder.Append(child);
            return builder.ToString();
        }
        public static String LocalPathDiff(String localParentDirectory, String localPathAndFileName)
        {
            if (!localPathAndFileName.StartsWith(localParentDirectory))
                throw new InvalidOperationException(String.Format("You attempted to take the local path diff of '{0}' and '{1}' but the second path does not start with the first path",
                    localParentDirectory, localPathAndFileName));

            Int32 index;
            for (index = localParentDirectory.Length; true; index++)
            {
                if (index >= localPathAndFileName.Length)
                    throw new InvalidOperationException(String.Format("The local path diff of '{0}' and '{1}' is empty",
                        localParentDirectory, localPathAndFileName));
                if (localPathAndFileName[index] != Path.DirectorySeparatorChar) break;
            }

            return localPathAndFileName.Substring(index);
        }
        public static String LocalToUnixPath(String localPath)
        {
            if (Path.DirectorySeparatorChar == '/') return localPath;
            return localPath.Replace(Path.DirectorySeparatorChar, '/');
        }
        public static String UnixToLocalPath(String unixPath)
        {
            if (Path.DirectorySeparatorChar == '/') return unixPath;
            return unixPath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}

namespace More.Nfs.Nfs3Procedure
{
    public enum Status
    {
        Ok = 0,
        ErrorPermission = 1,
        ErrorNoSuchFileOrDirectory = 2,
        ErrorIOHard = 5,
        ErrorIONoSuchDeviceOrAddress = 6,
        ErrorAccess = 13,
        ErrorAlreadyExists = 17,
        ErrorCrossLinkDevice = 18,
        ErrorNoSuchDevice = 19,
        ErrorNotDirectory = 20,
        ErrorIsDirectory = 21,
        ErrorInvalidArgument = 22,
        ErrorFileTooBig = 27,
        ErrorNoSpaceLeftOnDevice = 28,
        ErrorReadOnlyFileSystem = 30,
        ErrorToManyHardLinks = 31,
        ErrorNameTooLong = 63,
        ErrorDirectoryNotEmpty = 66,
        ErrorUserQuotaExceeded = 69,
        ErrorStaleFileHandle = 70,
        ErrorTooManyRemoteLevels = 71,
        ErrorBadHandle = 10001,
        ErrorNotSynchronized = 10002,
        ErrorBadCookie = 10003,
        ErrorNotSupported = 10004,
        ErrorTooSmall = 10005,
        ErrorServerFault = 10006,
        ErrorBadType = 10007,
        ErrorJukeBox = 10008,
    }
}

namespace More.Nfs
{
    public enum FileType
    {
        Regular = 1,
        Directory = 2,
        BlockDevice = 3,
        CharacterDevice = 4,
        SymbolicLink = 5,
        Socket = 6,
        NamedPipe = 7,
    }
    [Flags]
    public enum ModeFlags
    {
        OtherExecute = 1,
        OtherWrite = 2,
        OtherRead = 4,
        GroupExecute = 8,
        GroupWrite = 16,
        GroupRead = 32,
        OwnerExecute = 64,
        OwnerWrite = 128,
        OwnerRead = 256,
        SaveSwappedText = 512,
        SetGidOnExec = 1024,
        SetUidOnExec = 2048,
        UnknownFlag1 = 4096,
        UnknownFlag2 = 8192,
        UnknownFlag3 = 16384,
        UnknownFlag4 = 32768,
    }
    public static class NfsPath
    {
        // Returns the share name, and also splits the sub directory list
        public static String SplitShareNameAndSubPath(String fullSharePath, out String subPath)
        {
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }
            if (fullSharePath.StartsWith("/"))
            {
                fullSharePath = fullSharePath.Substring(1);
            }
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }
            if (fullSharePath.EndsWith("/"))
            {
                fullSharePath = fullSharePath.Remove(fullSharePath.Length - 1);
            }
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }

            Int32 firstSlashIndex = fullSharePath.IndexOf('/');
            if (firstSlashIndex < 0)
            {
                subPath = null;
                return fullSharePath;
            }
            else
            {
                subPath = fullSharePath.Substring(firstSlashIndex + 1);
                return fullSharePath.Remove(firstSlashIndex);
            }
        }


        //
        // Returns the leaf of the path, or the path itself if there is no parent.
        // If there is no parent parent will be set to null
        //
        public static String LeafName(String path)
        {
            if (path == null || path.Length == 0) return null;

            // Remove ending '/'
            if (path[path.Length - 1] == '/')
            {
                path = path.Remove(path.Length - 1);
            }

            if (path == null || path.Length == 0) return null;


            // Find last '/' (skip current last element because it shouldn't be a /'/)
            for (int i = path.Length - 2; i >= 0; i--)
            {
                if (path[i] == '/')
                {
                    return path.Substring(i + 1);
                }
            }

            return path;
        }
    }
    public struct Time
    {
        public const UInt32 FixedSerializationLength = 8;

        /*
        static InstanceSerializer serializer = null;
        public static InstanceSerializer Serializer
        {
            get
            {
                if (serializer == null) serializer = new InstanceSerializer();
                return serializer;
            }
        }

        public class InstanceSerializer : FixedLengthInstanceSerializer<Time>
        {
            public InstanceSerializer() { }
            public override UInt32 FixedSerializationLength() { return Time.FixedSerializationLength; }
            public override void FixedLengthSerialize(Byte[] bytes, UInt32 offset, Time instance)
            {
                bytes.BigEndianSetUInt32(offset, instance.seconds);
                offset += 4;
                bytes.BigEndianSetUInt32(offset, instance.nanoseconds);
                offset += 4;
            }
            public override Time FixedLengthDeserialize(Byte[] bytes, UInt32 offset)
            {
                return new Time(
                    bytes.BigEndianReadUInt32(offset + 0), // seconds
                    bytes.BigEndianReadUInt32(offset + 4) // nanoseconds
                );
            }
            public override void DataString(Time instance, StringBuilder builder)
            {
                builder.Append("Time:{");
                builder.Append(instance.seconds);
                builder.Append(',');
                builder.Append(instance.nanoseconds);
                builder.Append("}");
            }
            public override void DataSmallString(Time instance, StringBuilder builder)
            {
                builder.Append("Time:{");
                builder.Append(instance.seconds);
                builder.Append(',');
                builder.Append(instance.nanoseconds);
                builder.Append("}");
            }
        }
        */
        public UInt32 seconds;
        public UInt32 nanoseconds;
        public Time(UInt32 seconds, UInt32 nanoseconds)
        {
            this.seconds = seconds;
            this.nanoseconds = nanoseconds;
        }
        /*
        public FixedLengthInstanceSerializerAdapter<Time> CreateSerializerAdapater()
        {
            return new FixedLengthInstanceSerializerAdapter<Time>(Serializer, this);
        }
        */
    }
    public struct FileAttributes
    {
        public const UInt32 FixedSerializationLength = 84;

        /*
        static InstanceSerializer serializer = null;
        public static InstanceSerializer Serializer
        {
            get
            {
                if (serializer == null) serializer = new InstanceSerializer();
                return serializer;
            }
        }
        public class InstanceSerializer : FixedLengthInstanceSerializer<FileAttributes>
        {
            public InstanceSerializer() { }
            public override UInt32 FixedSerializationLength() { return FileAttributes.FixedSerializationLength; }
            public override void FixedLengthSerialize(Byte[] bytes, UInt32 offset, FileAttributes instance)
            {
                BigEndianUnsignedEnumSerializer<FileType>.FourByteInstance.FixedLengthSerialize(bytes, offset, instance.fileType);
                offset += 4;
                BigEndianUnsignedEnumSerializer<ModeFlags>.FourByteInstance.FixedLengthSerialize(bytes, offset, instance.protectionMode);
                offset += 4;
                bytes.BigEndianSetUInt32(offset, instance.hardLinks);
                offset += 4;
                bytes.BigEndianSetUInt32(offset, instance.ownerUid);
                offset += 4;
                bytes.BigEndianSetUInt32(offset, instance.gid);
                offset += 4;
                bytes.BigEndianSetUInt64(offset, instance.fileSize);
                offset += 8;
                bytes.BigEndianSetUInt64(offset, instance.diskSize);
                offset += 8;
                bytes.BigEndianSetUInt32(offset, instance.specialData1);
                offset += 4;
                bytes.BigEndianSetUInt32(offset, instance.specialData2);
                offset += 4;
                bytes.BigEndianSetUInt64(offset, instance.fileSystemID);
                offset += 8;
                bytes.BigEndianSetUInt64(offset, instance.fileID);
                offset += 8;
                Time.Serializer.Serialize(bytes, offset, instance.lastAccessTime);
                offset += 8;
                Time.Serializer.Serialize(bytes, offset, instance.lastModifyTime);
                offset += 8;
                Time.Serializer.Serialize(bytes, offset, instance.lastAttributeModifyTime);
                offset += 8;
            }
            public override FileAttributes FixedLengthDeserialize(Byte[] bytes, UInt32 offset)
            {
                return new FileAttributes(
                    BigEndianUnsignedEnumSerializer<FileType>.FourByteInstance.FixedLengthDeserialize(bytes, offset + 0), // fileType
                    BigEndianUnsignedEnumSerializer<ModeFlags>.FourByteInstance.FixedLengthDeserialize(bytes, offset + 4), // protectionMode
                    bytes.BigEndianReadUInt32(offset + 8), // hardLinks
                    bytes.BigEndianReadUInt32(offset + 12), // ownerUid
                    bytes.BigEndianReadUInt32(offset + 16), // gid
                    bytes.BigEndianReadUInt64(offset + 20), // fileSize
                    bytes.BigEndianReadUInt64(offset + 28), // diskSize
                    bytes.BigEndianReadUInt32(offset + 36), // specialData1
                    bytes.BigEndianReadUInt32(offset + 40), // specialData2
                    bytes.BigEndianReadUInt64(offset + 44), // fileSystemID
                    bytes.BigEndianReadUInt64(offset + 52), // fileID
                    Time.Serializer.FixedLengthDeserialize(bytes, offset + 60), // lastAccessTime
                    Time.Serializer.FixedLengthDeserialize(bytes, offset + 68), // lastModifyTime
                    Time.Serializer.FixedLengthDeserialize(bytes, offset + 76) // lastAttributeModifyTime
                );
            }
            public override void DataString(FileAttributes instance, StringBuilder builder)
            {
                builder.Append("FileAttributes:{");
                builder.Append(instance.fileType);
                builder.Append(',');
                builder.Append(instance.protectionMode);
                builder.Append(',');
                builder.Append(instance.hardLinks);
                builder.Append(',');
                builder.Append(instance.ownerUid);
                builder.Append(',');
                builder.Append(instance.gid);
                builder.Append(',');
                builder.Append(instance.fileSize);
                builder.Append(',');
                builder.Append(instance.diskSize);
                builder.Append(',');
                builder.Append(instance.specialData1);
                builder.Append(',');
                builder.Append(instance.specialData2);
                builder.Append(',');
                builder.Append(instance.fileSystemID);
                builder.Append(',');
                builder.Append(instance.fileID);
                builder.Append(',');
                Time.Serializer.DataString(instance.lastAccessTime, builder);
                builder.Append(',');
                Time.Serializer.DataString(instance.lastModifyTime, builder);
                builder.Append(',');
                Time.Serializer.DataString(instance.lastAttributeModifyTime, builder);
                builder.Append("}");
            }
            public override void DataSmallString(FileAttributes instance, StringBuilder builder)
            {
                builder.Append("FileAttributes:{");
                builder.Append(instance.fileType);
                builder.Append(',');
                builder.Append(instance.protectionMode);
                builder.Append(',');
                builder.Append(instance.hardLinks);
                builder.Append(',');
                builder.Append(instance.ownerUid);
                builder.Append(',');
                builder.Append(instance.gid);
                builder.Append(',');
                builder.Append(instance.fileSize);
                builder.Append(',');
                builder.Append(instance.diskSize);
                builder.Append(',');
                builder.Append(instance.specialData1);
                builder.Append(',');
                builder.Append(instance.specialData2);
                builder.Append(',');
                builder.Append(instance.fileSystemID);
                builder.Append(',');
                builder.Append(instance.fileID);
                builder.Append(',');
                Time.Serializer.DataSmallString(instance.lastAccessTime, builder);
                builder.Append(',');
                Time.Serializer.DataSmallString(instance.lastModifyTime, builder);
                builder.Append(',');
                Time.Serializer.DataSmallString(instance.lastAttributeModifyTime, builder);
                builder.Append("}");
            }
        }
        */
        public FileType fileType;
        public ModeFlags protectionMode;
        public UInt32 hardLinks;
        public UInt32 ownerUid;
        public UInt32 gid;
        public UInt64 fileSize;
        public UInt64 diskSize;
        public UInt32 specialData1;
        public UInt32 specialData2;
        public UInt64 fileSystemID;
        public UInt64 fileID;
        public Time lastAccessTime;
        public Time lastModifyTime;
        public Time lastAttributeModifyTime;
        public FileAttributes(FileType fileType, ModeFlags protectionMode, UInt32 hardLinks, UInt32 ownerUid, UInt32 gid, UInt64 fileSize, UInt64 diskSize, UInt32 specialData1, UInt32 specialData2, UInt64 fileSystemID, UInt64 fileID, Time lastAccessTime, Time lastModifyTime, Time lastAttributeModifyTime)
        {
            this.fileType = fileType;
            this.protectionMode = protectionMode;
            this.hardLinks = hardLinks;
            this.ownerUid = ownerUid;
            this.gid = gid;
            this.fileSize = fileSize;
            this.diskSize = diskSize;
            this.specialData1 = specialData1;
            this.specialData2 = specialData2;
            this.fileSystemID = fileSystemID;
            this.fileID = fileID;
            this.lastAccessTime = lastAccessTime;
            this.lastModifyTime = lastModifyTime;
            this.lastAttributeModifyTime = lastAttributeModifyTime;
        }
        /*
        public FixedLengthInstanceSerializerAdapter<FileAttributes> CreateSerializerAdapater()
        {
            return new FixedLengthInstanceSerializerAdapter<FileAttributes>(Serializer, this);
        }
         */
    }

    public static class CommonComparisons
    {
        public static Int32 IncreasingInt32(Int32 x, Int32 y)
        {
            return (x > y) ? 1 : ((x < y) ? -1 : 0);
        }
        public static Int32 DecreasingInt32(Int32 x, Int32 y)
        {
            return (x > y) ? -1 : ((x < y) ? 1 : 0);
        }
        public static Int32 IncreasingUInt32(UInt32 x, UInt32 y)
        {
            return (x > y) ? 1 : ((x < y) ? -1 : 0);
        }
        public static Int32 DecreasingUInt32(UInt32 x, UInt32 y)
        {
            return (x > y) ? -1 : ((x < y) ? 1 : 0);
        }
    }
    public class SortedList<T> : IList<T>
    {
        public class Enumerator : IEnumerator<T>
        {
            public readonly SortedList<T> list;
            public UInt32 state;
            public Enumerator(SortedList<T> list)
            {
                this.list = list;
                this.state = UInt32.MaxValue;
            }
            public void Reset()
            {
                this.state = UInt32.MaxValue;
            }
            public void Dispose()
            {
            }
            public T Current
            {
                get { return list.elements[this.state]; }
            }
            object System.Collections.IEnumerator.Current
            {
                get { return list.elements[this.state]; }
            }
            public Boolean MoveNext()
            {
                state++;
                return state < list.count;
            }
        }

        public T[] elements;
        public UInt32 count;

        private readonly UInt32 extendLength;

        private readonly Comparison<T> comparison;

        public SortedList(UInt32 initialCapacity, UInt32 extendLength, Comparison<T> comparison)
        {
            if (comparison == null) throw new ArgumentNullException("comparison");

            this.elements = new T[initialCapacity];
            this.count = 0;

            this.extendLength = extendLength;

            this.comparison = comparison;
        }
        public T this[int i]
        {
            get { return elements[i]; }
            set { throw new InvalidOperationException("Cannot set an element to a specific index on a SortedList"); }
        }
        public Boolean IsReadOnly
        {
            get { return false; }
        }
        public Int32 Count { get { return (Int32)this.count; } }
        public Boolean Contains(T item)
        {
            for (int i = 0; i < count; i++)
            {
                if (item.Equals(elements[i]))
                {
                    return true;
                }
            }
            return false;
        }
        public Int32 IndexOf(T item)
        {
            for (int i = 0; i < count; i++)
            {
                T listItem = elements[i];
                if (listItem.Equals(item)) return i;
            }
            return -1;
        }
        public void CopyTo(T[] array, Int32 arrayIndex)
        {
            for (int i = 0; i < count; i++)
            {
                array[arrayIndex++] = elements[i];
            }
        }
        public void Insert(int index, T item)
        {
            throw new InvalidOperationException("Cannot insert an element at a specific index on a SortedList");
        }
        public void Add(T newElement)
        {
            if (count >= elements.Length)
            {
                T[] newElements = new T[elements.Length + extendLength];
                Array.Copy(elements, newElements, elements.Length);
                elements = newElements;
            }

            UInt32 position;
            for (position = 0; position < count; position++)
            {
                T element = elements[position];
                if (comparison(newElement, element) <= 0)
                {
                    // Move remaining elements
                    for (UInt32 copyPosition = count; copyPosition > position; copyPosition--)
                    {
                        elements[copyPosition] = elements[copyPosition - 1];
                    }
                    break;
                }
            }

            elements[position] = newElement;
            count++;
        }

        public void Clear()
        {
            // remove references if necessary
            if (typeof(T).IsClass)
            {
                for (int i = 0; i < count; i++)
                {
                    this.elements[i] = default(T);
                }
            }
            this.count = 0;
        }
        public T GetAndRemoveLastElement()
        {
            count--;
            T element = elements[count];

            elements[count] = default(T); // Delete reference to this object

            return element;
        }
        public Boolean Remove(T element)
        {
            for (int i = 0; i < count; i++)
            {
                if (element.Equals(elements[i]))
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        public void RemoveAt(int index)
        {
            while (index < count - 1)
            {
                elements[index] = elements[index + 1];
                index++;
            }
            elements[index] = default(T); // Delete reference to this object
            count--;
        }
        public void RemoveFromStart(UInt32 count)
        {
            if (count <= 0) return;
            if (count >= this.count)
            {
                Clear();
                return;
            }

            this.count -= count;
            for (int i = 0; i < this.count; i++)
            {
                elements[i] = elements[count + i];
            }

            if (typeof(T).IsClass)
            {
                for (int i = 0; i < count; i++)
                {
                    this.elements[this.count + i] = default(T);
                }
            }
        }
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
    public class UniqueIndexObjectDictionary<ObjectType>
    {
        public interface IObjectGenerator
        {
            ObjectType GenerateObject(UInt32 uniqueIndex);
        }

        ObjectType[] objects;
        UInt32 nextIndex;

        readonly UInt32 extendLength;

        readonly SortedList<UInt32> sortedFreeIndices;
        readonly Dictionary<ObjectType, UInt32> objectToIndexDictionary;

        public UniqueIndexObjectDictionary(UInt32 initialFreeStackCapacity, UInt32 freeStackExtendLength,
            UInt32 initialTotalObjectsCapacity, UInt32 extendLength, IEqualityComparer<ObjectType> objectComparer)
        {
            this.objects = new ObjectType[initialTotalObjectsCapacity];
            nextIndex = 0;

            this.extendLength = extendLength;

            this.sortedFreeIndices = new SortedList<UInt32>(initialFreeStackCapacity, freeStackExtendLength, CommonComparisons.IncreasingUInt32);
            this.objectToIndexDictionary = new Dictionary<ObjectType, UInt32>(objectComparer);
        }
        public UniqueIndexObjectDictionary(UInt32 initialFreeStackCapacity, UInt32 freeStackExtendLength,
            UInt32 initialTotalObjectsCapacity, UInt32 extendLength)
        {
            this.objects = new ObjectType[initialTotalObjectsCapacity];
            nextIndex = 0;

            this.extendLength = extendLength;

            this.sortedFreeIndices = new SortedList<UInt32>(initialFreeStackCapacity, freeStackExtendLength, CommonComparisons.IncreasingUInt32);
            this.objectToIndexDictionary = new Dictionary<ObjectType, UInt32>();
        }
        private UInt32 GetFreeUniqueIndex()
        {
            if (sortedFreeIndices.count > 0) return sortedFreeIndices.GetAndRemoveLastElement();

            if (nextIndex >= UInt32.MaxValue)
                throw new InvalidOperationException(String.Format("The Free Stack Unique Object Tracker is tracking too many objects: {0}", nextIndex));

            // Make sure the local path buffer is big enough
            if (nextIndex >= objects.Length)
            {
                // extend local path array
                ObjectType[] newObjectsArray = new ObjectType[objects.Length + extendLength];
                Array.Copy(objects, newObjectsArray, objects.Length);
                objects = newObjectsArray;
            }

            UInt32 newestObjectIndex = nextIndex;
            nextIndex++;
            return newestObjectIndex;
        }
        public UInt32 GetUniqueIndexOf(ObjectType obj)
        {
            UInt32 uniqueIndex;
            if (objectToIndexDictionary.TryGetValue(obj, out uniqueIndex)) return uniqueIndex;

            uniqueIndex = GetFreeUniqueIndex();
            objects[uniqueIndex] = obj;
            objectToIndexDictionary.Add(obj, uniqueIndex);

            return uniqueIndex;
        }
        public ObjectType GetObject(UInt32 uniqueIndex)
        {
            return objects[uniqueIndex];
        }
        public UInt32 Add(ObjectType newObject)
        {
            UInt32 uniqueIndex = GetFreeUniqueIndex();

            objects[uniqueIndex] = newObject;
            objectToIndexDictionary.Add(newObject, uniqueIndex);

            return uniqueIndex;
        }
        public ObjectType GenerateNewObject(out UInt32 uniqueIndex, IObjectGenerator objectGenerator)
        {
            uniqueIndex = GetFreeUniqueIndex();

            ObjectType newObject = objectGenerator.GenerateObject(uniqueIndex);
            objects[uniqueIndex] = newObject;
            objectToIndexDictionary.Add(newObject, uniqueIndex);

            return newObject;
        }
        public void Free(UInt32 uniqueIndex)
        {
            ObjectType obj = objects[uniqueIndex];
            objectToIndexDictionary.Remove(obj);

            if (uniqueIndex == nextIndex - 1)
            {
                while (true)
                {
                    nextIndex--;
                    if (nextIndex <= 0) break;
                    if (sortedFreeIndices.count <= 0) break;
                    if (sortedFreeIndices.elements[sortedFreeIndices.count - 1] != nextIndex - 1) break;
                    sortedFreeIndices.count--;
                }
            }
            else
            {
                sortedFreeIndices.Add(uniqueIndex);
            }
        }
    }

}
