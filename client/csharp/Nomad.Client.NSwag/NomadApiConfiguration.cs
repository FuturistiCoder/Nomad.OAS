using System;

namespace HashiCorp.Nomad
{
    public class NomadApiConfiguration
    {
        public string BaseUrl { get; set; }

        public IObservable<string> ACLTokenObservable { get; set; }
    }
}