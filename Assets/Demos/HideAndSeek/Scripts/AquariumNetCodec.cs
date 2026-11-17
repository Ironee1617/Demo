using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R85: the wire format of the session's messages. It replaces JsonUtility text (a snapshot was about 22 KB of
    // JSON, sent 20 times a second) with a compact binary form of the same fields, deflated.
    // The fields written are the ones JsonUtility wrote: public (or [SerializeField]) instance fields of
    // [Serializable] types, without [NonSerialized], in declaration order. Both ends must run the same build: every
    // packet starts with the session protocol number, and a packet from another protocol is rejected.
    // Like JsonUtility, null strings come back as "" and null arrays and lists come back empty.
    public static class AquariumNetCodec
    {
        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, bool> supportedCache = new Dictionary<Type, bool>();

        public static byte[] Encode(object message, int protocol)
        {
            using (var raw = new MemoryStream())
            {
                var header = BitConverter.GetBytes(protocol);
                raw.Write(header, 0, 4);
                using (var deflate = new DeflateStream(raw, System.IO.Compression.CompressionLevel.Fastest, true))
                using (var w = new BinaryWriter(deflate, Encoding.UTF8, true))
                    WriteValue(w, message.GetType(), message);
                return raw.ToArray();
            }
        }

        // Returns false for another protocol or a damaged packet.
        public static bool TryDecode<T>(byte[] data, int protocol, out T message) where T : class
        {
            message = null;
            if (data == null || data.Length < 5 || BitConverter.ToInt32(data, 0) != protocol) return false;
            try
            {
                using (var raw = new MemoryStream(data, 4, data.Length - 4))
                using (var inflate = new DeflateStream(raw, CompressionMode.Decompress))
                using (var r = new BinaryReader(inflate, Encoding.UTF8))
                    message = (T)ReadValue(r, typeof(T));
                return message != null;
            }
            catch (Exception) { message = null; return false; }
        }

        // ---- fields -----------------------------------------------------------------------------------------------
        private static FieldInfo[] Fields(Type t)
        {
            FieldInfo[] fields;
            if (fieldCache.TryGetValue(t, out fields)) return fields;
            var list = new List<FieldInfo>();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.IsNotSerialized || f.IsInitOnly || f.IsLiteral) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                if (!Supported(f.FieldType)) continue;
                list.Add(f);
            }
            list.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            fields = list.ToArray();
            fieldCache[t] = fields;
            return fields;
        }

        private static bool Supported(Type t)
        {
            bool ok;
            if (supportedCache.TryGetValue(t, out ok)) return ok;
            supportedCache[t] = true;   // breaks cycles while deciding
            if (t.IsPrimitive || t.IsEnum || t == typeof(string)) ok = true;
            else if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Quaternion) || t == typeof(Color)) ok = true;
            else if (t.IsArray) ok = t.GetArrayRank() == 1 && Supported(t.GetElementType());
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) ok = Supported(t.GetGenericArguments()[0]);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface || t.IsGenericType) ok = false;
            else ok = t.IsSerializable;
            supportedCache[t] = ok;
            return ok;
        }

        // ---- write ------------------------------------------------------------------------------------------------
        private static void WriteVar(BinaryWriter w, uint v) { while (v >= 0x80) { w.Write((byte)(v | 0x80)); v >>= 7; } w.Write((byte)v); }
        private static void WriteInt(BinaryWriter w, int v) { WriteVar(w, (uint)((v << 1) ^ (v >> 31))); }

        private static void WriteValue(BinaryWriter w, Type t, object v)
        {
            if (t == typeof(int)) { WriteInt(w, (int)v); return; }
            if (t == typeof(float)) { w.Write((float)v); return; }
            if (t == typeof(bool)) { w.Write((bool)v); return; }
            if (t == typeof(string))
            {
                var s = (string)v;
                if (string.IsNullOrEmpty(s)) { WriteVar(w, 0); return; }
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteVar(w, (uint)bytes.Length + 1); w.Write(bytes); return;
            }
            if (t.IsEnum) { WriteInt(w, Convert.ToInt32(v)); return; }
            if (t.IsPrimitive) { WritePrimitive(w, t, v); return; }
            if (t.IsArray || t.IsGenericType)
            {
                var list = v as IList;
                if (list == null) { WriteVar(w, 0); return; }
                var e = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                WriteVar(w, (uint)list.Count + 1);
                for (int i = 0; i < list.Count; i++) WriteValue(w, e, list[i]);
                return;
            }
            if (!t.IsValueType)
            {
                if (v == null) { w.Write(false); return; }
                w.Write(true);
            }
            foreach (var f in Fields(t)) WriteValue(w, f.FieldType, f.GetValue(v));
        }

        private static void WritePrimitive(BinaryWriter w, Type t, object v)
        {
            if (t == typeof(byte)) w.Write((byte)v);
            else if (t == typeof(sbyte)) w.Write((sbyte)v);
            else if (t == typeof(short)) w.Write((short)v);
            else if (t == typeof(ushort)) w.Write((ushort)v);
            else if (t == typeof(uint)) w.Write((uint)v);
            else if (t == typeof(long)) w.Write((long)v);
            else if (t == typeof(ulong)) w.Write((ulong)v);
            else if (t == typeof(double)) w.Write((double)v);
            else if (t == typeof(char)) w.Write((ushort)(char)v);
        }

        // ---- read -------------------------------------------------------------------------------------------------
        private static uint ReadVar(BinaryReader r)
        {
            uint v = 0; int shift = 0;
            while (true)
            {
                byte b = r.ReadByte();
                v |= (uint)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return v;
                shift += 7;
                if (shift > 28) throw new InvalidDataException("varint");
            }
        }
        private static int ReadInt(BinaryReader r) { uint v = ReadVar(r); return (int)(v >> 1) ^ -(int)(v & 1); }

        private static object ReadValue(BinaryReader r, Type t)
        {
            if (t == typeof(int)) return ReadInt(r);
            if (t == typeof(float)) return r.ReadSingle();
            if (t == typeof(bool)) return r.ReadBoolean();
            if (t == typeof(string))
            {
                uint n = ReadVar(r);
                if (n == 0) return "";
                if (n > 1 << 20) throw new InvalidDataException("string");
                return Encoding.UTF8.GetString(r.ReadBytes((int)n - 1));
            }
            if (t.IsEnum) return Enum.ToObject(t, ReadInt(r));
            if (t.IsPrimitive) return ReadPrimitive(r, t);
            if (t.IsArray)
            {
                var e = t.GetElementType();
                uint n = ReadVar(r);
                int count = n == 0 ? 0 : (int)n - 1;
                if (count > 100000) throw new InvalidDataException("array");
                var a = Array.CreateInstance(e, count);
                for (int i = 0; i < count; i++) a.SetValue(ReadValue(r, e), i);
                return a;
            }
            if (t.IsGenericType)
            {
                var e = t.GetGenericArguments()[0];
                uint n = ReadVar(r);
                int count = n == 0 ? 0 : (int)n - 1;
                if (count > 100000) throw new InvalidDataException("list");
                var list = (IList)Activator.CreateInstance(t);
                for (int i = 0; i < count; i++) list.Add(ReadValue(r, e));
                return list;
            }
            if (!t.IsValueType && !r.ReadBoolean()) return null;
            object o = Activator.CreateInstance(t, true);
            foreach (var f in Fields(t)) f.SetValue(o, ReadValue(r, f.FieldType));
            return o;
        }

        private static object ReadPrimitive(BinaryReader r, Type t)
        {
            if (t == typeof(byte)) return r.ReadByte();
            if (t == typeof(sbyte)) return r.ReadSByte();
            if (t == typeof(short)) return r.ReadInt16();
            if (t == typeof(ushort)) return r.ReadUInt16();
            if (t == typeof(uint)) return r.ReadUInt32();
            if (t == typeof(long)) return r.ReadInt64();
            if (t == typeof(ulong)) return r.ReadUInt64();
            if (t == typeof(double)) return r.ReadDouble();
            if (t == typeof(char)) return (char)r.ReadUInt16();
            throw new InvalidDataException("primitive " + t);
        }
    }
}
