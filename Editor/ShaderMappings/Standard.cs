using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PerfHammer.ShaderMappings
{
    class StandardMapping : IShaderMapping
    {
        public Dictionary<string, Func<Material, (Color, AtlasBlendMode)>> Mapping 
            => new Dictionary<string, Func<Material, (Color, AtlasBlendMode)>>() {
                { "_MainTex", m => (
                    m.GetColor("_Color"), 
                    AtlasBlendMode.Multiply
                )} ,
                { "_OcclusionMap", m => (
                    Color.white * m.GetFloat("_OcclusionStrength"), 
                    AtlasBlendMode.MultiplyNegative
                )},
                { "_EmissionMap", m => (
                    m.GetColor("_EmissionColor"),
                    AtlasBlendMode.Multiply
                )},
            };
    }
}
