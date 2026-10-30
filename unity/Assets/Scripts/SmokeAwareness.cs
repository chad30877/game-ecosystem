using System;
using UnityEngine;

namespace Ecosystem
{
    [Serializable]
    public sealed class SmokeSighting
    {
        public int area;
        public float x, y, radius, lastSeenAt;
        public bool harmful, visible;
    }
}
