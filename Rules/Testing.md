# Testing

- New or changed public behavior should have unit tests; flag untested public methods with non-trivial logic.
- Cover edge cases: nulls, empty collections, boundary values, error/exception paths.
- Tests should be deterministic and isolated — no real network, clock, or filesystem dependencies (use mocks/fakes).
- Assess regression risk: does the change alter existing behavior without a test pinning it?
- One logical assertion per test; arrange-act-assert structure; descriptive test names.
