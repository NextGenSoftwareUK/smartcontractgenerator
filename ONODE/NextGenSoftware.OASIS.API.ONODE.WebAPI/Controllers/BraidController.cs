using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Models.Workflow;
using NextGenSoftware.OASIS.Common;

namespace NextGenSoftware.OASIS.API.ONODE.WebAPI.Controllers
{
    /// <summary>
    /// BRAID — Behavioural Reasoning and AI Decision Engine.
    ///
    /// This controller exposes the BRAID semantic matching API used by star-watch
    /// to map real-world events (Slack messages, Salesforce changes, emails) to
    /// pending SOP steps with a confidence score.
    ///
    /// Current status: keyword-based stub implementation.
    /// TODO: Replace ForwardToBraidAsync with a real call to the BRAID service
    ///       once API access is provisioned.  The request/response contract is
    ///       intentionally stable so the switch is a single method change.
    /// </summary>
    [ApiController]
    [Route("api/braid")]
    public class BraidController : OASISControllerBase
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration     _config;
        private readonly ILogger<BraidController> _logger;

        // When real BRAID access is available, set BRAID__BaseUrl in appsettings.
        private string BraidBaseUrl => _config["BRAID:BaseUrl"];

        public BraidController(IHttpClientFactory http, IConfiguration config, ILogger<BraidController> logger)
        {
            _http   = http;
            _config = config;
            _logger = logger;
        }

        // ─── Match ────────────────────────────────────────────────────────────

        /// <summary>
        /// Given an incoming event and a list of candidate SOP steps, returns the
        /// best-matching step with a confidence score (0–1) and reasoning text.
        ///
        /// Flow:
        ///   1. If BRAID:BaseUrl is configured, forward to the real BRAID service.
        ///   2. Otherwise fall back to keyword matching against TriggerConditions.
        ///
        /// Request:
        ///   {
        ///     "event":      { "source": "slack", "action": "message", "payload": { "text": "..." } },
        ///     "candidates": [ { "id": "step-1", "name": "Send welcome email", "triggerConditions": [...] } ],
        ///     "runContext": { "runId": "...", "sopName": "...", "completedSteps": [] }
        ///   }
        ///
        /// Response:
        ///   { "stepId": "step-1", "confidence": 0.92, "reasoning": "..." }
        /// </summary>
        [Authorize]
        [HttpPost("match")]
        [ProducesResponseType(typeof(OASISResult<BraidMatchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OASISResult<BraidMatchResponse>>> Match([FromBody] BraidMatchRequest request)
        {
            if (request?.Event == null || request.Candidates == null || !request.Candidates.Any())
                return BadRequest(new OASISResult<BraidMatchResponse> { IsError = true, Message = "Event and at least one candidate step are required." });

            // ── 1. Try real BRAID service ──────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(BraidBaseUrl))
            {
                try
                {
                    var braidResult = await ForwardToBraidAsync(request);
                    if (braidResult != null)
                        return Ok(new OASISResult<BraidMatchResponse> { Result = braidResult });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BRAID service unreachable ({Msg}), falling back to keyword matching.", ex.Message);
                }
            }

            // ── 2. Keyword fallback ────────────────────────────────────────────
            var keywordResult = KeywordMatch(request.Event, request.Candidates);
            if (keywordResult != null)
                return Ok(new OASISResult<BraidMatchResponse> { Result = keywordResult });

            return Ok(new OASISResult<BraidMatchResponse>
            {
                Result = new BraidMatchResponse { StepId = null, Confidence = 0, Reasoning = "No match found." }
            });
        }

        // ─── BRAID forwarding (real service — swap in when access is provisioned) ──

        /// <summary>
        /// TODO: Replace stub body with real BRAID API call.
        ///
        /// Expected BRAID endpoint: POST {BraidBaseUrl}/v1/match
        /// Auth:                    Bearer token from BRAID:ApiKey config value.
        ///
        /// The BRAID service should accept exactly the BraidMatchRequest structure
        /// and return { stepId, confidence, reasoning }.
        /// </summary>
        private async Task<BraidMatchResponse> ForwardToBraidAsync(BraidMatchRequest request)
        {
            var client   = _http.CreateClient();
            var apiKey   = _config["BRAID:ApiKey"] ?? "";
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var json    = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BraidBaseUrl}/v1/match", content);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BraidMatchResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // ─── Keyword matching fallback ────────────────────────────────────────

        /// <summary>
        /// Rule-based keyword matcher used when the real BRAID service is unavailable.
        ///
        /// Scoring:
        ///   - Each trigger condition that matches a word in the event payload adds 0.3.
        ///   - Exact phrase match scores 0.85.
        ///   - Capped at 0.92 to distinguish from a real semantic match (1.0).
        ///
        /// Replace this entirely once BRAID is live — it's intentionally simple.
        /// </summary>
        private static BraidMatchResponse KeywordMatch(BraidMatchEvent ev, List<BraidMatchCandidate> candidates)
        {
            // Flatten the event payload into a single searchable string
            var eventText = string.Join(" ", GetPayloadStrings(ev.Payload)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(eventText)) return null;

            BraidMatchCandidate bestCandidate = null;
            double bestScore = 0;
            var bestReason   = "";

            foreach (var candidate in candidates)
            {
                if (candidate.TriggerConditions == null || !candidate.TriggerConditions.Any())
                    continue;

                double score    = 0;
                var matchedTerms = new List<string>();

                foreach (var condition in candidate.TriggerConditions)
                {
                    if (string.IsNullOrWhiteSpace(condition.Value)) continue;

                    var term = condition.Value.ToLowerInvariant();

                    if (eventText.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        // Phrase match — check if the whole term appears
                        score += 0.35;
                        matchedTerms.Add(term);
                    }
                    else
                    {
                        // Word-by-word partial match
                        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var matched = words.Count(w => eventText.Contains(w, StringComparison.OrdinalIgnoreCase));
                        score += 0.15 * matched / Math.Max(1, words.Length);
                    }
                }

                // Normalise score against number of conditions
                score = Math.Min(0.92, score / candidate.TriggerConditions.Count * candidate.TriggerConditions.Count);

                if (score > bestScore)
                {
                    bestScore     = score;
                    bestCandidate = candidate;
                    bestReason    = matchedTerms.Any()
                        ? $"Keyword match on: {string.Join(", ", matchedTerms)}"
                        : "Partial keyword match (BRAID unavailable)";
                }
            }

            if (bestCandidate == null || bestScore < 0.3)
                return null;

            return new BraidMatchResponse
            {
                StepId     = bestCandidate.Id,
                Confidence = Math.Round(bestScore, 2),
                Reasoning  = bestReason,
            };
        }

        private static IEnumerable<string> GetPayloadStrings(Dictionary<string, object> payload)
        {
            if (payload == null) yield break;
            foreach (var v in payload.Values)
            {
                if (v is string s) yield return s;
                else if (v is JsonElement je && je.ValueKind == JsonValueKind.String) yield return je.GetString() ?? "";
            }
        }
    }
}
