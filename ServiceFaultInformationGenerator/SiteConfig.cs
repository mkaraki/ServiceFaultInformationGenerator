using YamlDotNet.Serialization;

namespace ServiceFaultInformationGenerator
{
    internal class SiteConfig
    {
        [YamlMember(Alias = "baseUrl")]
        public string BaseUrl { get; set; }
    }
}
