using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;

namespace PerfHammer.Utils
{
    public static class MaterialUtils {
        public static Shader GetCommonShader(IEnumerable<Material> materials) {
            Dictionary<string, int> shaders = new Dictionary<string, int>();

            foreach (var m in materials) {
                var s = m.shader;
                var name = s.name;

                if (name.StartsWith("Hidden/Locked/")) {
                    name = Regex.Match(name, @"^Hidden\/Locked\/(.*)\/.*$").Groups[1].Value;
                }

                if (!shaders.ContainsKey(name)) {
                    shaders.Add(name, 1);
                } else {
                    shaders[name]++;
                }
            }

            return Shader.Find(shaders.OrderByDescending(x => x.Value).First().Key);
        }

        public static string[] IGNORED_TEX_PROPERTIES = {
            "ramp",
            "cube",
            "noise",
            "curve",
            "distortion",
            "detail",
            "decal",
            "lut",
            "matcap",
            "fallback",
        };

        public static HashSet<string> GetCommonTextureProperties(IEnumerable<Material> materials) {
            var result = new HashSet<string>();
            foreach (var m in materials) {
                foreach (var p in m.GetTexturePropertyNames()) {
                    var pLower = p.ToLower();
                    if (IGNORED_TEX_PROPERTIES.Any(i => pLower.Contains(i)))
                        continue;
                    result.Add(p);
                }
            }
            return result;
        }
    }
}
