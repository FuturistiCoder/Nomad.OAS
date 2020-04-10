using Microsoft.Rest;
using System;
using System.Net.Http;
using System.Threading;

namespace HashiCorp.Nomad.Models
{
    public class ACLTokenServiceClientCredentials : ServiceClientCredentials
    {
        private const string ACLTokenHeader = "X-Nomad-Token";
        private string _token;

        public ACLTokenServiceClientCredentials(string token)
        {
            _token = token;
        }

        public void UpdateToken(string token)
        {
            _token = token;
        }

        public override System.Threading.Tasks.Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.IsNullOrWhiteSpace(_token))
            {
                request.Headers.TryAddWithoutValidation(ACLTokenHeader, _token);
            }

            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}