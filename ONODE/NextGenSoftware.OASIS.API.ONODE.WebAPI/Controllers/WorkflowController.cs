using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Interfaces;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Models.Workflow;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Services;
using NextGenSoftware.OASIS.Common;

namespace NextGenSoftware.OASIS.API.ONODE.WebAPI.Controllers
{
    /// <summary>
    /// OASIS CRE — Workflow Runtime API.
    ///
    /// Allows avatars to save, load, list, execute, and verify workflows.
    /// Workflows are persisted as Holons via HyperDrive (auto-replicated across
    /// Web2 + Web3 providers).  Each step execution is metered in platform tokens.
    /// Completed workflows produce a tamper-proof proof holon.
    ///
    /// All mutating endpoints require a valid JWT (Bearer token from /api/avatar/authenticate).
    /// </summary>
    [ApiController]
    [Route("api/workflow")]
    public class WorkflowController : OASISControllerBase
    {
        private readonly IWorkflowEngine _engine;
        private readonly WorkflowProofGenerator _proofGenerator;
        private readonly IWorkflowRunStore _runStore;

        public WorkflowController(IWorkflowEngine engine, WorkflowProofGenerator proofGenerator, IWorkflowRunStore runStore)
        {
            _engine       = engine       ?? throw new ArgumentNullException(nameof(engine));
            _proofGenerator = proofGenerator ?? throw new ArgumentNullException(nameof(proofGenerator));
            _runStore     = runStore     ?? throw new ArgumentNullException(nameof(runStore));
        }

        // ─── Save ───────────────────────────────────────────────────────────

        /// <summary>
        /// Save (or update) a WorkflowDefinition as a Holon via HyperDrive.
        /// Returns the workflow with its assigned Holon ID.
        /// </summary>
        [Authorize]
        [HttpPost("save")]
        [ProducesResponseType(typeof(OASISResult<WorkflowDefinition>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<OASISResult<WorkflowDefinition>>> Save([FromBody] SaveWorkflowRequest request)
        {
            if (request?.Workflow == null)
                return BadRequest(new OASISResult<WorkflowDefinition> { IsError = true, Message = "Workflow is required." });

            if (string.IsNullOrWhiteSpace(request.Workflow.Name))
                return BadRequest(new OASISResult<WorkflowDefinition> { IsError = true, Message = "Workflow.Name is required." });

            if (AvatarId == Guid.Empty)
                return Unauthorized(new OASISResult<WorkflowDefinition> { IsError = true, Message = "Unauthorized: avatar required." });

            var result = await _engine.SaveAsync(request.Workflow, AvatarId);
            return result.IsError ? BadRequest(result) : Ok(result);
        }

        // ─── Load ───────────────────────────────────────────────────────────

        /// <summary>Load a workflow by its Holon ID.</summary>
        [Authorize]
        [HttpGet("{holonId:guid}")]
        [ProducesResponseType(typeof(OASISResult<WorkflowDefinition>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<WorkflowDefinition>>> Load(Guid holonId)
        {
            var result = await _engine.LoadAsync(holonId);
            return result.IsError ? NotFound(result) : Ok(result);
        }

        // ─── List ───────────────────────────────────────────────────────────

        /// <summary>List all workflows owned by the authenticated avatar.</summary>
        [Authorize]
        [HttpGet("my")]
        [ProducesResponseType(typeof(OASISResult<System.Collections.Generic.IEnumerable<WorkflowSummaryDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult> ListMy()
        {
            if (AvatarId == Guid.Empty)
                return Unauthorized(new OASISResult<object> { IsError = true, Message = "Unauthorized: avatar required." });

            var result = await _engine.ListAsync(AvatarId);
            return result.IsError ? BadRequest(result) : Ok(result);
        }

        /// <summary>List all public workflow templates (no auth required).</summary>
        [HttpGet("public")]
        [ProducesResponseType(typeof(OASISResult<System.Collections.Generic.IEnumerable<WorkflowSummaryDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult> ListPublic()
        {
            var result = await _engine.ListPublicAsync();
            return result.IsError ? BadRequest(result) : Ok(result);
        }

        // ─── Delete ─────────────────────────────────────────────────────────

        /// <summary>Delete a workflow holon (owner only).</summary>
        [Authorize]
        [HttpDelete("{holonId:guid}")]
        [ProducesResponseType(typeof(OASISResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<bool>>> Delete(Guid holonId)
        {
            if (AvatarId == Guid.Empty)
                return Unauthorized(new OASISResult<bool> { IsError = true, Message = "Unauthorized: avatar required." });

            var result = await _engine.DeleteAsync(holonId, AvatarId);
            return result.IsError ? NotFound(result) : Ok(result);
        }

        // ─── Execute ────────────────────────────────────────────────────────

        /// <summary>
        /// Execute a workflow. Accepts either an inline WorkflowDefinition or a
        /// previously-saved WorkflowHolonId (or both — inline takes precedence).
        ///
        /// Each step is metered in platform tokens against the calling avatar.
        /// On success, a tamper-proof proof holon is written to HyperDrive and
        /// its ID is returned in ProofHolonId.
        /// </summary>
        [Authorize]
        [HttpPost("execute")]
        [ProducesResponseType(typeof(OASISResult<WorkflowExecutionResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<OASISResult<WorkflowExecutionResult>>> Execute([FromBody] Models.Workflow.ExecuteWorkflowRequest request)
        {
            if (request == null)
                return BadRequest(new OASISResult<WorkflowExecutionResult> { IsError = true, Message = "Request body is required." });

            if (AvatarId == Guid.Empty)
                return Unauthorized(new OASISResult<WorkflowExecutionResult> { IsError = true, Message = "Unauthorized: avatar required." });

            OASISResult<WorkflowExecutionResult> result;

            if (request.Workflow != null)
            {
                result = await _engine.ExecuteAsync(request.Workflow, AvatarId, request.TriggerInputs);
            }
            else if (request.WorkflowHolonId.HasValue && request.WorkflowHolonId.Value != Guid.Empty)
            {
                result = await _engine.ExecuteByHolonIdAsync(request.WorkflowHolonId.Value, AvatarId, request.TriggerInputs);
            }
            else
            {
                return BadRequest(new OASISResult<WorkflowExecutionResult>
                {
                    IsError = true,
                    Message = "Either Workflow (inline definition) or WorkflowHolonId must be provided."
                });
            }

            return result.IsError ? BadRequest(result) : Ok(result);
        }

        // ─── Proof verification ─────────────────────────────────────────────

        /// <summary>
        /// Verify that a proof holon is intact and has not been tampered with.
        /// Returns { isValid: true } if the SHA-256 hash matches.
        /// </summary>
        [HttpGet("proof/{proofHolonId:guid}/verify")]
        [ProducesResponseType(typeof(OASISResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<bool>>> VerifyProof(Guid proofHolonId)
        {
            var result = await _proofGenerator.VerifyProofAsync(proofHolonId);
            return result.IsError ? NotFound(result) : Ok(result);
        }

        // ─── Run management (STAR Watch) ─────────────────────────────────────
        //
        // These endpoints power the star-watch CLI daemon.  A "run" is one live
        // execution of an SOP — it tracks per-step status, assignees, and
        // evidence so that star-watch can match incoming events to pending steps.

        /// <summary>
        /// Start a new live run from a saved WorkflowDefinition (or an inline one).
        /// Returns the full run with all steps in Pending status.
        /// </summary>
        [Authorize]
        [HttpPost("runs/start")]
        [ProducesResponseType(typeof(OASISResult<StartRunResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OASISResult<StartRunResponse>>> StartRun([FromBody] StartRunRequest request)
        {
            if (request == null)
                return BadRequest(new OASISResult<StartRunResponse> { IsError = true, Message = "Request body is required." });

            WorkflowDefinition workflow = request.Workflow;

            if (workflow == null && request.WorkflowHolonId.HasValue)
            {
                var loadResult = await _engine.LoadAsync(request.WorkflowHolonId.Value);
                if (loadResult.IsError || loadResult.Result == null)
                    return NotFound(new OASISResult<StartRunResponse> { IsError = true, Message = $"Workflow {request.WorkflowHolonId} not found." });
                workflow = loadResult.Result;
            }

            if (workflow == null)
                return BadRequest(new OASISResult<StartRunResponse> { IsError = true, Message = "Either WorkflowHolonId or an inline Workflow is required." });

            var steps = workflow.Steps.Select((s, i) => new RunStepDto
            {
                StepId            = s.Id,
                Name              = s.Name,
                Connector         = s.Connector,
                Status            = i == 0 ? StepStatus.Running : StepStatus.Pending,
                RequiresSignOff   = s.Inputs.TryGetValue("requiresSignOff", out var rsv) && rsv?.ToString() == "true",
                RequiresEvidence  = s.Inputs.TryGetValue("requiresEvidence", out var rev) && rev?.ToString() == "true",
            }).ToList();

            var run = new WorkflowRun
            {
                WorkflowHolonId = request.WorkflowHolonId ?? workflow.Id,
                WorkflowId      = workflow.Id?.ToString() ?? "inline",
                SopName         = workflow.Name,
                OrgId           = request.OrgId,
                AvatarId        = AvatarId,
                Steps           = steps,
            };

            var created = await _runStore.CreateAsync(run);

            var response = new OASISResult<StartRunResponse>
            {
                Result = new StartRunResponse
                {
                    ExecutionId = created.ExecutionId,
                    WorkflowId  = created.WorkflowId,
                    SopName     = created.SopName,
                    Status      = created.Status,
                    Steps       = created.Steps,
                    StartedAt   = created.StartedAt,
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// List all active (and recently completed) runs.
        /// Optionally scope to an org with ?orgId=...
        /// </summary>
        [Authorize]
        [HttpGet("runs")]
        [ProducesResponseType(typeof(OASISResult<IEnumerable<ActiveRunSummaryDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetRuns([FromQuery] string orgId = null)
        {
            var runs = await _runStore.GetActiveAsync(orgId);

            var summaries = runs.Select(r => new ActiveRunSummaryDto
            {
                ExecutionId      = r.ExecutionId,
                WorkflowId       = r.WorkflowId,
                SopName          = r.SopName,
                OrgId            = r.OrgId,
                Status           = r.Status,
                CurrentStepIndex = r.CurrentStepIndex,
                TotalSteps       = r.Steps.Count,
                CurrentStepName  = r.Steps.ElementAtOrDefault(r.CurrentStepIndex)?.Name,
                StartedAt        = r.StartedAt,
                Steps            = r.Steps,
                Assignees        = r.Assignees,
            });

            return Ok(new OASISResult<IEnumerable<ActiveRunSummaryDto>> { Result = summaries });
        }

        /// <summary>
        /// Get the current state of a specific execution (for UI polling and star-watch sync).
        /// </summary>
        [Authorize]
        [HttpGet("execution/{executionId}")]
        [ProducesResponseType(typeof(OASISResult<GetExecutionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<GetExecutionResponse>>> GetExecution(string executionId)
        {
            var run = await _runStore.GetAsync(executionId);
            if (run == null)
                return NotFound(new OASISResult<GetExecutionResponse> { IsError = true, Message = $"Execution {executionId} not found." });

            return Ok(new OASISResult<GetExecutionResponse>
            {
                Result = new GetExecutionResponse
                {
                    ExecutionId  = run.ExecutionId,
                    WorkflowId   = run.WorkflowId,
                    Status       = run.Status,
                    CurrentStep  = run.CurrentStepIndex,
                    Steps        = run.Steps,
                    StartedAt    = run.StartedAt,
                    CompletedAt  = run.CompletedAt,
                    ProofHolonId = run.ProofHolonId,
                }
            });
        }

        /// <summary>
        /// Mark a step as complete (called by the SOP Runner UI or star-watch auto-complete).
        /// Advances the run cursor to the next pending step.
        /// </summary>
        [Authorize]
        [HttpPost("execution/{executionId}/steps/{stepId}/complete")]
        [ProducesResponseType(typeof(OASISResult<GetExecutionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<GetExecutionResponse>>> CompleteStep(
            string executionId,
            string stepId,
            [FromBody] CompleteStepRequest request)
        {
            var run = await _runStore.CompleteStepAsync(executionId, stepId, request ?? new CompleteStepRequest());
            if (run == null)
                return NotFound(new OASISResult<GetExecutionResponse> { IsError = true, Message = $"Execution {executionId} not found." });

            return Ok(new OASISResult<GetExecutionResponse>
            {
                Result = new GetExecutionResponse
                {
                    ExecutionId  = run.ExecutionId,
                    WorkflowId   = run.WorkflowId,
                    Status       = run.Status,
                    CurrentStep  = run.CurrentStepIndex,
                    Steps        = run.Steps,
                    StartedAt    = run.StartedAt,
                    CompletedAt  = run.CompletedAt,
                    ProofHolonId = run.ProofHolonId,
                }
            });
        }

        /// <summary>
        /// Record a human sign-off against a step.  Called by star-watch when a
        /// user clicks "Sign Off" in Slack (after star-watch has written the proof
        /// holon to STARNET).
        /// </summary>
        [Authorize]
        [HttpPost("runs/{runId}/steps/{stepId}/signoff")]
        [ProducesResponseType(typeof(OASISResult<SignOffStepResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OASISResult<SignOffStepResponse>>> SignOffStep(
            string runId,
            string stepId,
            [FromBody] SignOffStepRequest request)
        {
            if (request == null)
                return BadRequest(new OASISResult<SignOffStepResponse> { IsError = true, Message = "Request body is required." });

            var run = await _runStore.SignOffStepAsync(runId, stepId, request);
            if (run == null)
                return NotFound(new OASISResult<SignOffStepResponse> { IsError = true, Message = $"Run {runId} not found." });

            return Ok(new OASISResult<SignOffStepResponse>
            {
                Result = new SignOffStepResponse
                {
                    ExecutionId  = run.ExecutionId,
                    StepId       = stepId,
                    SignedBy     = request.AvatarId ?? "unknown",
                    SignedAt     = DateTime.UtcNow,
                    ProofHolonId = run.ProofHolonId,
                }
            });
        }
    }
}
