using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HashiCorp.Nomad
{
    public abstract class NomadApiBase : IObserver<string>
    {
        private const string ACLTokenHeader = "X-Nomad-Token";
        private string _aclToken;
        private readonly NomadApiConfiguration _configuration;

        public string BaseUrl { get; set; }

        public NomadApiBase(NomadApiConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            BaseUrl = _configuration.BaseUrl;
        }

        protected void UpdateJsonSerializerSettings(JsonSerializerSettings settings)
        {
        }

        protected Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
        {
            var httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(_aclToken))
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(ACLTokenHeader, _aclToken);
            }

            return System.Threading.Tasks.Task.FromResult(httpClient);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(string value)
            => _aclToken = value;
    }
}