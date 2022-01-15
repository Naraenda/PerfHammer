using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PerfHammer
{
    public interface IModule
    {
        string Name { get; }

        GameObject Run(Exporter e, GameObject obj);
    }
}

