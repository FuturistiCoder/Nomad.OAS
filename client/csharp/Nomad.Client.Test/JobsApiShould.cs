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

namespace Nomad.Client.Test
{
    public class JobsApiShould : ApiTestBase
    {
        public JobsApiShould(ITestOutputHelper output) : base(output)
        {
            BasePorts.Http = 20400;
            BasePorts.Rpc = 21400;
            BasePorts.Serf = 22400;
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

        [Fact]
        public async Task GetJobInfo()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            // not found
            Func<Task<Job>> func = () => api.GetJobAsync(job.ID);
            func.Should().Throw<ApiException>();

            _ = await api.RegisterJobAsync(new RegisterJobRequest { Job = job });
            var response = await api.GetJobAsync(job.ID);
            response.ID.Should().Be(job.ID);
        }

        [Fact]
        public async Task GetJobVersions()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            // not found
            Func<Task<JobVersionsResponse>> func = () => api.GetJobVersionsAsync("job1");
            func.Should().Throw<ApiException>();

            _ = await api.RegisterJobAsync(new RegisterJobRequest { Job = job });
            var response = await api.GetJobVersionsAsync(job.ID);
            response.Versions.Count.Should().BeGreaterThan(0);
            response.Versions.First().ID.Should().Be(job.ID);
        }

        [Fact]
        public async Task GetJobsHavingIdPrefix()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var jobs = await api.GetJobsAsync("dummy");
            jobs.Should().BeEmpty();

            var job = CreateTestJob();
            var response = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            var updatedJobs = await api.GetJobsAsync(job.ID.Substring(0, 4));
            updatedJobs.Count.Should().Be(1);
            updatedJobs.First().ID.Should().Be(job.ID);
        }

        [Fact]
        public async Task GetAllocationsBelongingToAJob()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);
            var response = await api.GetJobAllocationsAsync(job.ID, null);
            response.Should().NotBeEmpty();
            var allo = response.First();
            allo.ID.Should().NotBeNullOrEmpty();
            allo.JobID.Should().Be(job.ID);
            allo.EvalID.Should().Be(eval.ID);
        }

        [Fact]
        public async Task GetEvaluationsForAJob()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            var preRegistrationResponse = await api.GetJobEvaluationsAsync(job.ID);
            preRegistrationResponse.Should().BeEmpty();

            var eval = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(api, job);

            var postRegistrationResponse = await api.GetJobEvaluationsAsync(job.ID);
            postRegistrationResponse.Should().NotBeEmpty();
            var evaluation = postRegistrationResponse.Last();

            evaluation.JobID.Should().Be(job.ID);
            evaluation.ID.Should().Be(eval.ID);
        }

        [Fact]
        public async Task DeregisterAnExistingJob()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            var eval = await RegisterTestJobAndPollUntilEvaluationCompletes(api, job);

            var retrievedJob = await api.GetJobAsync(job.ID);
            retrievedJob.Status.Should().Be("running");

            var dummyDeregisterResponse = await api.StopJobAsync("nope", null);
            var dummyEvaluation = await PollForEvaluationCompletion(api, dummyDeregisterResponse.EvalID);
            dummyEvaluation.NextEval.Should().BeNullOrEmpty();
            dummyEvaluation.BlockedEval.Should().BeNullOrEmpty();

            retrievedJob = await api.GetJobAsync(job.ID);
            retrievedJob.Status.Should().Be("running");

            var deregisterResponse = await api.StopJobAsync(job.ID, null);
            deregisterResponse.EvalID.Should().NotBeNullOrEmpty();

            var evaluation = await PollForEvaluationCompletion(api, deregisterResponse.EvalID);
            evaluation.NextEval.Should().BeNullOrEmpty();

            retrievedJob = await api.GetJobAsync(job.ID);
            retrievedJob.Stop.Should().BeTrue();
            retrievedJob.Status.Should().Be("dead");

            deregisterResponse = await api.StopJobAsync(job.ID, true);
            evaluation = await PollForEvaluationCompletion(api, deregisterResponse.EvalID);
            evaluation.NextEval.Should().BeNullOrEmpty();

            Func<Task<Job>> func = () => api.GetJobAsync(job.ID);
            func.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task ForceEvaluationOfExistingJob()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            await AssertThatNoJobsHaveBeenRun(api);

            var job = CreateTestJob();
            var jobRegisterResponse = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            var jobForceEvaluateResponse = await api.EvaluateJobAsync(new JobEvaluateRequest
            {
                JobID = job.ID,
                EvalOptions = new EvalOptions
                {
                    ForceReschedule = true
                }
            });

            jobForceEvaluateResponse.EvalID.Should().NotBeNullOrEmpty();

            var evaluationsResponse = await api.GetJobEvaluationsAsync(job.ID);
            evaluationsResponse.Where(eval => eval.ID == jobForceEvaluateResponse.EvalID).Should().NotBeEmpty();
        }

        [Fact]
        public async Task ForceRunOfPeriodJob()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();
            job.Periodic = new PeriodicConfig
            {
                Enabled = true,
                Spec = "*/30 * * * *",
                SpecType = "cron"
            };

            await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            await api.GetJobAsync(job.ID);

            var forceResponse = await api.ForceNewPeriodicInstanceAsync(job.ID);
            forceResponse.EvalID.Should().NotBeNullOrEmpty();

            var eval = await api.GetEvaluationAsync(forceResponse.EvalID);
            eval.ID.Should().Be(forceResponse.EvalID);
        }

        [Fact]
        public async Task CreateJobPlan()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            var planResponse = await api.PlanJobAsync(new JobPlanRequest
            {
                Job = job,
                Diff = true
            });

            planResponse.JobModifyIndex.Should().BeGreaterThan(0);
            planResponse.Diff.ID.Should().NotBeNullOrEmpty();
            planResponse.Annotations.Should().NotBeNull();
            planResponse.CreatedEvals.Should().NotBeEmpty();

            var noDiffResponse = await api.PlanJobAsync(new JobPlanRequest
            {
                Job = job,
                Diff = false
            });

            noDiffResponse.JobModifyIndex.Should().BeGreaterThan(0);
            noDiffResponse.Diff.Should().BeNull();
            noDiffResponse.Annotations.Should().NotBeNull();
            noDiffResponse.CreatedEvals.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetJobSummary()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            Func<Task<JobSummary>> func = () => api.GetJobSummaryAsync(job.ID);
            func.Should().Throw<ApiException>().Which.StatusCode.Should().Be(404);

            var registerResponse = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            var jobSummaryResponse = await api.GetJobSummaryAsync(job.ID);
            jobSummaryResponse.JobID.Should().Be(job.ID);
            jobSummaryResponse.Summary.Should().ContainKey(job.TaskGroups.First().Name);
        }

        [Fact]
        public async Task BlockOnNonexistentDeviceRequest()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var job = CreateTestJob();

            var dev = new RequestedDevice
            {
                Name = "non-existent",
                Count = 1
            };

            var tg0 = job.TaskGroups.First();
            tg0.Tasks.First().Resources = new Resources
            {
                Devices = new[] { dev }
            };

            var registerResponse = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            var eval = await PollForEvaluationCompletion(api, registerResponse.EvalID);
            eval.FailedTgAllocs[tg0.Name].ConstraintFiltered.Should().ContainKey("missing devices");
        }
    }
}
