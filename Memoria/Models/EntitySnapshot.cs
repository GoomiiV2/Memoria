using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.Models
{
    internal class EntitySnapshot
    {
        public long EntityId;
        public uint Hp;
        public uint Mp;
        public Vector3 WorldPos;
        public Vector2 ScreenTopLeft;
        public Vector2 ScreenBottomRight;
    }
}
