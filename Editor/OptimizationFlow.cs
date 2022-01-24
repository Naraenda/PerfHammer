using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PerfHammer
{
    public class OptimizationFlow
    {
        public readonly LinkedList<IModule> Modules;

        public T Get<T>()
            => (T)Modules.First(m => m is T);

        public OptimizationFlow() {
            Modules = new LinkedList<IModule>(new IModule[] { 
                new Duplicator(),
                new Combiner(),
                new Atlasser(),
                new BlendShapeCleaner(),
                new Decimator(),
            });
        }

        public GameObject Optimize(GameObject obj, Exporter exporter) {
            var result = obj;
            foreach (var module in Modules) {
                Debug.Log($"### Module {module.Name}");
                result = module.Run(exporter, result, obj);
            }
            Debug.Log($"### Module {exporter.Name}");
            exporter.ExportMeshes(result);

            result.name = $"{obj.name} (Optimized)";
            return result;
        }
    }

}
