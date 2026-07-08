namespace ReviewerAgent.LlmConnector.Contracts;

/// <summary>
/// A single completion request to an LLM provider. The model and token limits are the
/// connector's own configuration concern, so they are intentionally not part of the request.
/// </summary>
public sealed record LlmRequest(string System, string User);

/// <summary>The text completion returned by an LLM provider.</summary>
public sealed record LlmResponse(string Text);
