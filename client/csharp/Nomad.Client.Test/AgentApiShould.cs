using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Nomad.Client.Test
{
    public class AgentApiShould : ApiTestBase
    {
        public AgentApiShould(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task ReturnSelfInfo()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var result = await api.GetSelfAsync();
            result.Member.Name.Should().NotBeNullOrEmpty();
            result.Config["Region"].Should().Be("test-region");
            result.Config["Datacenter"].Should().Be("dc1");
        }
    }
}