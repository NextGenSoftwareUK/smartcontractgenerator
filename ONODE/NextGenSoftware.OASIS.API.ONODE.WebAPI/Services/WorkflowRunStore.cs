using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Interfaces;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Models.Workflow;

namespace NextGenSoftware.OASIS.API.ONODE.WebAPI.Services
{
    /// <summary>
    /// Thread-safe in-memory store for live workflow runs.
    ///
    /// Registered as a singleton. A future iteration will persist each run as a
    /// Holon via HyperDrive so the state survives restarts and is visible on STARNET.
    /// </summary>
    public class WorkflowRunStore : IWorkflowRunStore
    {
        private readonly ConcurrentDictionary<string, WorkflowRun> _runs = new();

        public Task<WorkflowRun> CreateAsync(WorkflowRun run)
        {
            _runs[run.ExecutionId] = run;
            return Task.FromResult(run);
        }

        public Task<WorkflowRun> GetAsync(string executionId)
        {
            _runs.TryGetValue(executionId, out var run);
            return Task.FromResult(run);
        }

        public Task<IEnumerable<WorkflowRun>> GetActiveAsync(string orgId = null)
        {
            var query = _runs.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(orgId))
                query = query.Where(r => r.OrgId == orgId);

            return Task.FromResult(query);
        }

        public Task<WorkflowRun> CompleteStepAsync(string executionId, string stepId, CompleteStepRequest req)
        {
            if (!_runs.TryGetValue(executionId, out var run))
                return Task.FromResult<WorkflowRun>(null);

            lock (run)
            {
                var step = run.Steps.FirstOrDefault(s => s.StepId == stepId);
                if (step != null)
                {
                    step.Status      = StepStatus.Success;
                    step.CompletedAt = DateTime.UtcNow;
                    step.CompletedBy = req.CompletedBy;
                    step.Output      = req.Note;

                    if (req.Evidence?.Count > 0)
                    {
                        if (!run.Evidence.ContainsKey(stepId))
                            run.Evidence[stepId] = new List<string>();
                        run.Evidence[stepId].AddRange(req.Evidence);
                    }
                }

                // Advance the cursor to the next pending step
                var nextIdx = run.Steps.FindIndex(s => s.Status == StepStatus.Pending);
                if (nextIdx >= 0)
                {
                    run.CurrentStepIndex      = nextIdx;
                    run.Steps[nextIdx].Status = StepStatus.Running;
                }
                else
                {
                    // All steps done — mark run complete
                    run.Status      = RunStatus.Completed;
                    run.CompletedAt = DateTime.UtcNow;
                }
            }

            return Task.FromResult(run);
        }

        public Task<WorkflowRun> SignOffStepAsync(string executionId, string stepId, SignOffStepRequest req, Guid? proofHolonId = null)
        {
            if (!_runs.TryGetValue(executionId, out var run))
                return Task.FromResult<WorkflowRun>(null);

            lock (run)
            {
                var step = run.Steps.FirstOrDefault(s => s.StepId == stepId);
                if (step != null)
                {
                    step.Status      = StepStatus.Success;
                    step.CompletedAt = DateTime.UtcNow;
                    step.CompletedBy = req.AvatarId ?? "signed-off";
                    step.Output      = req.Note;
                }

                if (proofHolonId.HasValue)
                    run.ProofHolonId = proofHolonId;

                // Advance cursor if the signed-off step was the current one
                var nextIdx = run.Steps.FindIndex(s => s.Status == StepStatus.Pending);
                if (nextIdx >= 0)
                {
                    run.CurrentStepIndex      = nextIdx;
                    run.Steps[nextIdx].Status = StepStatus.Running;
                }
                else
                {
                    run.Status      = RunStatus.Completed;
                    run.CompletedAt = DateTime.UtcNow;
                }
            }

            return Task.FromResult(run);
        }

        public Task<WorkflowRun> SetRunStatusAsync(string executionId, RunStatus status, Guid? proofHolonId = null)
        {
            if (!_runs.TryGetValue(executionId, out var run))
                return Task.FromResult<WorkflowRun>(null);

            lock (run)
            {
                run.Status = status;
                if (status == RunStatus.Completed || status == RunStatus.Failed)
                    run.CompletedAt = DateTime.UtcNow;
                if (proofHolonId.HasValue)
                    run.ProofHolonId = proofHolonId;
            }

            return Task.FromResult(run);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ActiveRunSummaryDto ToSummary(WorkflowRun run) => new()
        {
            ExecutionId      = run.ExecutionId,
            WorkflowId       = run.WorkflowId,
            SopName          = run.SopName,
            OrgId            = run.OrgId,
            Status           = run.Status,
            CurrentStepIndex = run.CurrentStepIndex,
            TotalSteps       = run.Steps.Count,
            CurrentStepName  = run.Steps.ElementAtOrDefault(run.CurrentStepIndex)?.Name,
            StartedAt        = run.StartedAt,
            Steps            = run.Steps,
            Assignees        = run.Assignees,
        };
    }
}
