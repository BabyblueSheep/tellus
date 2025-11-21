# [Collision](../src/Tellus/Collision)
Tellus comes with a GPU-accelerated collision detection and resolution system.

The following file contains an overview of the system and an example of using it.

## Bodies
Objects that should have collision implement [`ICollisionBody`](../src/Tellus/Collision/CollisionBodyParts.cs). Bodies are composed of a list of body parts and a position offset.

Body parts can be represented by the following shapes:
- polygons that approximate circles (`CollisionBodyPart.CreateCircle()`);
- arbitrarily rotated rectangles (`CollisionBodyPart.CreateRectangle()`);
- triangles (`CollisionBodyPart.CreateTriangle()`).

## Lines
Objects that should hold lines implement [`ICollisionLineCollection`](../src/Tellus/Collision/CollisionLines.cs). Line collections are composed of a list of lines, a position offset and an index for the "velocity" line (more on this below).

Line collections can hold the following lines:
- rays with a normalized direction and a finite length (`CollisionLine.CreateFiniteLengthRay()`);
- lines with one fixed position point in world space (`CollisionLine.CreateFixedPointLineSegment()`);
