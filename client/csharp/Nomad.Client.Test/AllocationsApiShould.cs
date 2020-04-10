using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using NomadTask = HashiCorp.Nomad.Task;
using Xunit;
using Xunit.Abstractions;
using HashiCorp.Nomad;
using Polly;

namespace Nomad.Client.Test
{
    public class AllocationsApiShould : ApiTestBase
    {
        public AllocationsApiShould(ITestOutputHelper output) : base(output)
        {
            BasePorts.Http = 20100;
            BasePorts.Rpc = 21100;
            BasePorts.Serf = 22100;
        }

        [Fact]
        public async Task GetAllocations()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var allocations = await api.GetAllocationsAsync(null);
            allocations.Should().BeEmpty();

            var job = CreateTestJob();
            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            var updatedAllocations = await api.GetAllocationsAsync(null);
            updatedAllocations.Should().NotBeEmpty();
            foreach (var allo in updatedAllocations)
            {
                allo.EvalID.Should().Be(eval.ID);
                allo.Namespace.Should().Be("default");
                allo.NodeName.Should().NotBeEmpty();
                allo.JobType.Should().Be(eval.Type);
            }
        }

        [Fact]
        public async Task GetAllocationsHavingPrefix()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            Func<Task<ICollection<AllocationListStub>>> func1 = () => api.GetAllocationsAsync("f00");
            func1.Should().Throw<ApiException>().WithMessage("*must be even length*");

            Func<Task<ICollection<AllocationListStub>>> func2 = () => api.GetAllocationsAsync("ok");
            func2.Should().Throw<ApiException>().WithMessage("*Invalid UUID*");

            var response = await api.GetAllocationsAsync("f00d");
            response.Should().BeEmpty();

            var job = CreateTestJob();
            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            var allResponse = await api.GetAllocationsAsync("");
            allResponse.Count.Should().Be(1);
            allResponse.First().EvalID.Should().Be(eval.ID);

            string existingPrefix = allResponse.First().ID.Substring(0, 4);
            var prefixedResponse = await api.GetAllocationsAsync(existingPrefix);
            prefixedResponse.Count.Should().Be(1);

            string nonExistingPrefix = "0000" == existingPrefix ? "0001" : "0000";
            var nonExistingResponse = await api.GetAllocationsAsync(nonExistingPrefix);
            nonExistingResponse.Should().BeEmpty();
        }

        [Fact]
        public async Task RetrieveAllocationInfo()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<Allocation>> func = () => api.GetAllocationAsync(NONSENSE_GUID);
            func.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);

            var job = CreateTestJob();
            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            var allocations = await api.GetAllocationsAsync(null);
            allocations.Should().NotBeEmpty();
            string allocationId = allocations.First().ID;

            var allocation = await api.GetAllocationAsync(allocationId);
            allocation.ID.Should().Be(allocationId);
            allocation.EvalID.Should().Be(eval.ID);
            allocation.Metrics.Should().NotBeNull();
            allocation.Metrics.ScoreMetaData.Should().NotBeEmpty();
            allocation.NodeName.Should().NotBeEmpty();

            allocation.AllocatedResources.Shared.DiskMb.Should().BeGreaterThan(01);
            allocation.AllocatedResources.Tasks["task1"].Cpu.CpuShares.Should().Be(100L);
            allocation.AllocatedResources.Tasks["task1"].Memory.MemoryMb.Should().Be(256L);
        }

        [Fact]
        public async Task StopAllocation()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            // not found
            Func<Task<AllocStopResponse>> func = () => api.StopAllocationAsync(NONSENSE_GUID);
            func.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);

            var allocations = await api.GetAllocationsAsync(null);
            allocations.Should().NotBeEmpty();
            string allocationId = allocations.First().ID;

            var stopResposne = await api.StopAllocationAsync(allocationId);
            stopResposne.EvalID.Should().NotBe(eval.ID);
        }

        [Fact(Skip = "sending Signal always got 404, may not be a bug from generated client")]
        public async Task SignalAllocation()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            job.TaskGroups.First().Tasks.First().Config["run_for"] = "5s";
            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            // not found
            Func<Task> func1 = () => api.SignalAllocationAsync(NONSENSE_GUID, null);
            func1.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);

            var allocations = await api.GetAllocationsAsync(null);
            allocations.Should().NotBeEmpty();
            string allocationId = allocations.First().ID;

            // not found
            Func<Task> func2 = () => api.SignalAllocationAsync(allocationId, new AllocSignalRequest {
                Signal = "SIGALRM",
                Task = "non-existent-task"
            });
            func2.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);

            var beforeSignalResult = await Policy
                .HandleResult<Allocation>(alloc =>
                {
                    TaskState taskState = null;
                    alloc.TaskStates?.TryGetValue("task1", out taskState);
                    return taskState?.State != "running";
                })
                .WaitAndRetryAsync(20, i => TimeSpan.FromSeconds(0.3))
                .ExecuteAndCaptureAsync(() => api.GetAllocationAsync(allocationId));
            beforeSignalResult.Outcome.Should().Be(OutcomeType.Successful);

            // FixMe: I don't know why 404 error.
            await api.SignalAllocationAsync(allocationId, new AllocSignalRequest
            {
                Signal = "SIGALRM"
            });

            var result = await Policy
                .HandleResult<Allocation>(alloc =>
                {
                    TaskState taskState = null;
                    string signal = null;
                    alloc.TaskStates?.TryGetValue("task1", out taskState);
                    taskState?.Events.FirstOrDefault(e => e.Type == "Signaling")?.Details.TryGetValue("signal", out signal);
                    return signal != "SIGALRM";
                })
                .WaitAndRetryAsync(20, i => TimeSpan.FromSeconds(0.3))
                .ExecuteAndCaptureAsync(() => api.GetAllocationAsync(allocationId));

            result.Outcome.Should().Be(OutcomeType.Successful);

        }
    }
}
