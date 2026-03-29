using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextGenSoftware.OASIS.API.ONODE.WebAPI.Models.Workflow;

namespace NextGenSoftware.OASIS.API.ONODE.WebAPI.Interfaces
{
    /// <summary>
    /// Stores and retrieves live workflow runs.
    ///
    /// Current implementation is in-memory (WorkflowRunStore). A future version
    /// will back each WorkflowRun with a Holon persisted via HyperDrive so runs
    /// survive server restarts and are auditable on-chain.
    /// </summary>
    public interface IWorkflowRunStore
    {
        Task<WorkflowRun>              CreateAsync(WorkflowRun run);
        Task<WorkflowRun>              GetAsync(string executionId);
        Task<IEnumerable<WorkflowRun>> GetActiveAsync(string orgId = null);
        Task<WorkflowRun>              CompleteStepAsync(string executionId, string stepId, CompleteStepRequest req);
        Task<WorkflowRun>              SignOffStepAsync(string executionId, string stepId, SignOffStepRequest req, Guid? proofHolonId = null);
        Task<WorkflowRun>              SetRunStatusAsync(string executionId, RunStatus status, Guid? proofHolonId = null);
    }
}
