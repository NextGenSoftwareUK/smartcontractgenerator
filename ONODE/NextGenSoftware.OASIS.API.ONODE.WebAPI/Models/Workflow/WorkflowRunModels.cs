using System;
using System.Collections.Generic;

namespace NextGenSoftware.OASIS.API.ONODE.WebAPI.Models.Workflow
{
    // ─── Run state ────────────────────────────────────────────────────────────

    public enum RunStatus { Running, Completed, Failed, Paused }

    public class RunStepDto
    {
        public string StepId      { get; set; }
        public string Name        { get; set; }
        public string Connector   { get; set; }
        public StepStatus Status  { get; set; } = StepStatus.Pending;
        public string Output      { get; set; }
        public string Error       { get; set; }
        public long   DurationMs  { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; }
        public bool   RequiresSignOff { get; set; }
        public bool   RequiresEvidence { get; set; }
    }

    /// <summary>
    /// Represents one live execution of a WorkflowDefinition.
    /// Stored in WorkflowRunStore (in-memory, later backed by holons).
    /// </summary>
    public class WorkflowRun
    {
        public string       ExecutionId       { get; set; } = Guid.NewGuid().ToString();
        public Guid?        WorkflowHolonId   { get; set; }
        public string       WorkflowId        { get; set; }
        public string       SopName           { get; set; }
        public string       OrgId             { get; set; }
        public Guid         AvatarId          { get; set; }
        public RunStatus    Status            { get; set; } = RunStatus.Running;
        public int          CurrentStepIndex  { get; set; } = 0;
        public List<RunStepDto> Steps         { get; set; } = new();
        public Dictionary<string, string> Assignees { get; set; } = new();
        public DateTime     StartedAt         { get; set; } = DateTime.UtcNow;
        public DateTime?    CompletedAt       { get; set; }
        public Guid?        ProofHolonId      { get; set; }

        // Evidence collected during the run keyed by stepId
        public Dictionary<string, List<string>> Evidence { get; set; } = new();
    }

    // ─── Request / response DTOs ─────────────────────────────────────────────

    public class StartRunRequest
    {
        /// <summary>Previously-saved workflow by holon ID.</summary>
        public Guid? WorkflowHolonId { get; set; }

        /// <summary>Inline workflow — for runs started without a saved definition.</summary>
        public WorkflowDefinition Workflow { get; set; }

        /// <summary>
        /// External context ID linking this run to a record in another system
        /// (e.g. Salesforce Opportunity ID). Used to route events to the right run.
        /// </summary>
        public string ContextId { get; set; }

        /// <summary>Org ID — used to scope active run queries.</summary>
        public string OrgId { get; set; }

        /// <summary>Trigger inputs passed to the first step.</summary>
        public Dictionary<string, object> Inputs { get; set; } = new();
    }

    public class StartRunResponse
    {
        public string   ExecutionId { get; set; }
        public string   WorkflowId  { get; set; }
        public string   SopName     { get; set; }
        public RunStatus Status     { get; set; }
        public List<RunStepDto> Steps { get; set; }
        public DateTime StartedAt   { get; set; }
    }

    public class GetExecutionResponse
    {
        public string   ExecutionId  { get; set; }
        public string   WorkflowId   { get; set; }
        public RunStatus Status      { get; set; }
        public int      CurrentStep  { get; set; }
        public List<RunStepDto> Steps { get; set; }
        public DateTime StartedAt    { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid?    ProofHolonId { get; set; }
    }

    public class CompleteStepRequest
    {
        public string   CompletedBy    { get; set; }
        public string   Note           { get; set; }
        public List<string> Evidence   { get; set; } = new();
        public bool     AutoCompleted  { get; set; } = false;
        public double   Confidence     { get; set; } = 1.0;
    }

    public class SignOffStepRequest
    {
        public string AvatarId  { get; set; }
        public string Channel   { get; set; }
        public string Note      { get; set; }
        public string Signature { get; set; }
    }

    public class SignOffStepResponse
    {
        public string ExecutionId { get; set; }
        public string StepId      { get; set; }
        public string SignedBy    { get; set; }
        public DateTime SignedAt  { get; set; }
        public Guid?  ProofHolonId { get; set; }
    }

    public class ActiveRunSummaryDto
    {
        public string   ExecutionId     { get; set; }
        public string   WorkflowId      { get; set; }
        public string   SopName         { get; set; }
        public string   OrgId           { get; set; }
        public RunStatus Status          { get; set; }
        public int      CurrentStepIndex { get; set; }
        public int      TotalSteps       { get; set; }
        public string   CurrentStepName  { get; set; }
        public DateTime StartedAt        { get; set; }
        public List<RunStepDto> Steps    { get; set; }
        public Dictionary<string, string> Assignees { get; set; }
    }

    // ─── BRAID match DTOs ─────────────────────────────────────────────────────

    public class BraidMatchEvent
    {
        public string Source  { get; set; }
        public string Action  { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
        public List<string> Context { get; set; } = new();
    }

    public class BraidMatchCandidate
    {
        public string Id          { get; set; }
        public string Name        { get; set; }
        public string Connector   { get; set; }
        public string Description { get; set; }
        public List<BraidTriggerCondition> TriggerConditions { get; set; } = new();
    }

    public class BraidTriggerCondition
    {
        public string Field    { get; set; }
        public string Operator { get; set; }
        public string Value    { get; set; }
        public string Value2   { get; set; }
    }

    public class BraidMatchRunContext
    {
        public string RunId    { get; set; }
        public string SopName  { get; set; }
        public List<string> CompletedSteps { get; set; } = new();
    }

    public class BraidMatchRequest
    {
        public BraidMatchEvent      Event      { get; set; }
        public List<BraidMatchCandidate> Candidates { get; set; } = new();
        public BraidMatchRunContext  RunContext { get; set; }
    }

    public class BraidMatchResponse
    {
        public string StepId     { get; set; }
        public double Confidence { get; set; }
        public string Reasoning  { get; set; }
    }
}
