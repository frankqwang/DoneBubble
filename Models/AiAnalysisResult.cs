namespace DoneBubble.Models;
public sealed record AiAnalysisResult(string Prompt, string RawResponse, ActivityCandidate? Candidate, string? Error);
