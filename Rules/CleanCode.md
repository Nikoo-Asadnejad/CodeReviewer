# Clean Code

- Readability first: clear control flow, early returns over deep nesting.
- No code duplication — extract shared logic.
- Methods should be short and single-purpose; flag long methods doing several things.
- No dead code, commented-out blocks, or unused usings/variables/parameters.
- No magic numbers or strings — name them as constants.
- Keep nullability honest; flag `!` null-forgiving operators that hide real null risks.
- Prefer expression-bodied members and modern C# where it improves clarity, not where it hurts it.
