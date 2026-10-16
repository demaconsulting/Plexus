## IconCache Unit Verification Design

Scope: Cache of rasterized SVG icons keyed by icon and size.

### Verification Approach

Unit tests in `IconCacheTests.cs` call the cache directly with small SVG documents. Each test creates its own private
`IconCache` instance (a small capacity where the capacity is the subject, the default capacity otherwise), so that
parallel tests of other classes, which paint through the shared instance, cannot add, evict or count entries; the shared
instance is only checked for its capacity. Icons are unique to a test (a GUID inside an SVG comment gives a distinct
icon key). The real CanvasNet SVG codec is used; no mocks are needed.

### Test Environment

No setup beyond the standard test runner.

### Acceptance Criteria

A test passes when repeated requests return the same surface instance, other sizes return separate surfaces of the
requested size, malformed SVG returns null, the entry count never exceeds the capacity after any request, a request for
a new size that finds the cache full leaves only the new entry (so the earlier rasterizations are created anew when
requested again), a size that is already cached does not discard anything from a full cache, and the shared cache has a
capacity of 512.

### Test Scenarios

- **IconCache_Get_SameKeyAndSize_ReturnsSharedSurface**: two requests of the same icon and size share one surface and
  another size gets its own surface of that size.
- **IconCache_Get_MalformedSvg_ReturnsNull**: malformed SVG returns null twice without throwing and occupies one cache
  entry.
- **IconCache_Get_MoreSizesThanCapacity_NeverHoldsMoreThanCapacity**: a cache of four entries asked for twelve distinct
  sizes of one icon holds between one and four entries after every request.
- **IconCache_Get_ManyDistinctIcons_KeepsCacheBounded**: seven hundred distinct icons requested from a cache of the
  default capacity never leave more than 512 entries, and fewer entries than were requested.
- **IconCache_Get_FullCache_DiscardsEarlierRasterizations**: a cache of four entries filled with four sizes holds one
  entry after a fifth size is requested, and the first size, requested again, is a new surface.
- **IconCache_Get_FullCacheCachedSize_KeepsEntries**: a full cache of two entries returns the same surface for an
  already cached size and still holds both entries.
- **IconCache_Shared_HasCapacityOf512**: the shared cache and the default capacity are 512, and a capacity of zero is
  rejected.
- **IconCache_Get_ConcurrentRequests_ReturnTheSameSurface**: thirty-two parallel requests for one icon and size all
  return the same surface.
- **IconCache_Get_ConcurrentMisses_NeverHoldMoreThanCapacity** (`PlexusRendering-IconCache-ConcurrentBound`): sixteen
  threads each request forty distinct sizes of one icon from a cache of capacity four; the largest count observed after
  any request, and the final count, never exceed four.
