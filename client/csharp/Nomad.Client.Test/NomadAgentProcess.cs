using FluentAssertions;
using HashiCorp.Nomad;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;
using NomadTask = HashiCorp.Nomad.Task;
using System.Linq;
using System.Net.Http;

namespace Nomad.Client.Test
{
    internal class NomadAgentProcess : Process
    {
        private readonly NomadAgentConfiguration _configuration;
        private readonly ITestOutputHelper _output;
        private string _configFilePath;
        private static string _executePath;

        public NomadAgentProcess(NomadAgentConfiguration configuration, ITestOutputHelper output)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            InitConfigFile();
            InitStartInfo();
            CleanDataDir();

            OutputDataReceived += NomadAgentProcess_OutputDataReceived;
            ErrorDataReceived += NomadAgentProcess_ErrorDataReceived;
        }

        private void CleanDataDir()
        {
            if (Directory.Exists(_configuration.DataDir))
            {
                _output.WriteLine($"Deleting data_dir: {_configuration.DataDir}");
                Directory.Delete(_configuration.DataDir, true);
            }
        }

        private void NomadAgentProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            _output.WriteLine(e.Data ?? string.Empty);
        }

        private void NomadAgentProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            _output.WriteLine(e.Data ?? string.Empty);
        }

        static NomadAgentProcess()
        {
            InitExecutePath();
        }

        private static void InitExecutePath()
        {
            using var goProcess = new Process();
            var startInfo = goProcess.StartInfo;
            startInfo.FileName = "go";
            startInfo.Arguments = "env GOPATH";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            goProcess.Start();

            string goPath = goProcess.StandardOutput.ReadToEnd().TrimEnd();
            _executePath = Path.Combine(goPath, "bin", "nomad");
        }

        private void InitStartInfo()
        {
            StartInfo.FileName = _executePath;
            StartInfo.Arguments = $"agent -config {_configFilePath}";
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
        }

        private void InitConfigFile()
        {
            _configFilePath = $"{Path.GetTempPath()}NomadTestConfig_{_configuration.Name}.json";
            _output.WriteLine($"config file location: {_configFilePath}");
            File.WriteAllText(_configFilePath, JsonConvert.SerializeObject(_configuration, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            }));
        }

        public new bool Start()
        {
            var configJson = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            _output.WriteLine("starting a new nomad agent process by using config:");
            _output.WriteLine(configJson);
            var result = base.Start();
            _output.WriteLine($"[process] agent process id: {Id}");
            BeginOutputReadLine();
            BeginErrorReadLine();
            WaitNomadAgentStarted();

            return result;
        }

        public NomadApi CreateNomadApi()
        {
            var builder = new UriBuilder
            {
                Host = "127.0.0.1",
                Port = _configuration.Ports.Http,
                Scheme = _configuration.Tls.Http ? "https" : "http",
                Path = "/v1"
            };

            var api = new NomadApi(new NomadApiConfiguration
            {
                BaseUrl = builder.ToString()
            });

            return api;
        }

        private void WaitNomadAgentStarted()
        {
            var api = CreateNomadApi();
            var result = Policy
                .HandleResult<ICollection<NodeListStub>>(nodes => nodes.Where(node => node.Status != "ready").Any())
                .Or<ApiException>()
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(20, i => TimeSpan.FromSeconds(1))
                .ExecuteAndCaptureAsync(async () =>
                {
                    _output.WriteLine("Waiting for nomad agent process be ready...");
                    if (HasExited)
                    {
                        _output.WriteLine($"Nomad agent process (name: {_configuration.Name} ) has exited");
                        throw new Exception("nomad agnet process is exited");
                    }
                    return await api.GetNodesAsync(null);
                }).GetAwaiter().GetResult();

            result.Outcome.Should().Be(OutcomeType.Successful);
            HasExited.Should().BeFalse();
        }

        protected override void Dispose(bool disposing)
        {
            OutputDataReceived -= NomadAgentProcess_OutputDataReceived;
            ErrorDataReceived -= NomadAgentProcess_ErrorDataReceived;
            _output.WriteLine($"[process] killing process id: {Id}");
            Kill(true);
        }
    }
}
