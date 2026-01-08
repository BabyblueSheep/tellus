using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;

namespace Tellus.Collision;

public sealed class CollisionLine
{
    public Vector2 Start { get; set; }
    public Vector2 End { get; set; }

    public bool CanBeRestrcited { get; set; }
}
