using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PerfHammer {
    interface IShaderMapping {
        Dictionary<string, Func<Material, (Color, AtlasBlendMode)>> Mapping { get; }
    }

    public enum AtlasBlendMode
    {
        Multiply = 0,
        MultiplyNegative = 1,
    }

    public static class ShaderMapper
    {
        public static Dictionary<string, Func<Material, (Color, AtlasBlendMode)>> Mapping { 
            get {
                if (_mapping == null) {
                    _mapping = new Dictionary<string, Func<Material, (Color, AtlasBlendMode)>>();
                    var mapBuilder = _mapping.AsEnumerable();
                    foreach (var t in GetAllMappingTypes()) {
                        IShaderMapping m = (IShaderMapping)Activator.CreateInstance(t);
                        mapBuilder = mapBuilder.Concat(m.Mapping);
                    }
                    _mapping = mapBuilder.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                return _mapping;
            } 
        }

        static Dictionary<string, Func<Material, (Color, AtlasBlendMode)>> _mapping = null;

        public static IEnumerable<Type> GetAllMappingTypes()
            => Assembly.GetExecutingAssembly()
                .DefinedTypes
                .Where(t => t.ImplementedInterfaces.Any(i => i == typeof(IShaderMapping)));
    }
}
