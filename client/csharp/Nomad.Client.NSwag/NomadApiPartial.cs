namespace HashiCorp.Nomad
{
    public partial class NomadApi
    {
        public System.Threading.Tasks.Task<NodeDrainUpdateResponse> UpdateDrainModeForNodeAsync(NodeUpdateDrainRequest body)
            => UpdateDrainModeForNodeAsync(body.NodeID, body, System.Threading.CancellationToken.None);

        public System.Threading.Tasks.Task<NodeDrainUpdateResponse> UpdateDrainModeForNodeAsync(NodeUpdateDrainRequest body, System.Threading.CancellationToken cancellationToken)
            => UpdateDrainModeForNodeAsync(body.NodeID, body, cancellationToken);

        public System.Threading.Tasks.Task<JobPlanResponse> PlanJobAsync(JobPlanRequest body)
            => PlanJobAsync(body.Job.ID, body);

        public System.Threading.Tasks.Task<JobPlanResponse> PlanJobAsync(JobPlanRequest body, System.Threading.CancellationToken cancellationToken)
            => PlanJobAsync(body.Job.ID, body, cancellationToken);

        public System.Threading.Tasks.Task<JobRegisterResponse> EvaluateJobAsync(JobEvaluateRequest body)
            => EvaluateJobAsync(body.JobID, body);

        public System.Threading.Tasks.Task<JobRegisterResponse> EvaluateJobAsync(JobEvaluateRequest body, System.Threading.CancellationToken cancellationToken)
            => EvaluateJobAsync(body.JobID, body, cancellationToken);
    }
}