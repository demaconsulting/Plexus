## IconCache Unit Verification Design

Scope: Cache of rasterized SVG icons keyed by icon and size.

### Verification Approach

Unit tests in `IconCacheTests.cs` call the cache directly with small SVG documents. Because the cache is process-wide and
tests run in parallel, each test creates icons that are unique to it (a GUID inside an SVG comment gives a distinct
icon key), so tests cannot interfere with each other's entries. The real CanvasNet SVG codec is used; no mocks are needed.
The tests of one class run sequentially, so the bounded-memory and concurrency tests never overlap.

### Test Environment

No setup beyond the standard test runner.

### Acceptance Criteria

A test passes when repeated requests return the same surface instance, other sizes return separate surfaces of the
requested size, malformed SVG returns null, and the entry count stays below the number of distinct requests.

### Test Scenarios

- **IconCache_Get_SameKeyAndSize_ReturnsSharedSurface**: two requests of the same icon and size share one surface and
  another size gets its own surface of that size.
- **IconCache_Get_MalformedSvg_ReturnsNull**: malformed SVG returns null twice without throwing.
- **IconCache_Get_ManyDistinctIcons_KeepsCacheBounded**: seven hundred distinct icons leave fewer entries in the cache than
  were requested.
- **IconCache_Get_ConcurrentRequests_ReturnTheSameSurface**: thirty-two parallel requests for one icon and size all
  return the same surface.
