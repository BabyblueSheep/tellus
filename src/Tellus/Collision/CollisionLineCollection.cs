using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;

namespace Tellus.Collision;

public sealed class CollisionLineCollection : IEnumerable<CollisionLine>
{
    public Vector2 Offset { get; }

    private readonly List<CollisionLine> _lines;

    public CollisionLineCollection()
    {
        _lines = [];
    }

    public CollisionLineCollection(params CollisionLine[] lines) : this()
    {

        foreach (var line in lines)
        {
            Add(line);
        }
    }

    public void Add(CollisionLine line)
    {
        _lines.Add(line);
    }

    public void Clear()
    {
        _lines.Clear();
    }

    public IEnumerator<CollisionLine> GetEnumerator()
    {
        return _lines.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}