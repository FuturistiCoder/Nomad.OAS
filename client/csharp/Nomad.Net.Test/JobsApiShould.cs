using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using HashiCorp.Nomad;
using Xunit;
using Task = System.Threading.Tasks.Task;
using NomadTask = HashiCorp.Nomad.Task;
using System.Linq;
using Xunit.Abstractions;

namespace Nomad.Net.Test
{
    public class JobsApiShould : ApiTestBase
    {
        public JobsApiShould(ITestOutputHelper output) : base(output)
        {
            BasePorts.Http = 20100;
            BasePorts.Rpc = 21100;
            BasePorts.Serf = 22100;
        }

        [Fact]
        public async Task RegisterANewJob()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            await AssertThatNoJobsHaveBeenRun(api);
            var job = CreateTestJob();

            var registerResponse = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            registerResponse.EvalID.Should().NotBeNullOrEmpty();
            var jobs = await api.GetJobsAsync(null);  
            jobs.Count.Should().Be(1);
            jobs.First().ID.Should().Be(job.ID);
            foreach (var j in jobs)
            {
                j.Datacenters.Should().NotBeNullOrEmpty();
            }
        }

        private async Task AssertThatNoJobsHaveBeenRun(NomadApi api)
        {
            var result = await api.GetJobsAsync(null);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateJob()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            var result = await api.ValidateJobAsync(new JobValidateRequest { Job = job });
            result.ValidationErrors.Should().BeNullOrEmpty();

            job.ID = null;
            result = await api.ValidateJobAsync(new JobValidateRequest { Job = job });
            result.ValidationErrors.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RevertJob()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            _ = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);
            job.Meta.Add("foo", "bar");
            _ = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            // Fails at incorrect version
            Func<Task<JobRegisterResponse>> func = () =>
                api.RevertJobAsync(job.ID, new JobRevertRequest
                {
                    JobID = job.ID,
                    JobVersion = 0,
                    EnforcePriorVersion = 10
                });
            func.Should().Throw<ApiException>();

            var revertedResponse = await api.RevertJobAsync(job.ID, new JobRevertRequest
            {
                JobID = job.ID,
                JobVersion = 0,
                EnforcePriorVersion = 1
            });
        }
    }
}
