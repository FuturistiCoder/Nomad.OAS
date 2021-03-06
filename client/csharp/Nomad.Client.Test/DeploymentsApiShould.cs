﻿using FluentAssertions;
using HashiCorp.Nomad;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Nomad.Client.Test
{
    public class DeploymentsApiShould : ApiTestBase
    {
        public DeploymentsApiShould(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task DiscoverDeployment()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            job.Type = "service";
            job.Update = new HashiCorp.Nomad.UpdateStrategy
            {
                Canary = 1
            };

            await RegisterTestJobAndPollUntilEvaluationCompletes(api, job);

            var deployments = await api.GetDeploymentsAsync(null);
            deployments.Count.Should().Be(1);

            var fromJob = await api.GetJobDeploymentsAsync(job.ID, null);
            fromJob.Count.Should().Be(1);
            fromJob.First().ID.Should().Be(deployments.First().ID);
        }

        [Fact]
        public async Task HandleDeficientUseCases()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<Deployment>> func = () => api.GetDeploymentAsync("f6004f6b-a58d-835f-5acf-3ad349000835");
            func.Should().Throw<ApiException>();

            var allocations = await api.GetAllocationsForDeploymentAsync("f6004f6b-a58d-835f-5acf-3ad349000835");
            allocations.Should().BeEmpty();

            var jobDeployment = await api.GetJobLatestDeploymentAsync("nonexistent-job");
            jobDeployment.Should().BeNull();
        }
    }
}