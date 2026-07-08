# Security

- Never log secrets (PATs, API keys, tokens, connection strings). Flag any log statement that could leak one.
- SQL must be parameterized — flag string-concatenated queries (SQL injection).
- Validate and encode untrusted input; flag reflected/stored values rendered without encoding (XSS).
- No hardcoded credentials or secrets in source or config committed to the repo.
- Verify authentication and authorization checks are present on protected operations.
- Avoid unsafe deserialization of untrusted data (e.g. `BinaryFormatter`, unrestricted type binders).
- Do not weaken TLS/certificate validation.
