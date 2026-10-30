using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>
    /// Writes a format class's public fields as JSON and reads them back, in declaration order with snake_case
    /// keys. Enums are written by name, references to other records as "33 Tackle".
    /// </summary>
    public static class ReflectedFields
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>The name of the record a numbered member refers to, by member name; null for plain numbers.</summary>
        public delegate Func<int, string> RefLookup(string member);

        public static JsonObject Write(object target, RefLookup refs, ISet<string> skip = null)
        {
            JsonObject json = new JsonObject();
            foreach (MemberInfo m in Members(target.GetType(), skip))
            {
                object value = Get(m, target);
                JsonNode node = ToNode(value, MemberType(m), refs?.Invoke(m.Name), refs);
                if (node != null || value == null) json[Key(m.Name)] = node;
            }
            return json;
        }

        public static void Read(JsonObject json, object target, RefLookup refs, ISet<string> skip = null, Func<Type, object> create = null)
        {
            foreach (MemberInfo m in Members(target.GetType(), skip))
            {
                string key = Key(m.Name);
                if (!json.TryGetPropertyValue(key, out JsonNode node)) throw new InvalidDataException($"Missing \"{key}\".");
                Type type = MemberType(m);
                if (!Supported(type)) continue;
                object current = Get(m, target);
                object value = FromNode(node, type, current, refs, create);
                if (m is FieldInfo { IsInitOnly: true } || m is PropertyInfo { CanWrite: false })
                {
                    // A read-only list is refilled in place.
                    IList list = (IList)current;
                    list.Clear();
                    foreach (object item in (IList)value) list.Add(item);
                }
                else Set(m, target, value);
            }
        }

        private static IEnumerable<MemberInfo> Members(Type type, ISet<string> skip)
        {
            IEnumerable<MemberInfo> fields = type.GetFields(Instance).Where(f => !f.IsLiteral && (!f.IsInitOnly || IsList(f.FieldType, out _)));
            IEnumerable<MemberInfo> props = type.GetProperties(Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                            && (p.CanWrite && p.GetSetMethod() != null || !p.CanWrite && IsList(p.PropertyType, out _)));
            return fields.Concat(props)
                .Where(m => skip == null || !skip.Contains(m.Name))
                .Where(m => Supported(MemberType(m)))
                .OrderBy(m => m is PropertyInfo ? 1 : 0).ThenBy(m => m.MetadataToken);
        }

        private static Type MemberType(MemberInfo m) => m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
        private static object Get(MemberInfo m, object target) => m is FieldInfo f ? f.GetValue(target) : ((PropertyInfo)m).GetValue(target);

        private static void Set(MemberInfo m, object target, object value)
        {
            if (m is FieldInfo f) f.SetValue(target, value);
            else ((PropertyInfo)m).SetValue(target, value);
        }

        private static bool IsScalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string);

        private static bool Supported(Type t)
        {
            if (IsScalar(t)) return true;
            if (t.IsArray) return Supported(t.GetElementType());
            if (IsSet(t, out Type e) || IsList(t, out e)) return Supported(e);
            if (t.IsValueType && t.GetFields(Instance).Length > 0 && !t.Name.StartsWith("ValueTuple", StringComparison.Ordinal)) return true;
            return t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("DSPRE", StringComparison.Ordinal) == true;
        }

        private static bool IsSet(Type t, out Type element)
        {
            element = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SortedSet<>) ? t.GetGenericArguments()[0] : null;
            return element != null;
        }

        private static bool IsList(Type t, out Type element)
        {
            element = t.IsGenericType && t.GetGenericArguments().Length == 1 && typeof(IList).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null
                ? t.GetGenericArguments()[0]
                : null;
            return element != null;
        }

        private static JsonNode ToNode(object value, Type type, Func<int, string> refName, RefLookup refs)
        {
            if (value == null) return null;
            if (type.IsEnum)
            {
                string name = Enum.GetName(type, value);
                if (type.IsDefined(typeof(FlagsAttribute), false) && name == null)
                    return new JsonArray(Enum.GetValues(type).Cast<object>().Where(v => Convert.ToInt64(v) != 0 && ((Enum)value).HasFlag((Enum)v))
                        .Select(v => (JsonNode)Enum.GetName(type, v)).ToArray());
                return name != null ? (JsonNode)name : Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            if (type == typeof(bool)) return (bool)value;
            if (type == typeof(string)) return (string)value;
            if (type.IsPrimitive)
            {
                long number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (refName != null) return ChangesJson.Ref((int)number, SafeName(refName, (int)number));
                return number;
            }
            if (type.IsArray || IsSet(type, out _) || IsList(type, out _))
            {
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return new JsonArray(((IEnumerable)value).Cast<object>().Select(v => ToNode(v, element, refName, refs)).ToArray());
            }
            return Write(value, refs);
        }

        private static object FromNode(JsonNode node, Type type, object current, RefLookup refs, Func<Type, object> create)
        {
            if (node == null) return null;
            if (type.IsEnum)
            {
                if (node is JsonArray flags)
                {
                    long bits = 0;
                    foreach (JsonNode f in flags) bits |= Convert.ToInt64(Enum.Parse(type, f.GetValue<string>()), CultureInfo.InvariantCulture);
                    return Enum.ToObject(type, bits);
                }
                if (node is JsonValue v && v.TryGetValue(out string name)) return Enum.Parse(type, name);
                return Enum.ToObject(type, node.GetValue<long>());
            }
            if (type == typeof(bool)) return node.GetValue<bool>();
            if (type == typeof(string)) return node.GetValue<string>();
            if (type.IsPrimitive)
            {
                long number = node is JsonValue value && value.TryGetValue(out string text) ? ChangesJson.ParseRef(text) : node.GetValue<long>();
                return Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
            }
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                JsonArray items = (JsonArray)node;
                Array old = current as Array;
                Array array = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++)
                    array.SetValue(FromNode(items[i], element, old != null && i < old.Length ? old.GetValue(i) : null, refs, create), i);
                return array;
            }
            if (IsSet(type, out Type setElement))
            {
                object collection = Activator.CreateInstance(type);
                MethodInfo add = type.GetMethod("Add");
                foreach (JsonNode item in (JsonArray)node) add.Invoke(collection, new[] { FromNode(item, setElement, null, refs, create) });
                return collection;
            }
            if (IsList(type, out Type listElement))
            {
                IList old = current as IList;
                IList list = (IList)Activator.CreateInstance(type);
                JsonArray items = (JsonArray)node;
                for (int i = 0; i < items.Count; i++) list.Add(FromNode(items[i], listElement, old != null && i < old.Count ? old[i] : null, refs, create));
                return list;
            }
            object target = current ?? create?.Invoke(type) ?? Activator.CreateInstance(type);
            Read((JsonObject)node, target, refs, null, create);
            return target;
        }

        private static string SafeName(Func<int, string> name, int id)
        {
            try { return name(id); }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is IndexOutOfRangeException) { return null; }
        }

        /// <summary>BaseHP becomes base_hp, EvSpAtk ev_sp_atk, Type1 type1, Is3D is3d.</summary>
        public static string Key(string name)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0)
                {
                    bool afterLower = char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) && i + 1 < name.Length;
                    bool beforeLower = i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]);
                    if (afterLower || beforeLower) sb.Append('_');
                }
                if (c != '_' || sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().TrimStart('_');
        }
    }
}
