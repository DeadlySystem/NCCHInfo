using System;
using System.Reflection;
using System.Runtime.InteropServices;




/// <summary>
/// Provides methods to marshal and unmarshal structs to and from byte arrays.
/// For this to work, the struct type in question should have an appropriate StructLayout attribute
/// and its array fields must have a a MarshalAs attribute that specifies their size.
/// The custom ByteOrder attribute can be specified to enforce byte orders (<seealso cref="ByteOrderAttibute"/>).
/// </summary>
/// <example>
/// The following is an example of a struct that can be processed by this class:
///
/// <code>
/// [StructLayout(LayoutKind.Sequential, Pack = 1), ByteOrder(ByteOrder.Default)]
/// public struct Entry
/// {
///     public UInt32 type;
///
///     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16), ByteOrder(ByteOrder.LittleEndian)]
///     public byte[] data;
/// }
/// </code>
/// </example>
public class StructMarshaling
{
    /// <summary>
    /// Marshals the given struct and returns the resulting byte array.
    /// </summary>
    /// <typeparam name="T">The type of the struct to be marshaled.</typeparam>
    /// <param name="sourcestruct">The struct to be marshaled.</param>
    /// <returns>Returns the marshaled struct as a byte array.</returns>
    public static byte[] StructToBytes<T>(T sourcestruct) where T : struct
    {
        /*
            * Ensure that the struct can be marshaled:
            *
            * - All array fields must have a MarshalAs attribute
            * - Probably more, but not needed at this point
            *
            */
        FieldInfo[] fields = typeof(T).GetFields();
        foreach (FieldInfo field in fields)
        {
            if (field.FieldType.IsArray)
            {
                //Verify that the MarshalAs attribute is set
                if (!field.IsDefined(typeof(MarshalAsAttribute)))
                {
                    throw new InvalidOperationException("Struct " + typeof(T).Name + " contains one or more array fields that do not have an appropriate MarshalAs attribute!");
                }
            }
        }

        //Find the size of the structure and allocate a corresponding amount of unmanaged memory, then store the structure in that memory
        int structsize = Marshal.SizeOf(typeof(T));
        IntPtr unmanagedstruct = Marshal.AllocHGlobal(structsize);
        Marshal.StructureToPtr(sourcestruct, unmanagedstruct, true);

        //Copy the structure's bytes from the unmanaged memory to a new managed byte array
        byte[] structbytes = new byte[structsize];
        Marshal.Copy(unmanagedstruct, structbytes, 0, structsize);

        //Free the unmanaged memory
        Marshal.FreeHGlobal(unmanagedstruct);

        //Adjust the byte order to match the specified one
        FixByteOrder<T>(structbytes);

        return structbytes;
    }

    /// <summary>
    /// Unmarshals the given byte array to a struct.
    /// </summary>
    /// <typeparam name="T">The struct type that the byte array should be unmarshaled to.</typeparam>
    /// <param name="structbytes">The byte array to be unmarshaled.</param>
    /// <returns>The unmarshaled struct.</returns>
    public static T BytesToStruct<T>(byte[] structbytes) where T : struct
    {
        //Adjust the byte order to match that of the current platform
        FixByteOrder<T>(structbytes);

        //Prevent the Garbage Collector from moving the buffer
        GCHandle handle = GCHandle.Alloc(structbytes, GCHandleType.Pinned);

        //Marshal the bytes
        T result = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

        //Return control to the Garbage Collector
        handle.Free();

        return result;
    }

    /// <summary>
    /// Internal helper method that adjusts the byte order of a struct's fields
    /// to match the byte order requested by a custom ByteOrder attribute.
    /// This method modifies the given array directly.
    /// </summary>
    /// <typeparam name="T">The type of the struct whose byte order should be adjusted.</typeparam>
    /// <param name="structbytes">A byte array representing the marshaled version of the target struct.</param>
    private static void FixByteOrder<T>(byte[] structbytes) where T : struct
    {
        //Find the byte order of the struct if specified
        ByteOrder classbyteorder = ByteOrder.Default;
        if (typeof(T).IsDefined(typeof(ByteOrderAttribute), false))
        {
            classbyteorder = typeof(T).GetCustomAttribute<ByteOrderAttribute>().ByteOrder;
        }

        //Iterate over the struct's fields
        FieldInfo[] fields = typeof(T).GetFields();
        foreach (FieldInfo field in fields)
        {
            //Find the byte order of the current field if specified, defaulting to the struct's byte order if unspecified
            ByteOrder fieldbyteorder = classbyteorder;
            if (field.IsDefined(typeof(ByteOrderAttribute), false))
            {
                fieldbyteorder = field.GetCustomAttribute<ByteOrderAttribute>().ByteOrder;
            }

            //Compare the applicable byte order with the system's byte order
            if ((fieldbyteorder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian) || (fieldbyteorder == ByteOrder.BigEndian && BitConverter.IsLittleEndian))
            {
                //If the byte orders disagree, perform a conversion
                int fieldoffset = Marshal.OffsetOf(typeof(T), field.Name).ToInt32();
                if (field.FieldType.IsArray)
                {
                    //Verify that the MarshalAs attribute is set
                    if (!field.IsDefined(typeof(MarshalAsAttribute)))
                    {
                        throw new InvalidOperationException("Struct " + typeof(T).Name + " contains one or more array fields that do not have an appropriate MarshalAs attribute!");
                    }
                    MarshalAsAttribute marshalasattribute = field.GetCustomAttribute<MarshalAsAttribute>();
                    int arraylength = marshalasattribute.SizeConst; //Will be 1 if not set explicitly
                    int elementsize = Marshal.SizeOf(field.FieldType.GetElementType());
                    for (int i = 0; i < arraylength; ++i)
                    {
                        Array.Reverse(structbytes, fieldoffset + i * elementsize, elementsize);
                    }
                }
                else
                {
                    int size = Marshal.SizeOf(field.FieldType);
                    Array.Reverse(structbytes, fieldoffset, size);
                }
            }
        }
    }
}




[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field)]
public class ByteOrderAttribute : Attribute
{
    public ByteOrder ByteOrder { get; private set; }

    public ByteOrderAttribute(ByteOrder byteorder)
    {
        this.ByteOrder = byteorder;
    }
}




public enum ByteOrder
{
    Default,
    BigEndian,
    LittleEndian
}