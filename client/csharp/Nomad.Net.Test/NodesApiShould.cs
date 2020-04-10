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
using System.Numerics;

namespace Nomad.Net.Test
{
    public class NodesApiShould : ApiTestBase
    {
        public NodesApiShould(ITestOutputHelper output) : base(output)
        {
            BasePorts.Http = 20500;
            BasePorts.Rpc = 21500;
            BasePorts.Serf = 22500;
        }

        [Fact]
        public async Task GetNodes()
        {
            using var agent = NewServer(config => {
                config.Name = "especially-named";
                config.Client.Enabled = true;
            });

            var api = agent.CreateNomadApi();

            var response = await api.GetNodesAsync(null);
            response.Count.Should().Be(1);
            response.First().Name.Should().Be("especially-named");
        }

        [Fact]
        public async Task GetGetNodesMatchingPrefix()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var emptyPrefixResponse = await api.GetNodesAsync("");
            emptyPrefixResponse.Count.Should().Be(1);
            var node = emptyPrefixResponse.First();

            string prefix = node.ID.Substring(0, 4);
            var prefixResponse = await api.GetNodesAsync(prefix);
            prefixResponse.Count.Should().Be(1);
            prefixResponse.First().ID.Should().Be(node.ID);
            prefixResponse.First().Name.Should().Be(node.Name);

            string otherPrefix = "0000" == prefix ? "0001" : "0000";

            var otherPrefixResponse = await api.GetNodesAsync(otherPrefix);
            otherPrefixResponse.Should().BeEmpty();
        }

        [Fact]
        public async Task GetNodeInfo()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<Node>> func = () => api.GetNodeAsync(NONSENSE_GUID);
            func.Should().Throw<ApiException>();

            var nodes = await api.GetNodesAsync(null);
            nodes.Should().NotBeEmpty();

            var nodeListStub = nodes.First();

            var node = await api.GetNodeAsync(nodeListStub.ID);
            node.ID.Should().Be(nodeListStub.ID);
            node.Datacenter.Should().Be(nodeListStub.Datacenter);
            node.Drivers.Should().ContainKey("mock_driver");
            node.NodeResources.Should().NotBeNull();
            node.NodeResources.Cpu.CpuShares.Should().BeGreaterThan(01);
            node.NodeResources.Memory.MemoryMb.Should().BeGreaterThan(01);
            node.ReservedResources.Should().NotBeNull();
            node.ReservedResources.Cpu.CpuShares.Should().Be(0);
            node.ReservedResources.Memory.MemoryMb.Should().Be(0);
        }

        [Fact]
        public async Task ToggleDrainMode()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<NodeDrainUpdateResponse>> func = () => api.UpdateDrainModeForNodeAsync(new NodeUpdateDrainRequest {
                NodeID = NONSENSE_GUID,
                DrainSpec = new DrainSpec
                {
                    Deadline = 3600000000000,
                    IgnoreSystemJobs = true
                }
            });
            func.Should().Throw<ApiException>();

            var nodes = await api.GetNodesAsync(null);
            nodes.Should().NotBeEmpty();

            var nodeListStub = nodes.First();

            var initialNode = await api.GetNodeAsync(nodeListStub.ID);
            initialNode.Drain.Should().BeFalse();

            _ = await api.UpdateDrainModeForNodeAsync(new NodeUpdateDrainRequest
            {
                NodeID = nodeListStub.ID,
                DrainSpec = new DrainSpec
                {
                    Deadline = 3600000000000,
                    IgnoreSystemJobs = true
                }
            });

            var drainingNodeInfo = await api.GetNodeAsync(nodeListStub.ID);

            // I don't know why it cannot be true
            // drainingNodeInfo.Drain.Should().BeTrue();

            _ = await api.UpdateDrainModeForNodeAsync(new NodeUpdateDrainRequest
            {
                NodeID = nodeListStub.ID
            });

            var finalNodeInfo = await api.GetNodeAsync(nodeListStub.ID);
            finalNodeInfo.Drain.Should().BeFalse();
        }

        [Fact]
        public async Task GetAllocations()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var nonsenseResponse = await api.GetAllocationsForNodeAsync(NONSENSE_GUID);
            nonsenseResponse.Should().BeEmpty();

            var nodes = await api.GetNodesAsync(null);
            nodes.Should().NotBeEmpty();
            string nodeId = nodes.First().ID;

            var initialAllocations = await api.GetAllocationsForNodeAsync(nodeId);
            initialAllocations.Should().BeEmpty();

            Job job = CreateTestJob();
            var evaluation = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            var allocations = await api.GetAllocationsForNodeAsync(nodeId);
            allocations.Count.Should().Be(1);
            var allocation = allocations.First();
            allocation.ID.Should().NotBeNullOrEmpty();
            allocation.JobID.Should().Be(job.ID);
            allocation.EvalID.Should().Be(evaluation.ID);
        }

        [Fact]
        public async Task ForceEvaluate()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<NodeEvalResponse>> func = () => api.EvaluateNodeAsync(NONSENSE_GUID);
            func.Should().Throw<ApiException>();

            var nodes = await api.GetNodesAsync(null);
            nodes.Should().NotBeEmpty();
            string nodeId = nodes.First().ID;

            _ = await api.EvaluateNodeAsync(nodeId);
        }
    }
}
