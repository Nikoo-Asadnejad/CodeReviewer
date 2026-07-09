# Clean Code

- Readability first: clear control flow, early returns over deep nesting.
- No code duplication — extract shared logic.
- Methods should be short and single-purpose; flag long methods doing several things.
- No dead code, commented-out blocks, or unused usings/variables/parameters.
- No magic numbers or strings — name them as constants.
- Keep nullability honest; flag `!` null-forgiving operators that hide real null risks.
- Prefer expression-bodied members and modern C# where it improves clarity, not where it hurts it.
- Use primary constructor with no fields.
- define classes internal when possible.
- define classes as sealed when no other class would inherit them.
- wrap conditions in meaningful method or variable.
- no method should have more than 5 parameter.
- Classes should be short and single-purpose avoid long classes which handles every thing always  follow SRP.
