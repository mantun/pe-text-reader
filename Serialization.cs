using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;


namespace TextReader.Serialization {

public class Serializer {
    public static void Serialize(object o, Stream s) {
        new ObjectWalker(new BinaryStreamFormatter(s)).Scan(o);
    }
    public static object Deserialize(Stream s) {
        return new ObjectWalker(new BinaryStreamFormatter(s)).Build();
    }

    private class ObjectWalker {
        private Formatter formatter;
        public ObjectWalker(Formatter formatter) {
            this.formatter = formatter;
        }
        public void Scan(object o) {
            if (o == null) {
                formatter.WriteInt(0);
            } else {
                formatter.WriteString(o.GetType().FullName);
                foreach (FieldInfo f in getFields(o.GetType())) {
                    object value = f.GetValue(o);
                    writeValue(f.FieldType, value);
                }
            }
        }
        public object Build() {
            string typeStr = formatter.ReadString();
            Type type = Type.GetType(typeStr);
            if (type == null) {
                return null;
            }
            ConstructorInfo ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
            if (ctor == null) {
                throw new ArgumentException("Type " + type + " does not have parameterless constructor");
            }
            object result = ctor.Invoke(new object[0]);
            foreach (FieldInfo f in getFields(type)) {
                object value = readValue(f.FieldType);
                f.SetValue(result, value);
            }
            return result;
        }
        private FieldInfo[] getFields(Type type) {
            return type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        private bool isList(Type type) {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }
        private void writeValue(Type declaredType, object value) {
            if (declaredType.IsValueType || declaredType == typeof(string)) {
                if (value.GetType() != declaredType) {
                    throw new ArgumentException("Cannot serialize value " + value.GetType() + " " + value + " with declared type " + declaredType);
                }
                writeSimpleValue(value);
            } else if (declaredType.IsArray) {
                if (value.GetType().GetElementType() != declaredType.GetElementType()) {
                    throw new ArgumentException("Cannot serialize array " + value + " with declared type " + declaredType);
                }
                writeArray((Array) value);
            } else if (isList(declaredType)) {
                writeList(value);
            } else {
                Scan(value);
            }
        }
        private object readValue(Type declaredType) {
            if (declaredType.IsValueType || declaredType == typeof(string)) {
                return readSimpleValue(declaredType);
            } else if (declaredType.IsArray) {
                return readArray(declaredType);
            } else if (isList(declaredType)) {
                return readList(declaredType);
            } else {
                return Build();
            }
        }

        private void writeSimpleValue(object value) {
            if (value == null) {
                formatter.WriteInt(0);
                return;
            } 
            Type type = value.GetType();
            if (type == typeof(int)) {
                formatter.WriteInt((int) value);
            } else if (type == typeof(long)) {
                formatter.WriteLong((long) value);
            } else if (type == typeof(string)) {
                formatter.WriteString((string) value);
            } else if (type.IsEnum) {
                formatter.WriteEnum((Enum) value);
            } else {
                throw new ArgumentException("Cannot serialize value " + type + " " + value);
            }
        }
        private object readSimpleValue(Type type) {
            if (type == typeof(int)) {
                return formatter.ReadInt();
            } else if (type == typeof(long)) {
                return formatter.ReadLong();
            } else if (type == typeof(string)) {
                return formatter.ReadString();
            } else if (type.IsEnum) {
                return formatter.ReadEnum(type);
            } else {
                throw new ArgumentException("Cannot deserialize " + type);
            }
        }
        private void writeArray(Array value) {
            if (value == null) {
                formatter.WriteInt(0);
                return;
            } 
            formatter.WriteInt(value.Length);
            for (int i = 0; i < value.Length; i++) {
                writeValue(value.GetType().GetElementType(), value.GetValue(i));
            }
        }
        private object readArray(Type type) {
            int len = formatter.ReadInt();
            Array result = Array.CreateInstance(type.GetElementType(), len);
            for (int i = 0; i < result.Length; i++) {
                result.SetValue(readValue(type.GetElementType()), i);
            }
            return result;
        }
        private void writeList(object value) {
            if (value == null) {
                formatter.WriteInt(0);
                return;
            }
            Type listTypeArgument = value.GetType().GetGenericArguments()[0];
            MethodInfo genericWriteListMethod = this.GetType().GetMethod("writeListGeneric", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo writeListMethod = genericWriteListMethod.MakeGenericMethod(listTypeArgument);
            writeListMethod.Invoke(this, new object[] { value });
        }
        private void writeListGeneric<T>(List<T> value) {
            formatter.WriteInt(value.Count);
            foreach (T v in value) {
                writeValue(typeof(T), v);
            }
        }
        private object readList(Type declaredListType) {
            Type listTypeArgument = declaredListType.GetGenericArguments()[0];
            MethodInfo genericReadListMethod = this.GetType().GetMethod("readListGeneric", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo readListMethod = genericReadListMethod.MakeGenericMethod(listTypeArgument);
            return readListMethod.Invoke(this, new object[] { declaredListType });
        }
        private object readListGeneric<T>(Type declaredListType) {
            Type listTypeArgument = declaredListType.GetGenericArguments()[0];
            List<T> result = (List<T>) Activator.CreateInstance(declaredListType);
            int len = formatter.ReadInt();
            for (int i = 0; i < len; i++) {
                result.Add((T) readValue(listTypeArgument));
            }
            return result;
        }
    }
}

interface Formatter {
    void WriteInt(int i);
    void WriteString(string str);
    void WriteLong(long l);
    void WriteEnum(Enum value);
    string ReadString();
    int ReadInt();
    long ReadLong();
    object ReadEnum(Type type);
}

interface ObjectReader {
    Type ReadObjectType();
    object ReadFieldValue(FieldInfo f);
}

class BinaryStreamFormatter : Formatter {
    Stream s;
    public BinaryStreamFormatter(Stream s) {
        this.s = s;
    }
    public void WriteInt(int i) {
        s.Write(new byte[] { (byte) i, (byte) (i >> 8), (byte) (i >> 16), (byte) (i >> 24) }, 0, 4);
    }
    public void WriteString(string str) {
        WriteInt(str.Length);
        char[] chars = str.ToCharArray();
        byte[] bytes = new byte[chars.Length * 2];
        for (int i = 0; i < chars.Length; i++) {
            bytes[i * 2] = (byte) chars[i];
            bytes[i * 2 + 1] = (byte) (chars[i] >> 8);
        }
        s.Write(bytes, 0, bytes.Length);
    }
    public void WriteLong(long l) {
        WriteInt((int) l);
        WriteInt((int) (l >> 32));
    }
    public void WriteEnum(Enum value) {
        WriteString(value.ToString());
    }
    public string ReadString() {
        int len = ReadInt();
        byte[] bytes = new byte[len * 2];
        s.Read(bytes, 0, bytes.Length);
        char[] chars = new char[len];
        for (int i = 0; i < len; i++) {
            chars[i] = (char) (bytes[i * 2] + ((char) bytes[i * 2 + 1] << 8));
        }
        return new string(chars);
    }
    public int ReadInt() {
        byte[] b = new byte[4];
        s.Read(b, 0, b.Length);
        return b[0] + ((int) b[1] << 8) + ((int) b[2] << 16) + ((int) b[3] << 24);
    }
    public long ReadLong() {
        int i1 = ReadInt();
        int i2 = ReadInt();
        return i1 + ((long) i2 << 32);
    }
    public object ReadEnum(Type type) {
        string value = ReadString();
        return Enum.Parse(type, value, false);
    }
}

}