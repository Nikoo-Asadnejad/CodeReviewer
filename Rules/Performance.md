# Performance

- No blocking on async: flag `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on hot paths. Use `await`.
- Avoid multiple enumeration of `IEnumerable` — materialize with `ToList()`/`ToArray()` once when iterated more than once.
- Avoid unnecessary allocations in loops (string concatenation in loops → `StringBuilder`; avoid per-item LINQ closures on hot paths).
- Flag LINQ misuse: `Count() > 0` instead of `Any()`, `Where().First()` instead of `First(predicate)`, repeated `OrderBy`.
- Pass `CancellationToken` through async call chains.
- Avoid synchronous I/O when an async API exists.
