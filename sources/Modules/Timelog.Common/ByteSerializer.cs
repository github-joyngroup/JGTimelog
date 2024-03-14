using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;
using Binaron.Serializer;
using Binaron.Serializer.CustomObject;

namespace Timelog.Common
{
    public static class ByteSerializer<T>
    {
        public static byte[] Serialize(T obj)
        {
            if(obj is null)
            {
                return null;
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, obj);
                return memoryStream.ToArray();
            }
        }

        public static T Deserialize(byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                return (T)binaryFormatter.Deserialize(memoryStream);
            }
        }
    }

    public static class JsonSerializer<T>
    {
        public static byte[] Serialize(T obj)
        {
            if(obj is null)
            {
                return null;
            }

            string jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
            return Encoding.UTF8.GetBytes(jsonString);
        }

        public static T Deserialize(byte[] data)
        {
            string jsonString = Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
        }
    }

    //class Binaron serializer
    public static class BinaronSerializer<T>
    {
        public static byte[] Serialize(T obj)
        {
            byte[] buf;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                BinaronConvert.Serialize(obj, ms);
                ms.Flush();
                ms.Position = 0;
                buf = ms.ToArray();
            }

            return buf;
        }

        public static T Deserialize(byte[] data)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
            {
                ms.Flush();
                ms.Position = 0;
                return BinaronConvert.Deserialize<T>(ms);
            }
        }
    }
}
