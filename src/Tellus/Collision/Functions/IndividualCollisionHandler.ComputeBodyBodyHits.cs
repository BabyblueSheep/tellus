using MoonWorks.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;
using static MoonWorks.Graphics.VertexStructs;

namespace Tellus.Collision.Individual;

public static partial class IndividualCollisionHandler
{
    public static bool ComputeBodyBodyHits(CollisionBody bodyOne, CollisionBody bodyTwo)
    {
        if (bodyOne.IsWithinNarrowRange(bodyTwo))
        {
            return false;
        }

        static bool DoBodyPartsOverlap(CollisionPolygon bodyPartMain, Vector2 offsetMain, CollisionPolygon bodyPartSub, Vector2 offsetSub)
        {
            foreach (var normal in bodyPartMain.Normals)
            {
                var shapeOneProjection = ProjectVerticesOnAxis(bodyPartMain.Vertices, offsetMain, normal);
                var shapeTwoProjection = ProjectVerticesOnAxis(bodyPartSub.Vertices, offsetSub, normal);

                if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var bodyPartOne in bodyOne)
        {
            foreach (var bodyPartTwo in bodyTwo)
            {
                if (DoBodyPartsOverlap(bodyPartOne, bodyOne.Offset, bodyPartTwo, bodyTwo.Offset))
                    return true;
                if (DoBodyPartsOverlap(bodyPartTwo, bodyTwo.Offset, bodyPartOne, bodyOne.Offset))
                    return true;
            }
        }

        return false;
    }
}
