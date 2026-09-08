namespace DoneBubble.Models;
using System.Collections.Generic;
public sealed record AiAnalysisResult(string Prompt, string RawResponse, ActivityCandidate? Candidate, string? Error, IReadOnlyList<string>? Images = null);
