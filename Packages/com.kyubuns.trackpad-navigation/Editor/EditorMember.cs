using System;
using System.Collections.Generic;
using System.Reflection;

namespace TrackpadNavigation
{
    internal static class EditorMember
    {
        // Reflectionは既知のビュー状態に限定し、編集データには使わない。
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly Dictionary<(Type, string), MemberInfo> Members = new();

        static MemberInfo Find(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }
            var key = (owner.GetType(), name);
            if (Members.TryGetValue(key, out var member))
            {
                return member;
            }
            // Unityの内部実装が基底型へ移動しても、最も派生した定義を優先する。
            for (var type = key.Item1; type != null; type = type.BaseType)
            {
                member = (MemberInfo)type.GetProperty(name, Flags) ?? type.GetField(name, Flags);
                if (member != null)
                {
                    break;
                }
            }
            Members.Add(key, member);
            return member;
        }

        public static object Get(object owner, string name)
        {
            switch (Find(owner, name))
            {
                case PropertyInfo property:
                    return property.GetValue(owner);
                case FieldInfo field:
                    return field.GetValue(owner);
                default:
                    return null;
            }
        }

        public static bool Writable(object owner, string name, Type valueType)
        {
            var property = Find(owner, name) as PropertyInfo;
            return property != null && property.PropertyType == valueType && property.CanRead && property.CanWrite;
        }

        public static MethodInfo Method(object owner, string name, params Type[] arguments)
        {
            for (var type = owner?.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, Flags, null, arguments, null);
                if (method != null)
                {
                    return method;
                }
            }
            return null;
        }

        public static void Set(object owner, string name, object value)
        {
            if (!(Find(owner, name) is PropertyInfo property) || !property.CanWrite)
            {
                throw new MissingMemberException(owner?.GetType().FullName, name);
            }
            property.SetValue(owner, value);
        }
    }
}
