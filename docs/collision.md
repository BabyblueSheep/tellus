# [Collision](../src/Tellus/Collision)
Tellus comes with a GPU-accelerated collision detection and resolution system. This file contains an overview of the system and an example of using it.

> [!NOTE]
> This file only provides an overview of the system. More detailed information can be viewed in either the source code (the files for which are linked here) or XML documentation.

## Bodies
Objects that should have collision implement [`ICollisionBody`](../src/Tellus/Collision/CollisionBodyParts.cs). Bodies are composed of a list of body parts and a position offset.

Body parts can be represented by the following shapes:
- polygons that approximate circles (`CollisionBodyPart.CreateCircle()`);
- arbitrarily rotated rectangles (`CollisionBodyPart.CreateRectangle()`);
- triangles (`CollisionBodyPart.CreateTriangle()`).

## Lines
Objects that should hold lines implement [`ICollisionLineCollection`](../src/Tellus/Collision/CollisionLines.cs). Line collections are composed of a list of lines, a position offset and an index for the "velocity" line (niche; more on this below).

Line collections can hold the following lines:
- rays with a normalized direction and a finite length (`CollisionLine.CreateFiniteLengthRay()`);
- lines with one fixed position point in world space (`CollisionLine.CreateFixedPointLineSegment()`);

## Buffers
With the collision system being GPU-accelerated, uploading to the GPU is required. But, instead of directly using buffers, several "storage buffer bundle" classes are provided to simplify uploading data. The bundles contain functions for uploading, downloading or clearing data, and mapping buffer indices to objects. Buffers also store "segments" to allow 

The following bundles are provided:
- [`BodyStorageBufferBundle`](../src/Tellus/Collision/CollisionHandler.BodyStorageBufferBundle.cs) for storing bodies and body parts;
- [`LineCollectionStorageBufferBundle`](../src/Tellus/Collision/CollisionHandler.LineCollectionStorageBufferBundle.cs) for storing line collections and lines;
- [`HitResultStorageBufferBundle`](../src/Tellus/Collision/CollisionHandler.HitResultStorageBufferBundle.cs) for storing hit results of body-body and body-line collisions;
- [`ResolutionResultStorageBufferBundle`](../src/Tellus/Collision/CollisionHandler.ResolutionResultStorageBufferBundle.cs) for storing resolution vectors of body-body collisions;
- [`BodyLineCollectionPairStorageBufferBundle`](../src/Tellus/Collision/CollisionHandler.BodyLineCollectionPairStorageBufferBundle.cs) for storing pairs of body and line collection indicies (niche);

> [!WARNING]
> Be mindful about storing more objects in buffers than can fit. Besides specifying the amount of objects that can be stored, there is no protection against storing too many objects.

## [CollisionHandler](../src/Tellus/Collision/CollisionHandler.cs)
`CollisionHandler` is the core of the collision system, and is where all of the collision functions are located.

The class contains the following functions:
- `ComputeBodyBodyHits()`;
- `ResolveBodyBodyCollisions()`;
- `ComputeLineBodyHits()`;
- `RestrictLines()` - used to restrict lines so that they don't overlap with any bodies;
- `IncrementLineCollectionOffsets()` - increments line collections with the "velocity" line;
- `IncrementLineCollectionBodiesOffsets()` - increments line collections and bodies with the "velocity" line given a `BodyLineCollectionPairStorageBufferBundle`;

## Example
