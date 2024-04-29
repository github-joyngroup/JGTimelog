using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Joyn.Timelog.Common.Models;

namespace Joyn.Timelog.Common
{
    public static class ProtoBufSerializer
    {
        public static byte[] Serialize<T>(T obj)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, obj);
                return memoryStream.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(memoryStream);
            }
        }
    }

    ///// <summary>
    ///// Provides methods to serialize and deserialize objects to and from byte arrays
    ///// </summary>
    ///// <typeparam name="T"></typeparam>
    //public static class ByteSerializer<T>
    //{
    //    public static byte[] Serialize(T obj)
    //    {
    //        if(obj is null)
    //        {
    //            return null;
    //        }

    //        using (MemoryStream memoryStream = new MemoryStream())
    //        {
    //            BinaryFormatter binaryFormatter = new BinaryFormatter();
    //            binaryFormatter.Serialize(memoryStream, obj);
    //            return memoryStream.ToArray();
    //        }
    //    }

    //    public static T Deserialize(byte[] data)
    //    {
    //        using (MemoryStream memoryStream = new MemoryStream(data))
    //        {
    //            BinaryFormatter binaryFormatter = new BinaryFormatter();
    //            return (T)binaryFormatter.Deserialize(memoryStream);
    //        }
    //    }
    //}
}
